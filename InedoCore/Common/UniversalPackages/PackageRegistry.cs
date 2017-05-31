using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
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

        public void Dispose()
        {
            if (!this.disposed)
            {
                if (this.LockToken != null)
                {
                    try
                    {
                        Task.Run(() => this.LockRegistryAsync(this.agent.GetService<IFileOperationsExecuter>(), CancellationToken.None)).Wait();
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
            var linux = await agent.GetServiceAsync<ILinuxFileOperationsExecuter>().ConfigureAwait(false);
            if (linux != null)
                return "/var/lib/upack";

            var windows = await agent.GetServiceAsync<IRemoteMethodExecuter>().ConfigureAwait(false);
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
            var fileInfo = await fileOps.GetFileInfoAsync(fileName).ConfigureAwait(false);
            while (fileInfo != null && DateTime.UtcNow - fileInfo.LastWriteTimeUtc <= new TimeSpan(0, 0, 10))
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                fileInfo = await fileOps.GetFileInfoAsync(fileName).ConfigureAwait(false);
            }

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
        private static async Task<IList<RegisteredPackage>> GetInstalledPackagesAsync(IFileOperationsExecuter fileOps, string registryRoot)
        {
            var fileName = fileOps.CombinePath(registryRoot, "installedPackages.json");

            if (!await fileOps.DirectoryExistsAsync(fileName).ConfigureAwait(false))
                return new RegisteredPackage[0];

            using (var configStream = await fileOps.OpenFileAsync(fileName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
            using (var streamReader = new StreamReader(configStream, InedoLib.UTF8Encoding))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return new JsonSerializer().Deserialize<RegisteredPackage[]>(jsonReader) ?? new RegisteredPackage[0];
            }
        }
    }
}
