using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.Configurations.Network;
#if Otter
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.Network
{
    [DisplayName("Ensure Hosts Entry")]
    [Description("Ensures an entry in the hosts file on a server.")]
    [ScriptAlias("Ensure-HostsEntry")]
    [Tag("network")]
    [ScriptNamespace(Namespaces.Network, PreferUnqualified = true)]
    [Example(@"
# bind otter.localhost to local ip
Ensure-HostsEntry otter.localhost (IP: 127.0.0.1);

# override hdars.com to a local address
Ensure-HostsEntry (
  Host: hdars.com
  IP: 192.168.10.0);
")]
    public sealed class EnsureHostsEntryOperation : EnsureOperation<HostsEntryConfiguration>
    {
        private static readonly LazyRegex CommentRegex = new LazyRegex(@"^\s*#", RegexOptions.Compiled);
        private static readonly LazyRegex EntryRegex = new LazyRegex(@"^\s*(?<1>\S+)\s+(?<2>\S.*)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            RichDescription uninclusedDesc = null;
            if (string.Equals(config[nameof(IExistential.Exists)], "false", StringComparison.OrdinalIgnoreCase))
            {
                uninclusedDesc = new RichDescription("does not exist");
            }

            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure ",
                    new Hilite(config[nameof(HostsEntryConfiguration.HostName)]),
                    " hostsfile entry"
                ),
                uninclusedDesc ?? new RichDescription(
                    "is ",
                    new Hilite(config[nameof(HostsEntryConfiguration.IpAddress)])
                )
            );
        }

#if Otter
        public override async Task<PersistedConfiguration> CollectAsync(IOperationExecutionContext context)
        {
            string hostsPath;
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();
            if (fileOps is ILinuxFileOperationsExecuter)
            {
                hostsPath = "/etc/hosts";
            }
            else
            {
                var remoteMethod = context.Agent.GetService<IRemoteMethodExecuter>();
                hostsPath = await remoteMethod.InvokeFuncAsync(GetHostsFilePath).ConfigureAwait(false);
            }

            this.LogDebug("Hosts file is at " + hostsPath);

            var entries = (from l in await fileOps.ReadAllLinesAsync(hostsPath).ConfigureAwait(false)
                           where !string.IsNullOrWhiteSpace(l) && !CommentRegex.IsMatch(l)
                           let e = EntryRegex.Match(l)
                           where e.Success
                           select new HostsEntryConfiguration
                           {
                               Exists = true,
                               IpAddress = e.Groups[1].Value,
                               HostName = e.Groups[2].Value
                           }).ToLookup(e => e.HostName, StringComparer.OrdinalIgnoreCase);

            var matches = entries[this.Template.HostName];
            return matches.FirstOrDefault(e => string.Equals(e.IpAddress, this.Template.IpAddress, StringComparison.OrdinalIgnoreCase))
                ?? matches.FirstOrDefault()
                ?? new HostsEntryConfiguration
                {
                    Exists = false,
                    IpAddress = this.Template.IpAddress,
                    HostName = this.Template.HostName
                };
        }
#endif

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            string hostsPath;
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();
            if (fileOps is ILinuxFileOperationsExecuter)
            {
                hostsPath = "/etc/hosts";
            }
            else
            {
                var remoteMethod = context.Agent.GetService<IRemoteMethodExecuter>();
                hostsPath = await remoteMethod.InvokeFuncAsync(GetHostsFilePath).ConfigureAwait(false);
            }

            this.LogDebug("Hosts file is at " + hostsPath);

            var lines = (await fileOps.ReadAllLinesAsync(hostsPath).ConfigureAwait(false)).ToList();

            if (this.Template.Exists)
            {
                int lastLineIndex = -1;
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (CommentRegex.IsMatch(line))
                        continue;

                    var match = EntryRegex.Match(line);
                    if (match.Success)
                    {
                        lastLineIndex = i;
                        var ipAddress = match.Groups[1].Value;
                        var hostName = match.Groups[2].Value;
                        if (string.Equals(this.Template.HostName, hostName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.Equals(this.Template.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase))
                            {
                                this.LogInformation("Hosts entry already present.");
                                return;
                            }

                            this.LogInformation("Updating hosts entry...");
                            lines[i] = this.Template.IpAddress + "\t" + this.Template.HostName;
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    this.LogInformation("Adding hosts entry...");
                    lines.Insert(lastLineIndex + 1, this.Template.IpAddress + "\t" + this.Template.HostName);
                }
            }
            else
            {
                if (lines.RemoveAll(this.IsEntryToRemove) == 0)
                {
                    this.LogInformation("Entry is not present.");
                    return;
                }

                this.LogInformation("Removing hosts entry...");
            }

            this.LogDebug("Writing changes to hosts file...");
            await fileOps.WriteAllTextAsync(hostsPath, string.Join(fileOps.NewLine, lines)).ConfigureAwait(false);
            this.LogDebug("Hosts file saved.");
        }

        private static string GetHostsFilePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        }

        private bool IsEntryToRemove(string line)
        {
            if (CommentRegex.IsMatch(line))
                return false;

            var match = EntryRegex.Match(line);
            if (!match.Success)
                return false;

            var hostName = match.Groups[2].Value;
            return string.Equals(hostName, this.Template.HostName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
