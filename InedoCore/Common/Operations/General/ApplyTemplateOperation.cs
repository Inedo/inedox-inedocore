using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensions.SuggestionProviders;
using Inedo.IO;
#if Otter
using Inedo.Otter.Documentation;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.RaftRepositories;
using Inedo.Otter.Extensions;
using Inedo.Otter.Web.Controls;
using Inedo.Otter.Web.Controls.Plans;
#elif BuildMaster
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.RaftRepositories;
using Inedo.BuildMaster.Web;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Plans;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensibility.Web;
using Inedo.Extensibility.Web.Plans.ArgumentEditors;
#endif

namespace Inedo.Extensions.Operations.General
{
    [DefaultProperty(nameof(Asset))]
    [Description("Applies full template transformation on a literal, a file, or a template asset.")]
    [DisplayName("Apply Template")]
    [ScriptAlias("Apply-Template")]
    [Tag(Tags.Variables)]
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
        [SuggestibleValue(typeof(TextTemplateRaftSuggestionProvider))]
        [PlaceholderText("not using an asset")]
        public string Asset { get; set; }

        [Output]
        [ScriptAlias("OutputVariable")]
        [DisplayName("Store to variable")]
        [PlaceholderText("do not store in variable")]
        public string OutputVariable { get; set; }
        [FilePathEditor]
        [ScriptAlias("OutputFile")]
        [DisplayName("Output file")]
        [PlaceholderText("do not write to file")]
        public string OutputFile { get; set; }

        [ScriptAlias("Literal")]
        [DisableVariableExpansion]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Category("Source")]
        public string Literal { get; set; }
        [FilePathEditor]
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
        public TemplateNewLineMode NewLineMode { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var template = await getTemplateAsync().ConfigureAwait(false);

            this.LogDebug("Detecting newlines in source template...");
            var sourceNewLines = template.Contains("\r\n") ? TemplateNewLineMode.Windows
                : template.Contains("\n") ? TemplateNewLineMode.Linux
                : TemplateNewLineMode.Auto;

            this.LogDebug("Applying template...");
            var result = await context.ApplyTextTemplateAsync(template, this.AdditionalVariables).ConfigureAwait(false);
            this.LogInformation("Template applied.");

            if (this.NewLineMode == TemplateNewLineMode.Windows)
                result = result.Replace("\n", "\r\n");

            if (!string.IsNullOrWhiteSpace(this.OutputFile))
            {
                var path = context.ResolvePath(this.OutputFile);
                this.LogDebug($"Writing output to {path}...");
                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
                if (this.NewLineMode == TemplateNewLineMode.Auto)
                    result = result.Replace("\n", fileOps.NewLine);

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
                    string templateName;
                    string raftName;

                    var templateNameParts = this.Asset.Split(new[] { "::" }, 2, StringSplitOptions.None);
                    if (templateNameParts.Length == 2)
                    {
                        raftName = templateNameParts[0];
                        templateName = templateNameParts[1];
                    }
                    else
                    {
#if BuildMaster
                        raftName = (await context.TryEvaluateFunctionAsync(RuntimeVariableName.Parse("$ApplicationName"), new List<RuntimeValue>()).ConfigureAwait(false))?.AsString()
                            ?? RaftRepository.DefaultName;
#elif Otter
                        raftName = RaftRepository.DefaultName;
#endif
                        templateName = templateNameParts[0];
                    }
#if Hedgehog
#warning FIX
                    throw new NotImplementedException("Hedgehog needs basic raft support");
#else
                    using (var raft = RaftRepository.OpenRaft(raftName))
                    {
                        if (raft == null)
                            throw new ExecutionFailureException("Raft not found.");

                        using (var stream = raft.OpenRaftItem(RaftItemType.TextTemplate, templateName, FileMode.Open, FileAccess.Read))
                        {
                            if (stream == null)
                                throw new ExecutionFailureException($"Template \"{templateName}\" not found in raft.");

                            this.LogDebug("Loading template from raft...");
                            using (var reader = new StreamReader(stream, InedoLib.UTF8Encoding))
                            {
                                return await reader.ReadToEndAsync().ConfigureAwait(false);
                            }
                        }
                    }
#endif
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
        Linux
    }
}
