using System;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Diagnostics;
using Inedo.Documentation;

namespace Inedo.Extensions.Operations.Otter
{
    [DisplayName("Remediate Drift")]
    [Description("Checks configuration status and remediates drift in Otter.")]
    [ScriptAlias("Remediate-Drift")]
    [ScriptNamespace(Namespaces.Otter)]
    [Tag("otter")]
    public sealed class RemediateDriftOperation : ExecuteOperation, IHasCredentials<OtterCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [ScriptAlias("Server")]
        [DisplayName("Server name")]
        public string Server { get; set; }

        [ScriptAlias("Role")]
        [DisplayName("Role name")]
        public string Role { get; set; }

        [ScriptAlias("WaitForCompletion")]
        [DisplayName("Wait until remediated")]
        [DefaultValue(true)]
        public bool WaitForCompletion { get; set; } = true;

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
            string entityName;
            EntityType entityType;
            if (this.Server != null)
            {
                entityName = this.Server;
                entityType = EntityType.Server;
            }
            else if (this.Role != null)
            {
                entityName = this.Role;
                entityType = EntityType.Role;
            }
            else
            {
                this.LogError("A server or role name is required to remediate drift in Otter.");
                return;
            }

            string entityTypeAndName = $"{entityType.ToString().ToLowerInvariant()} '{entityName}'";

            this.LogInformation($"Remediating drift for {entityTypeAndName}...");
            
            var client = new OtterClient(this.Host, this.ApiKey, this, context.CancellationToken);

            try
            {
                this.LogDebug("Triggering configuration check...");
                await client.TriggerConfigurationCheckAsync(entityType, entityName).ConfigureAwait(false);

                this.LogDebug("Waiting a few seconds to get new configuration status...");
                await Task.Delay(3 * 1000, context.CancellationToken).ConfigureAwait(false);

                var configuration = await client.GetConfigurationStatusAsync(entityType, entityName).ConfigureAwait(false);

                if (string.Equals(configuration.Status, "error", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogError($"Configuration status is 'error': {configuration.ErrorText ?? ""}; will not attempt to remediate.");
                    return;
                }
                else if (string.Equals(configuration.Status, "current", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogInformation("Configuration status is already 'current'.");
                    return;
                }
                else if (string.Equals(configuration.Status, "disabled", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogWarning("Configuration status is 'disabled', will not attempt to remediate.");
                    return;
                }
                else if (new[] { "unknown", "pendingRemediation", "executing" }.Contains(configuration.Status, StringComparer.OrdinalIgnoreCase))
                {
                    this.LogInformation($"Configuration status is '{configuration.Status}', will not attempt to remediate.");
                    return;
                }
                else if (string.Equals(configuration.Status, "drifted", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogInformation("Configuration status is 'drifted', triggering remediation job...");
                    string jobToken = await client.TriggerRemediationJobAsync(entityType, entityName).ConfigureAwait(false);

                    this.LogInformation("Remediation job triggered successfully.");
                    this.LogDebug("Job token: " + jobToken);

                    if (!this.WaitForCompletion)
                    {
                        this.LogDebug("Operation specified false for wait until remediated.");
                        return;
                    }

                    this.LogInformation("Waiting for remediation job to complete...");

                    RemediationStatus status;
                    do
                    {
                        await Task.Delay(3 * 1000, context.CancellationToken).ConfigureAwait(false);
                        status = await client.GetRemediationJobStatusAsync(jobToken).ConfigureAwait(false);
                    }
                    while (status == RemediationStatus.Pending || status == RemediationStatus.Running);

                    if (status == RemediationStatus.Completed)
                    {
                        this.LogInformation("Drift remediation complete.");
                    }
                    else
                    {
                        this.LogError($"Drift remediation execution was '{status.ToString().ToLowerInvariant()}', see Otter for more details.");
                    }
                }
                else
                {
                    this.LogError($"Unexpected configuration status '{configuration.Status}' returned from Otter.");
                }
            }
            catch (OtterException ex)
            {
                this.LogError(ex.FullMessage);
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Remediate drift for ", new Hilite(AH.CoalesceString(config[nameof(this.Server)], config[nameof(this.Role)])))
            );
        }
    }
}
