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
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.Operations.Files
{
    [DisplayName("Search/Replace File Contents")]
    [Description("Searches a text file for a specified string and replaces it.")]
    [Tag("files")]
    [ScriptAlias("Replace-Text")]
    [ScriptNamespace("Files", PreferUnqualified = true)]
    [Example(@"# Replaces the product version in an example vdproj file with the BuildMaster release number
Create-File example.vdproj
(
	Text: >>""Product""
		{
			""ProductVersion"" = ""8:1.0.0""
		}>>
);

Replace-Text
(
	Include: **.vdproj,
	SearchText: '""ProductVersion"" = ""(?<1>[0-9]+)\:[0-9]+\.[0-9]+\.[0-9]+""',
	ReplaceWith: '""ProductVersion"" = ""`$1:$ReleaseNumber""',
	Regex: true
);

Log-Information `$FileContents = $FileContents(example.vdproj);  # ""ProductVersion"" = ""8:1.2.3""
")]
    public sealed class ReplaceFileTextOperation : ExecuteOperation
    {
        [ScriptAlias("Include")]
        [MaskingDescription]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("Directory")]
        public string SourceDirectory { get; set; }
        [Required]
        [ScriptAlias("SearchText")]
        [DisplayName("Search text")]
        public string SearchText { get; set; }
        [ScriptAlias("ReplaceWith")]
        [DisplayName("Replace with")]
        [Description("The text that replaces all occurrences of the search string found in matching files. If the Regex property is true, this property can replace matched group numbers as well, e.g.<br/><pre>" +
            "Example line: Version: 0.0.0-b05f2ad" + "\r\n" +
            "SearchText: Version: (\\d+\\.\\d+\\.\\d+)(?&lt;commitId&gt;-\\w+)?" + "\r\n" +
            "ReplaceWith: Version: $ReleaseNumber`${commitId} (was `$1)" + "\r\n" +
            "Example result: Version: 1.2.3-b05f2ad (was 0.0.0)" +
            "</pre><br/>" +
            "<i>Note the backtick characters (`) used to escape the $ in the replacement text, which otherwise would be interpreted as OtterScript variables.</i>")]
        public string ReplaceText { get; set; }
        [ScriptAlias("Regex")]
        [DisplayName("Use regex")]
        [Description("Determines whether the search text should be interpreted as <a target=\"_blank\" href=\"https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference\">.NET Regular Expression syntax</a>.")]
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
