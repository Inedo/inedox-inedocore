using System;
using System.ComponentModel;
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
    public sealed class DriftStatusPromotionRequirement : PromotionRequirement
    {
        [Persistent]
        [DisplayName("Credentials:")]
        [Required]
        [SuggestibleValue(typeof(OtterCredentialSuggestionProvider))]
        public string CredentialName { get; set; }
        [Persistent]
        [DisplayName("Server name:")]
        [SuggestibleValue(typeof(OtterServerNameSuggestionProvider))]
        public string Server { get; set; }
        [Persistent]
        [DisplayName("Role name:")]
        [SuggestibleValue(typeof(OtterRoleNameSuggestionProvider))]
        public string Role { get; set; }
        [Persistent]
        public DriftStatus Status { get; set; }

        public override async Task<PromotionRequirementStatus> GetStatusAsync(PromotionContext context)
        {
            var entity = InfrastructureEntity.Create(serverName: this.Server, roleName: this.Role);
            if (entity == null)
                return new PromotionRequirementStatus(PromotionRequirementState.NotApplicable, "A server or role must be specified to determine drift status.");
            
            var credentials = ResourceCredentials.Create<OtterCredentials>(this.CredentialName);

            var client = new OtterClient(credentials.Host, credentials.ApiKey);
            try
            {
                await client.TriggerConfigurationCheckAsync(entity).ConfigureAwait(false);
                await Task.Delay(2 * 1000).ConfigureAwait(false);
                var config = await client.GetConfigurationStatusAsync(entity).ConfigureAwait(false);

                if (string.Equals(config.Status, this.Status.ToString(), StringComparison.OrdinalIgnoreCase))
                    return new PromotionRequirementStatus(PromotionRequirementState.Met, $"{entity} status is {config.Status}.");
                else
                    return new PromotionRequirementStatus(PromotionRequirementState.NotMet, $"{entity} status is {config.Status}, must be {this.Status.ToString().ToLowerInvariant()}.");
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
