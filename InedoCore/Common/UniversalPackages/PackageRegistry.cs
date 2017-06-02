using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.IO;
using Newtonsoft.Json;
#if Otter
using Agent = Inedo.Otter.Extensibility.Agents.OtterAgent;
#elif BuildMaster
using Agent = Inedo.BuildMaster.Extensibility.Agents.BuildMasterAgent;
#endif

namespace Inedo.Extensions.UniversalPackages
{
    internal sealed class PackageRegistry : IDisposable
    {
        private Agent agent;
        private bool disposed;

        private PackageRegistry(Agent agent, string registryRoot)
        {
            this.agent = agent;
            this.RegistryRoot = registryRoot;
        }

        public string RegistryRoot { get; }
        public string LockToken { get; private set; }

        public static async Task<PackageRegistry> GetRegistryAsync(Agent agent, bool openUserRegistry)
        {
            var root = await (openUserRegistry ? GetCurrentUserRegistryRootAsync(agent) : GetMachineRegistryRootAsync(agent)).ConfigureAwait(false);
            return new PackageRegistry(agent, root);
        }

        public async Task LockAsync(CancellationToken cancellationToken) => await this.LockRegistryAsync(await this.agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        public async Task UnlockAsync() => await this.UnlockRegistryAsync(await this.agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false)).ConfigureAwait(false);
        public async Task<IList<RegisteredPackage>> GetInstalledPackagesAsync()
        {
            return await GetInstalledPackagesAsync(await this.agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false), this.RegistryRoot).ConfigureAwait(false);
        }
        public async Task RegisterPackageAsync(RegisteredPackage package, CancellationToken cancellationToken)
        {
            var fileOps = await this.agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
            var packages = await GetInstalledPackagesAsync(fileOps, this.RegistryRoot).ConfigureAwait(false);

            packages.RemoveAll(p => RegisteredPackage.NameAndGroupEquals(p, package));
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

            var lockDescription = "Locked by Otter";
            var lockToken = Guid.NewGuid().ToString();

            TryAgain:
            var fileInfo = await getFileInfoAsync().ConfigureAwait(false);
            while (fileInfo != null && DateTime.UtcNow - fileInfo.LastWriteTimeUtc <= new TimeSpan(0, 0, 10))
            {
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
                    writer.WriteLine(lockToken.ToString());
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
            catch (IOException)
            {
                // file may be in use by other process
                goto TryAgain;
            }

            // at this point, lock is acquired provided everyone is following the rules
            this.LockToken = lockToken;

            async Task<SlimFileInfo> getFileInfoAsync()
            {
                try
                {
                    return await fileOps.GetFileInfoAsync(fileName).ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                catch (DirectoryNotFoundException)
                {
                    return null;
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
        private static async Task<List<RegisteredPackage>> GetInstalledPackagesAsync(IFileOperationsExecuter fileOps, string registryRoot)
        {
            var fileName = fileOps.CombinePath(registryRoot, "installedPackages.json");

            if (!await fileOps.FileExistsAsync(fileName).ConfigureAwait(false))
                return new List<RegisteredPackage>();

            using (var configStream = await fileOps.OpenFileAsync(fileName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
            using (var streamReader = new StreamReader(configStream, InedoLib.UTF8Encoding))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return (new JsonSerializer().Deserialize<RegisteredPackage[]>(jsonReader) ?? new RegisteredPackage[0]).ToList();
            }
        }
        private static async Task WriteInstalledPackagesAsync(IFileOperationsExecuter fileOps, string registryRoot, IEnumerable<RegisteredPackage> packages)
        {
            var fileName = fileOps.CombinePath(registryRoot, "installedPackages.json");

            using (var configStream = await fileOps.OpenFileAsync(fileName, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
            using (var streamWriter = new StreamWriter(configStream, InedoLib.UTF8Encoding))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                new JsonSerializer { Formatting = Formatting.Indented }.Serialize(jsonWriter, packages.ToArray());
            }
        }
    }
}
