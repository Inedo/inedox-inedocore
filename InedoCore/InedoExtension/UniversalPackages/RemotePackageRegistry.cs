using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Extensibility.Agents;
using Inedo.IO;
using Newtonsoft.Json;

namespace Inedo.Extensions.UniversalPackages
{
    internal sealed class RemotePackageRegistry : IDisposable
    {
        public static readonly SemaphoreSlim registryLock = new SemaphoreSlim(1, 1);

        private Agent agent;
        private ILogger logger;
        private bool disposed;

        private RemotePackageRegistry(Agent agent, string registryRoot, ILogger logger = null)
        {
            this.agent = agent;
            this.RegistryRoot = registryRoot;
            this.logger = logger;
        }

        public string RegistryRoot { get; }
        public string LockToken { get; private set; }

        public static async Task<RemotePackageRegistry> GetRegistryAsync(Agent agent, bool openUserRegistry)
        {
            var root = await (openUserRegistry ? GetCurrentUserRegistryRootAsync(agent) : GetMachineRegistryRootAsync(agent)).ConfigureAwait(false);
            return new RemotePackageRegistry(agent, root);
        }

        public async Task LockAsync(CancellationToken cancellationToken) => await this.LockRegistryAsync(await this.agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        public async Task UnlockAsync() => await this.UnlockRegistryAsync(await this.agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false)).ConfigureAwait(false);
        public async Task<IList<RegisteredPackageModel>> GetInstalledPackagesAsync()
        {
            return await GetInstalledPackagesAsync(await this.agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false), this.RegistryRoot).ConfigureAwait(false);
        }
        public async Task RegisterPackageAsync(RegisteredPackageModel package, CancellationToken cancellationToken)
        {
            var fileOps = await this.agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
            var packages = await GetInstalledPackagesAsync(fileOps, this.RegistryRoot).ConfigureAwait(false);

            packages.RemoveAll(p => RegisteredPackageModel.NameAndGroupEquals(p, package));
            packages.Add(package);

            await WriteInstalledPackagesAsync(fileOps, this.RegistryRoot, packages).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                if (this.LockToken != null)
                {
                    try
                    {
                        Task.Run(() => this.UnlockRegistryAsync(this.agent.GetService<IFileOperationsExecuter>())).Wait();
                    }
                    catch
                    {
                    }
                }

                this.disposed = true;
            }
        }

        private static async Task<string> GetMachineRegistryRootAsync(Agent agent)
        {
            var linux = await agent.TryGetServiceAsync<ILinuxFileOperationsExecuter>().ConfigureAwait(false);
            if (linux != null)
                return "/var/lib/upack";

            var windows = await agent.TryGetServiceAsync<IRemoteMethodExecuter>().ConfigureAwait(false);
            if (windows != null)
                return await windows.InvokeFuncAsync(Remote.GetMachineRegistryRoot).ConfigureAwait(false);

            throw new ArgumentException("Agent does not support file operations.");
        }
        private static async Task<string> GetCurrentUserRegistryRootAsync(Agent agent)
        {
            var linux = await agent.GetServiceAsync<ILinuxFileOperationsExecuter>().ConfigureAwait(false);
            if (linux != null)
                return "~/.upack";

            var windows = await agent.GetServiceAsync<IRemoteMethodExecuter>().ConfigureAwait(false);
            if (windows != null)
                return await windows.InvokeFuncAsync(Remote.GetCurrentUserRegistryRoot).ConfigureAwait(false);

            throw new ArgumentException("Agent does not support file operations.");
        }

        private async Task LockRegistryAsync(IFileOperationsExecuter fileOps, CancellationToken cancellationToken)
        {
            var fileName = fileOps.CombinePath(this.RegistryRoot, ".lock");

            var lockDescription = "Locked by " + SDK.ProductName;
            var lockToken = Guid.NewGuid().ToString();

            TryAgain:
            var fileInfo = await getFileInfoAsync().ConfigureAwait(false);
            string lastToken = null;
            while (fileInfo.Item1 != null && DateTime.UtcNow - fileInfo.Item1.LastWriteTimeUtc <= new TimeSpan(0, 0, 10))
            {
                if ((lastToken == null || !string.Equals(lastToken, fileInfo.Item3)) && fileInfo.Item3 != null)
                {
                    this.logger?.LogDebug("Package registry is locked: " + fileInfo.Item2);
                    lastToken = fileInfo.Item3;
                }
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                fileInfo = await getFileInfoAsync().ConfigureAwait(false);
            }

            // ensure registry root exists
            await fileOps.CreateDirectoryAsync(this.RegistryRoot).ConfigureAwait(false);

            try
            {
                // write out the lock info
                using (var lockStream = await fileOps.OpenFileAsync(fileName, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
                using (var writer = new StreamWriter(lockStream, InedoLib.UTF8Encoding))
                {
                    writer.WriteLine(lockDescription);
                    writer.WriteLine(lockToken);
                }

                // verify that we acquired the lock
                using (var lockStream = await fileOps.OpenFileAsync(fileName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
                using (var reader = new StreamReader(lockStream, InedoLib.UTF8Encoding))
                {
                    if (reader.ReadLine() != lockDescription)
                        goto TryAgain;

                    if (reader.ReadLine() != lockToken)
                        goto TryAgain;
                }
            }
            catch (IOException ex)
            {
                this.logger?.LogDebug("Locking package registry failed: " + ex.Message);

                // file may be in use by other process
                goto TryAgain;
            }

            // at this point, lock is acquired provided everyone is following the rules
            this.LockToken = lockToken;

            async Task<(SlimFileInfo, string, string)> getFileInfoAsync()
            {
                try
                {
                    var info = await fileOps.GetFileInfoAsync(fileName).ConfigureAwait(false);

                    string description = null, token = null;
                    try
                    {
                        using (var lockStream = await fileOps.OpenFileAsync(fileName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
                        using (var reader = new StreamReader(lockStream, InedoLib.UTF8Encoding))
                        {
                            description = await reader.ReadLineAsync().ConfigureAwait(false);
                            token = await reader.ReadLineAsync().ConfigureAwait(false);
                        }
                    }
                    catch (IOException)
                    {
                    }

                    return (info, description, token);
                }
                catch (FileNotFoundException)
                {
                    return (null, null, null);
                }
                catch (DirectoryNotFoundException)
                {
                    return (null, null, null);
                }
            }
        }
        private async Task UnlockRegistryAsync(IFileOperationsExecuter fileOps)
        {
            if (this.LockToken == null)
                return;

            var fileName = fileOps.CombinePath(this.RegistryRoot, ".lock");
            if (!await fileOps.FileExistsAsync(fileName).ConfigureAwait(false))
                return;

            string token;
            using (var lockStream = await fileOps.OpenFileAsync(fileName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
            using (var reader = new StreamReader(lockStream, InedoLib.UTF8Encoding))
            {
                reader.ReadLine();
                token = reader.ReadLine();
            }

            if (token == this.LockToken)
                await fileOps.DeleteFileAsync(fileName).ConfigureAwait(false);

            this.LockToken = null;
        }
        private static async Task<List<RegisteredPackageModel>> GetInstalledPackagesAsync(IFileOperationsExecuter fileOps, string registryRoot)
        {
            var fileName = fileOps.CombinePath(registryRoot, "installedPackages.json");

            if (!await fileOps.FileExistsAsync(fileName).ConfigureAwait(false))
                return new List<RegisteredPackageModel>();

            using (var configStream = await fileOps.OpenFileAsync(fileName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
            using (var streamReader = new StreamReader(configStream, InedoLib.UTF8Encoding))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return (new JsonSerializer().Deserialize<RegisteredPackageModel[]>(jsonReader) ?? new RegisteredPackageModel[0]).ToList();
            }
        }
        private static async Task WriteInstalledPackagesAsync(IFileOperationsExecuter fileOps, string registryRoot, IEnumerable<RegisteredPackageModel> packages)
        {
            var fileName = fileOps.CombinePath(registryRoot, "installedPackages.json");

            using (var configStream = await fileOps.OpenFileAsync(fileName, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
            using (var streamWriter = new StreamWriter(configStream, InedoLib.UTF8Encoding))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                new JsonSerializer { Formatting = Formatting.Indented }.Serialize(jsonWriter, packages.ToArray());
            }
        }

        internal static class Remote
        {
            public static string GetMachineRegistryRoot() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "upack");
            public static string GetCurrentUserRegistryRoot() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".upack");
        }
    }
}
