﻿using System.Text.RegularExpressions;
using Inedo.Agents;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensions.SuggestionProviders;

namespace Inedo.Extensions.Operations.General
{
    [DefaultProperty(nameof(Asset))]
    [Description("Applies full template transformation on a literal, a file, or a template asset.")]
    [ScriptAlias("Apply-Template")]
    [Example(@"
# applies the a literal template and stores the result in $text
Apply-Template
(
    Literal: >>Hello from $ServerName!
<% if $IsSimulation { %> This is a simulation run. <% } else { %> This is not a simulation run. <% } %>
Thanks,
$MyName
>>,
    OutputVariable => $text,
    AdditionalVariables: %(MyName: Steve)
);
")]
    [Example(@"
# applies the hdars template and stores the result in $text
Apply-Template hdars
(
    OutputVariable => $text
);
")]
    [Note("When reading from or writing to a file, there must be a valid server context.")]
    public sealed partial class ApplyTemplateOperation : ExecuteOperation
    {
        [ScriptAlias("Asset")]
        [SuggestableValue(typeof(TextTemplateRaftSuggestionProvider))]
        [PlaceholderText("not using an asset")]
        public string Asset { get; set; }

        [Output]
        [ScriptAlias("OutputVariable")]
        [DisplayName("Store to variable")]
        [PlaceholderText("do not store in variable")]
        public string OutputVariable { get; set; }
        [ScriptAlias("OutputFile")]
        [DisplayName("Output file")]
        [PlaceholderText("do not write to file")]
        public string OutputFile { get; set; }

        [ScriptAlias("Literal")]
        [DisableVariableExpansion]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Category("Source")]
        public string Literal { get; set; }
        [ScriptAlias("InputFile")]
        [DisplayName("Input file")]
        [Category("Source")]
        public string InputFile { get; set; }

        [ScriptAlias("AdditionalVariables")]
        [DisplayName("Additional variables")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Category("Advanced")]
        public IReadOnlyDictionary<string, RuntimeValue> AdditionalVariables { get; set; }
        [ScriptAlias("NewLines")]
        [DisplayName("New lines")]
        [Category("Advanced")]
        [DefaultValue(TemplateNewLineMode.Auto)]
        [Description("""
            Specifies how to handle new lines in the output. When set to "Auto", it will attempt to match
            the format to the operating system of the server in context. Setting "Windows" or "Linux" will
            force newlines to match each format respectively. Setting "None" will pass through all new line
            characters unmodified.
            """)]
        public TemplateNewLineMode NewLineMode { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var template = await getTemplateAsync().ConfigureAwait(false);

            this.LogDebug("Applying template...");
            var result = await context.ApplyTextTemplateAsync(template, this.AdditionalVariables).ConfigureAwait(false);
            this.LogInformation("Template applied.");

            if (this.NewLineMode == TemplateNewLineMode.Windows)
                result = Regex.Replace(result, @"\r?\n", "\r\n");
            else if (this.NewLineMode == TemplateNewLineMode.Linux)
                result = Regex.Replace(result, @"\r?\n", "\n");

            if (!string.IsNullOrWhiteSpace(this.OutputFile))
            {
                var path = context.ResolvePath(this.OutputFile);
                this.LogDebug($"Writing output to {path}...");
                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
                if (this.NewLineMode == TemplateNewLineMode.Auto)
                    result = Regex.Replace(result, @"\r?\n", fileOps.NewLine);

                await fileOps.CreateDirectoryAsync(PathEx.GetDirectoryName(path));
                await fileOps.WriteAllTextAsync(path, result).ConfigureAwait(false);
            }

            this.LogDebug("Setting output variable...");
            this.OutputVariable = result;

            async Task<string> getTemplateAsync()
            {
                if (!string.IsNullOrEmpty(this.Literal))
                {
                    this.LogDebug("Applying literal template: " + this.Literal);
                    return this.Literal;
                }

                if (!string.IsNullOrEmpty(this.InputFile))
                {
                    var path = context.ResolvePath(this.InputFile);
                    this.LogDebug($"Using file {path} as template...");
                    var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
                    if (!await fileOps.FileExistsAsync(path).ConfigureAwait(false))
                        throw new ExecutionFailureException("Template file not found.");

                    return await fileOps.ReadAllTextAsync(path).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(this.Asset))
                {
                    var raftItem = SDK.GetRaftItem(RaftItemType.TextFile, this.Asset, context);
                    if (raftItem == null)
                        throw new ExecutionFailureException($"Template \"{this.Asset}\" not found.");

                    return raftItem.Content;
                }

                this.LogWarning("No template specified. Setting output to empty string.");
                return string.Empty;
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(getFirstPart(), getSecondPart());

            RichDescription getFirstPart()
            {
                if (!string.IsNullOrEmpty(config[nameof(Literal)]))
                {
                    return new RichDescription(
                        "Apply template starting with ",
                        new Hilite(config[nameof(Literal)])
                    );
                }

                if (!string.IsNullOrEmpty(config[nameof(InputFile)]))
                {
                    return new RichDescription(
                        "Apply ",
                        new DirectoryHilite(config[nameof(InputFile)]),
                        " template file"
                    );
                }

                return new RichDescription(
                    "Apply ",
                    new Hilite((string)config[nameof(Asset)] ?? "<unspecified>"),
                    " template"
                );
            }

            RichDescription getSecondPart()
            {
                var outputs = new List<object>(2);
                var outVar = config.OutArguments.GetValueOrDefault(nameof(OutputVariable))?.ToString();
                if (outVar != null)
                    outputs.Add(new Hilite(outVar));

                var outFile = (string)config[nameof(OutputFile)];
                if (!string.IsNullOrEmpty(outFile))
                    outputs.Add(new DirectoryHilite(outFile));

                if (outputs.Count == 0)
                    return new RichDescription();
                else if (outputs.Count == 1)
                    return new RichDescription("storing results in ", outputs[0]);
                else
                    return new RichDescription("storing results in ", outputs[0], " and ", outputs[1]);
            }
        }
    }

    public enum TemplateNewLineMode
    {
        Auto,
        Windows,
        Linux,
        None
    }
}
