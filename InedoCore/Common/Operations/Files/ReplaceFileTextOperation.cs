using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Agents;
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
    [DisplayName("Search/Replace File Contents")]
    [Description("Searches a text file for a specified string and replaces it.")]
    [Tag(Tags.Files)]
    [ScriptAlias("Replace-Text")]
    [ScriptNamespace("Files", PreferUnqualified = true)]
    public sealed class ReplaceFileTextOperation : ExecuteOperation
    {
        [ScriptAlias("Include")]
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
        [ScriptAlias("Directory")]
        public string SourceDirectory { get; set; }
        [Required]
        [ScriptAlias("SearchText")]
        [DisplayName("Search text")]
        public string SearchText { get; set; }
        [ScriptAlias("ReplaceWith")]
        [DisplayName("Replace with")]
        public string ReplaceText { get; set; }
        [ScriptAlias("Regex")]
        [DisplayName("Use regex")]
        public bool UseRegex { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            Regex regex;
            try
            {
                regex = this.UseRegex ? new Regex(this.SearchText, RegexOptions.Compiled) : null;
            }
            catch (ArgumentException ex)
            {
                this.LogError("Invalid regular expression: " + ex.Message);
                return;
            }

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var path = context.ResolvePath(this.SourceDirectory);

            this.LogDebug("Search string: " + this.SearchText + (this.UseRegex ? "(regex)" : string.Empty));
            this.LogDebug("Replace with: " + this.ReplaceText);

            if (!await fileOps.DirectoryExistsAsync(path).ConfigureAwait(false))
            {
                this.LogWarning($"Directory \"{path}\" does not exist.");
                return;
            }

            var files = (await fileOps.GetFileSystemInfosAsync(path, new MaskingContext(this.Includes, this.Excludes)).ConfigureAwait(false))
                .OfType<SlimFileInfo>()
                .ToList();

            if (files.Count == 0)
            {
                this.LogWarning($"The mask matched no files in \"{path}\".");
                return;
            }

            foreach (var file in files)
            {
                this.LogDebug($"Looking for search string in {file.FullName}...");

                var sourceText = await fileOps.ReadAllTextAsync(file.FullName).ConfigureAwait(false);
                string replacedText;
                if (regex != null)
                    replacedText = regex.Replace(sourceText, this.ReplaceText);
                else
                    replacedText = sourceText.Replace(this.SearchText, this.ReplaceText);

                if (sourceText != replacedText)
                {
                    this.LogDebug($"Found search string in {file.FullName}; updating file...");

                    if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                        await fileOps.SetAttributesAsync(file.FullName, file.Attributes & ~FileAttributes.ReadOnly).ConfigureAwait(false);

                    await fileOps.WriteAllTextAsync(file.FullName, replacedText).ConfigureAwait(false);
                    this.LogInformation(file.FullName + " saved.");
                }
            }

            this.LogInformation("Replacement complete.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Replace Text in ",
                    new MaskHilite(config[nameof(this.Includes)], config[nameof(this.Excludes)])
                ),
                new RichDescription(
                    "matching ",
                    new Hilite(config[nameof(this.SearchText)]),
                    " with ",
                    new Hilite(config[nameof(this.ReplaceText)]),
                    " in ",
                    new DirectoryHilite(config[nameof(this.SourceDirectory)])
                )
            );
        }
    }
}
