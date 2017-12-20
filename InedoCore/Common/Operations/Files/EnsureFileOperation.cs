using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.Configurations.Files;
using Inedo.IO;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;
using CollectContext = Inedo.Otter.Extensibility.Operations.IOperationExecutionContext;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using CollectContext = Inedo.Extensibility.Operations.IOperationCollectionContext;
#endif

namespace Inedo.Extensions.Operations.Files
{
    [DisplayName("Ensure File")]
    [Description("Ensures the existence of a file on a server.")]
    [ScriptAlias("Ensure-File")]
    [ScriptNamespace(Namespaces.Files, PreferUnqualified = true)]
    [Tag("files")]
    [Example(@"
# ensures the otter.txt file exists on the server and is marked readonly
Ensure-File(
    Name: E:\Docs\otter.txt,
    Text: >>
Otter is a common name for a carnivorous mammal in the subfamily Lutrinae. 
Help, I'm trapped in an Otter documentation factory! The 13 extant otter species are all semiaquatic, 
aquatic or marine, with diets based on fish and invertebrates.
>>,
    ReadOnly: true
);
")]
    public sealed class EnsureFileOperation : EnsureOperation<FileConfiguration>
    {
#if Otter || Hedgehog
        public override async Task<PersistedConfiguration> CollectAsync(CollectContext context)
        {
            var path = this.Template.Name;

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            this.LogDebug($"Looking for {path}...");
            if (!await fileOps.FileExistsAsync(path))
            {
                this.LogDebug("File does not exist.");
                return new FileConfiguration
                {
                    Name = path,
                    Exists = false
                };
            }

            this.LogDebug("File exists, loading from disk...");

            var file = await fileOps.GetFileInfoAsync(path);

            var config = new FileConfiguration { Name = this.Template.Name };

            if (this.Template.Attributes.HasValue)
                config.Attributes = file.Attributes;

            if (this.Template.Contents != null)
                config.Contents = await fileOps.ReadFileBytesAsync(path);
            else if (this.Template.TextContents != null)
                config.TextContents = await fileOps.ReadAllTextAsync(path);

            if (this.Template.IsReadOnly.HasValue)
                config.IsReadOnly = file.IsReadOnly;

            if (this.Template.LastWriteTimeUtc != null)
                config.LastWriteTimeUtc = file.LastWriteTimeUtc;

            this.LogDebug("File configuration loaded.");
            return config;
        }
#endif

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var path = this.Template.Name;

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            this.LogDebug($"Looking for {path}...");
            bool fileExists = await fileOps.FileExistsAsync(path).ConfigureAwait(false);

            if (!this.Template.Exists)
            {
                if (fileExists)
                {
                    this.LogDebug($"File {path} exists, deleting...");
                    await fileOps.DeleteFileAsync(path).ConfigureAwait(false);
                    this.LogInformation("File deleted.");
                }
                else
                {
                    this.LogDebug("File does not exist.");
                }

                return;
            }

            if (!fileExists)
            {
                this.LogDebug("File does not exist, creating...");
                var directoryName = PathEx.GetDirectoryName(path);
                if (!await fileOps.DirectoryExistsAsync(directoryName))
                {
                    this.LogDebug("Directory does not exist, creating...");
                    await fileOps.CreateDirectoryAsync(directoryName).ConfigureAwait(false);
                }

                await fileOps.WriteFileBytesAsync(path, new byte[0]).ConfigureAwait(false);
            }

            this.LogDebug("Setting properties...");
            if (this.Template.Attributes.HasValue)
            {
                this.LogDebug("Setting Attributes...");
                await fileOps.SetAttributesAsync(path, this.Template.Attributes.Value);
            }

            if (this.Template.Contents != null)
            {
                this.LogDebug("Writing out Contents...");
                await fileOps.WriteFileBytesAsync(path, this.Template.Contents);
            }
            else if (this.Template.TextContents != null)
            {
                this.LogDebug("Writing out TextContents...");
                await fileOps.WriteAllTextAsync(path, this.Template.TextContents);
            }

            if (this.Template.IsReadOnly != null)
            {
                this.LogDebug("Setting IsReadOnly...");
                if (this.Template.IsReadOnly == true)
                    await fileOps.AddAttributesAsync(path, FileAttributes.ReadOnly);
                else
                    await fileOps.RemoveAttributesAsync(path, FileAttributes.ReadOnly);
            }

            if (this.Template.LastWriteTimeUtc.HasValue)
            {
                this.LogDebug("Setting LastWriteTimeUtc...");
                await fileOps.SetLastWriteTimeAsync(path, this.Template.LastWriteTimeUtc.Value);
            }

            this.LogInformation($"File {path} configured.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var desc = new ExtendedRichDescription(
                new RichDescription(
                    "Ensure file ",
                    new DirectoryHilite(config[nameof(FileConfiguration.Name)])
                ),
                new RichDescription()
            );

            if (string.Equals(config[nameof(FileConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase))
            {
                desc.LongDescription.AppendContent("does not exist");
                return desc;
            }
            char semi = ' ';
            if (!string.IsNullOrEmpty(config[nameof(FileConfiguration.Attributes)]))
            {
                desc.LongDescription.AppendContent(
                    " with attributes: ", config[nameof(FileConfiguration.Attributes)]
                );
            }
            if (!string.IsNullOrEmpty(config[nameof(FileConfiguration.LastWriteTimeUtc)]))
            {
                desc.LongDescription.AppendContent(
                    semi + " with last modified date: ", config[nameof(FileConfiguration.LastWriteTimeUtc)]
                );
            }

            return desc;
        }
    }
}
