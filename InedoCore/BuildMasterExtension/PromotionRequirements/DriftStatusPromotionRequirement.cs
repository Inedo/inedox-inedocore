using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.PromotionRequirements;
using Inedo.BuildMaster.Web.Controls;
using Inedo.Documentation;
using Inedo.Extensions.Operations.Otter;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Serialization;

namespace Inedo.Extensions.PromotionRequirements
{
    public enum DriftStatus { Current, Drifted }

    [DisplayName("Drift Status")]
    [Description("Verifies that a specified Otter server or role is in a specified drift status.")]
    public sealed class DriftStatusPromotionRequirement : PromotionRequirementBase
    {
        [Persistent]
        [DisplayName("Credentials:")]
        [SuggestibleValue(typeof(OtterCredentialSuggestionProvider))]
        public string CredentialName { get; set; }
        [Persistent]
        [DisplayName("Server name:")]
        public string Server { get; set; }
        [Persistent]
        [DisplayName("Role name:")]
        public string Role { get; set; }
        [Persistent]
        public DriftStatus Status { get; set; }

        public override async Task<PromotionRequirementStatus> GetStatusAsync(PromotionContext context)
        {
            string entityName;
            EntityType entityType;
            if (!string.IsNullOrEmpty(this.Server))
            {
                entityName = this.Server;
                entityType = EntityType.Server;
            }
            else if (!string.IsNullOrEmpty(this.Role))
            {
                entityName = this.Role;
                entityType = EntityType.Role;
            }
            else
            {
                return new PromotionRequirementStatus(PromotionRequirementState.NotApplicable, "A server or role must be specified to determine drift status.");
            }

            string entityTypeAndName = $"{entityType.ToString()} {entityName}";

            var credentials = ResourceCredentials.Create<OtterCredentials>(this.CredentialName);

            var client = new OtterClient(credentials.Host, credentials.ApiKey, null, CancellationToken.None);
            try
            {
                await client.TriggerConfigurationCheckAsync(entityType, entityName).ConfigureAwait(false);
                await Task.Delay(2 * 1000).ConfigureAwait(false);
                var config = await client.GetConfigurationStatusAsync(entityType, entityName).ConfigureAwait(false);

                if (string.Equals(config.Status, this.Status.ToString(), StringComparison.OrdinalIgnoreCase))
                    return new PromotionRequirementStatus(PromotionRequirementState.Met, $"{entityTypeAndName} status is {config.Status}.");
                else
                    return new PromotionRequirementStatus(PromotionRequirementState.NotMet, $"{entityTypeAndName} status is {config.Status}, must be {this.Status.ToString().ToLower()}.");
            }
            catch (OtterException ex)
            {
                return new PromotionRequirementStatus(PromotionRequirementState.NotMet, ex.FullMessage);
            }
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription(
                "Ensure drift status is ", 
                new Hilite(this.Status.ToString()),
                " for ",
                !string.IsNullOrEmpty(this.Server) ? "server " : "role ",
                new Hilite(AH.CoalesceString(this.Server, this.Role))
            );
        }
    }
}
