using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.Operations.Files
{
    [Description("Concatenates files on a server.")]
    [ScriptAlias("Concatenate-Files")]
    [ScriptNamespace(Namespaces.Files, PreferUnqualified = true)]
    [Example(@"
# concatenates all SQL files in the working directory into a 
# single file, each script separated by a GO statement
Concatenate-Files(
    File: all.sql,
    Include: *.sql,
    Separator: >>
GO
>>
);
")]
    public sealed class ConcatenateFilesOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("File")]
        [DisplayName("Output file")]
        public string OutputFile { get; set; }
        [ScriptAlias("Directory")]
        [DisplayName("Directory")]
        [PlaceholderText("$WorkingDirectory")]
        public string SourceDirectory { get; set; }
        [ScriptAlias("Include")]
        [PlaceholderText("* (top-level items)")]
        [MaskingDescription]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("Encoding")]
        [DisplayName("Encoding")]
        [PlaceholderText("utf8")]
        public string OutputFileEncoding { get; set; }
        [ScriptAlias("Separator")]
        [DisplayName("Separator")]
        public string ContentSeparationText { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Concatenate ",
                    new MaskHilite(config[nameof(this.Includes)], config[nameof(this.Excludes)])
                ),
                new RichDescription(
                    "in ",
                    new DirectoryHilite(config[nameof(this.SourceDirectory)]),
                    " into ",
                    new DirectoryHilite(config[nameof(this.OutputFile)])
                )
            );
        }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var sourceDirectory = context.ResolvePath(this.SourceDirectory);

            this.LogInformation($"Concatenating files in {sourceDirectory}...");

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();

            var files = (await fileOps.GetFileSystemInfosAsync(sourceDirectory, new MaskingContext(this.Includes, this.Excludes)).ConfigureAwait(false))
                .OfType<SlimFileInfo>()
                .ToList();

            if (files.Count == 0)
            {
                this.LogWarning("No files to concatenate.");
                return;
            }

            var encoding = this.GetOrGuessEncoding();

            var outputFileName = context.ResolvePath(this.OutputFile);

            this.LogInformation($"Concatenating {files.Count} files into {PathEx.GetFileName(outputFileName)}...");
            this.LogDebug($"Output file {outputFileName}, encoding: {encoding.EncodingName}");

            var outputDirectory = PathEx.GetDirectoryName(outputFileName);
            if (!string.IsNullOrEmpty(outputDirectory))
                await fileOps.CreateDirectoryAsync(outputDirectory).ConfigureAwait(false);

            using (var outputStream = await fileOps.OpenFileAsync(outputFileName, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
            using (var outputWriter = new StreamWriter(outputStream, encoding))
            {
                bool first = true;

                foreach (var file in files)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    if (!first)
                        await outputWriter.WriteAsync(this.ContentSeparationText ?? "").ConfigureAwait(false);
                    else
                        first = false;

                    using (var inputStream = await fileOps.OpenFileAsync(file.FullName, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
                    using (var inputReader = new StreamReader(inputStream))
                    {
                        await inputReader.CopyToAsync(outputWriter, 4096, context.CancellationToken).ConfigureAwait(false);
                    }
                }
            }

            this.LogInformation(outputFileName + " file created.");
        }

        private Encoding GetOrGuessEncoding()
        {
            if (!string.IsNullOrEmpty(this.OutputFileEncoding))
            {
                if (this.OutputFileEncoding.Equals("ansi", StringComparison.OrdinalIgnoreCase))
                    return Encoding.Default;
                else if (string.Equals(this.OutputFileEncoding, "utf8", StringComparison.OrdinalIgnoreCase))
                    return InedoLib.UTF8Encoding;
                else
                    return Encoding.GetEncoding(this.OutputFileEncoding);
            }
            else
            {
                return InedoLib.UTF8Encoding;
            }
        }
    }
}
