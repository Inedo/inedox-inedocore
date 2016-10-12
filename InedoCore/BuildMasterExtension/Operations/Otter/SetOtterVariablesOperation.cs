﻿using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.SuggestionProviders;

namespace Inedo.Extensions.Operations.Otter
{
    [DisplayName("Set Variable Value in Otter")]
    [Description("Creates or assigns a configuration variable in Otter.")]
    [ScriptAlias("Set-Variable")]
    [ScriptNamespace(Namespaces.Otter)]
    [Tag("otter")]
    [Tag(Tags.Variables)]
    [Example(@"
# sets the variable for the hdars-web-1k-tokyo server to the name of the current application
Otter::Set-Variable
(
    Credentials: ProductionOtter,
    Server: hdars-web-1k-tokyo,
    Name: LatestDeployedApplication,
    Value: $ApplicationName,
    Sensitive: false
);")]
    [Note("If multiple entity scopes are provided, the variable will be multi-scoped. If no entity scope is provided, a global variable will be set.")]
    public sealed class SetOtterVariablesOperation : ExecuteOperation, IHasCredentials<OtterCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [ScriptAlias("Name")]
        [DisplayName("Variable name")]
        [Required]
        public string Name { get; set; }

        [ScriptAlias("Value")]
        [DisplayName("Value")]
        [Required]
        public string Value { get; set; }

        [ScriptAlias("Server")]
        [DisplayName("Server name")]
        [SuggestibleValue(typeof(OtterServerNameSuggestionProvider))]
        public string Server { get; set; }

        [ScriptAlias("Role")]
        [DisplayName("Role name")]
        [SuggestibleValue(typeof(OtterRoleNameSuggestionProvider))]
        public string Role { get; set; }

        [ScriptAlias("Environment")]
        [DisplayName("Environment name")]
        [SuggestibleValue(typeof(OtterEnvironmentNameSuggestionProvider))]
        public string Environment { get; set; }

        [Category("Connection")]
        [ScriptAlias("Host")]
        [DisplayName("Otter server URL")]
        [PlaceholderText("Use URL from credentials")]
        [MappedCredential(nameof(OtterCredentials.Host))]
        public string Host { get; set; }
        [Category("Connection")]
        [ScriptAlias("ApiKey")]
        [DisplayName("API key")]
        [PlaceholderText("Use API key from credentials")]
        [MappedCredential(nameof(OtterCredentials.ApiKey))]
        public SecureString ApiKey { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            var variable = new ScopedVariableJsonModel
            {
                Name = this.Name,
                Value = this.Value,
                Environment = this.Environment,
                Server = this.Server,
                ServerRole = this.Role
            };

            var client = new OtterClient(this.Host, this.ApiKey, this, context.CancellationToken);
            try
            {
                await client.SetVariableAsync(variable).ConfigureAwait(false);
            }
            catch (OtterException ex)
            {
                this.LogError(ex.FullMessage);
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Set ", new Hilite(config[nameof(this.Name)]), " = ", new Hilite(config[nameof(this.Value)]), " in Otter")
            );
        }
    }
}
