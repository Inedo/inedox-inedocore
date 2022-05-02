﻿using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.Otter
{
    [DisplayName("Set Variable Value in Otter")]
    [Description("Creates or assigns a configuration variable in Otter.")]
    [ScriptAlias("Set-Variable")]
    [ScriptNamespace(Namespaces.Otter)]
    [Tag("otter")]
    [Tag("variables")]
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
    public sealed class SetOtterVariablesOperation : ExecuteOperation
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
        public string Server { get; set; }

        [ScriptAlias("Role")]
        [DisplayName("Role name")]
        public string Role { get; set; }

        [ScriptAlias("Environment")]
        [DisplayName("Environment name")]
        public string Environment { get; set; }

        [ScriptAlias("Sensitive")]
        [DisplayName("Sensitive")]
        public bool Sensitive { get; set; }


        [Category("Connection")]
        [ScriptAlias("Host")]
        [DisplayName("Otter server URL")]
        [PlaceholderText("Use URL from credentials")]
        public string Host { get; set; }
        [Category("Connection")]
        [ScriptAlias("ApiKey")]
        [DisplayName("API key")]
        [PlaceholderText("Use API key from credentials")]
        public SecureString ApiKey { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogInformation($"Setting Otter variable {this.Name}...");

            var variable = new ScopedVariableJsonModel
            {
                Name = this.Name,
                Value = this.Value,
                Environment = this.Environment,
                Server = this.Server,
                ServerRole = this.Role,
                Sensitive = this.Sensitive
            };

            var client = OtterClient.Create(this.Host, this.ApiKey, this, context.CancellationToken);
            try
            {
                if (string.IsNullOrEmpty(variable.Server) && string.IsNullOrEmpty(variable.ServerRole) && string.IsNullOrEmpty(variable.Environment))
                {
                    this.LogDebug($"Setting Otter global variable '{variable.Name}' = '{variable.Value}'...");
                    await client.SetGlobalVariableAsync(variable.Name, variable.Value).ConfigureAwait(false);
                }
                else
                {
                    this.LogDebug($"Setting Otter scoped variable '{variable.Name}' = '{variable.Value}'...");
                    await client.SetSingleVariableAsync(variable).ConfigureAwait(false);
                }

                this.LogInformation("Otter variable value set.");
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
