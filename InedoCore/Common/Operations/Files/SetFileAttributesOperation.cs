using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
#if BuildMaster
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Documentation;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.Files
{
    [Serializable]
    [DisplayName("Set File Attributes")]
    [Description("Sets or clears attributes on matching files.")]
    [Tag("files")]
    [ScriptAlias("Set-FileAttributes")]
    [ScriptNamespace("Files", PreferUnqualified = true)]
    public sealed class SetFileAttributesOperation : RemoteExecuteOperation
    {
        [ScriptAlias("Directory")]
        public string SourceDirectory { get; set; }
        [ScriptAlias("Include")]
        [PlaceholderText("* (top-level items)")]
#if Hedgehog
        [MaskingDescription]
#else
        [Description(CommonDescriptions.MaskingHelp)]
#endif
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
#if Hedgehog
        [MaskingDescription]
#else
        [Description(CommonDescriptions.MaskingHelp)]
#endif
        public IEnumerable<string> Excludes { get; set; }

        [ScriptAlias("ReadOnly")]
        [DisplayName("Read only")]
        public bool? ReadOnly { get; set; }
        [ScriptAlias("Hidden")]
        public bool? Hidden { get; set; }
        [ScriptAlias("System")]
        public bool? System { get; set; }

        [ScriptAlias("Verbose")]
        [DisplayName("Verbose")]
        public bool VerboseLogging { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var readOnly = TryParse(config[nameof(ReadOnly)]);
            var hidden = TryParse(config[nameof(Hidden)]);
            var system = TryParse(config[nameof(System)]);

            var set = new List<string>();
            var clear = new List<string>();

            if (readOnly == true)
                set.Add("read-only");
            if (hidden == true)
                set.Add("hidden");
            if (system == true)
                set.Add("system");

            if (readOnly == false)
                clear.Add("read-only");
            if (hidden == false)
                clear.Add("hidden");
            if (system == false)
                clear.Add("system");

            RichDescription shortDesc;
            if (set.Count > 0)
            {
                shortDesc = new RichDescription(
                    "Set ",
                    new ListHilite(set),
                    set.Count > 1 ? " flags" : " flag"
                );

                if (clear.Count > 0)
                {
                    shortDesc.AppendContent(
                        ", and clear ",
                        new ListHilite(clear),
                        clear.Count > 1 ? " flags" : " flag"
                    );
                }
            }
            else if (clear.Count > 0)
            {
                shortDesc = new RichDescription(
                    "Clear ",
                    new ListHilite(clear),
                    clear.Count > 1 ? " flags" : " flag"
                );
            }
            else
            {
                shortDesc = new RichDescription("Change flags");
            }

            return new ExtendedRichDescription(
                shortDesc,
                new RichDescription(
                    "on ",
                    new MaskHilite(config[nameof(Includes)], config[nameof(Excludes)])
                )
            );
        }

        protected override Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            if (this.ReadOnly ?? this.Hidden ?? this.System == null)
            {
                this.LogWarning("No file attributes have been specified.");
                return Complete;
            }

            var mask = new MaskingContext(this.Includes, this.Excludes);
            var sourcePath = context.ResolvePath(this.SourceDirectory);
            this.LogInformation($"Getting list of files in {sourcePath} matching {mask}...");
            var matches = DirectoryEx.GetFileSystemInfos(sourcePath, mask)
                .OfType<SlimFileInfo>()
                .ToList();

            if (matches.Count == 0)
            {
                this.LogWarning("No files match the specified mask.");
                return Complete;
            }

            FileAttributes attributesToChange = 0;
            if (this.ReadOnly.HasValue)
                attributesToChange |= FileAttributes.ReadOnly;
            if (this.Hidden.HasValue)
                attributesToChange |= FileAttributes.Hidden;
            if (this.System.HasValue)
                attributesToChange |= FileAttributes.System;

            FileAttributes attributeValues = 0;
            if (this.ReadOnly.GetValueOrDefault())
                attributeValues |= FileAttributes.ReadOnly;
            if (this.Hidden.GetValueOrDefault())
                attributeValues |= FileAttributes.Hidden;
            if (this.System.GetValueOrDefault())
                attributeValues |= FileAttributes.System;

            if (this.VerboseLogging)
            {
                this.LogDebug("Attributes to change: " + attributesToChange);
                this.LogDebug("Attribute values: " + attributeValues);
            }

            this.LogDebug($"Found {matches.Count} matching files.");
            this.LogInformation("Applying attributes...");
            foreach (var file in matches)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var attributes = file.Attributes;

                if (((attributes & attributesToChange) ^ attributeValues) != 0)
                {
                    attributes &= ~attributesToChange;
                    attributes |= attributeValues;
                    if (this.VerboseLogging)
                        this.LogDebug("Changing " + file.FullName + "...");
                    FileEx.SetAttributes(file.FullName, attributes);
                }
            }

            this.LogInformation("Attributes applied.");

            return Complete;
        }

        private static bool? TryParse(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            bool b;
            if (!bool.TryParse(value, out b))
                return true;

            return b;
        }
    }
}
