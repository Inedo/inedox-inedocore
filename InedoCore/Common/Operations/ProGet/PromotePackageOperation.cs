using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.SuggestionProviders;

#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensions.Credentials;
using Inedo.Otter.Web.Controls;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    //[DisplayName("Promote Package")]
    //[Description("Promotes a ProGet package to a different feed.")]
    //[ScriptAlias("Promote-Package")]
    //[ScriptNamespace(Namespaces.ProGet)]
    //[Tag("proget")]
    //public sealed class PromotePackageOperation : ExecuteOperation, IHasCredentials<ProGetApiKeyCredentials>
    //{
    //    [ScriptAlias("Credentials")]
    //    [DisplayName("Credentials")]
    //    public string CredentialName { get; set; }

    //    [Required]
    //    [ScriptAlias("Name")]
    //    [DisplayName("Package name")]
    //    public string Name { get; set; }

    //    [Required]
    //    [ScriptAlias("Version")]
    //    [SuggestibleValue(typeof(PackageVersionSuggestionProvider))]
    //    public string Version { get; set; }
        
    //    [Required]
    //    [ScriptAlias("FromFeed")]
    //    [DisplayName("From feed name")]
    //    [SuggestibleValue(typeof(FeedNameSuggestionProvider))]
    //    public string FromFeedName { get; set; }

    //    [Required]
    //    [ScriptAlias("ToFeed")]
    //    [DisplayName("To feed name")]
    //    [SuggestibleValue(typeof(FeedNameSuggestionProvider))]
    //    public string ToFeedName { get; set; }

    //    [ScriptAlias("Comments")]
    //    [DisplayName("Comments")]
    //    public string Comments { get; set; }

    //    [Category("Connection/Identity")]
    //    [ScriptAlias("Server")]
    //    [DisplayName("ProGet server URL")]
    //    [PlaceholderText("Use server URL from credential")]
    //    [MappedCredential(nameof(ProGetApiKeyCredentials.Host))]
    //    public string Server { get; set; }

    //    [Category("Connection/Identity")]
    //    [ScriptAlias("UserName")]
    //    [DisplayName("ProGet API key")]
    //    [PlaceholderText("Use API key from credential")]
    //    [MappedCredential(nameof(ProGetApiKeyCredentials.ApiKey))]
    //    public SecureString ApiKey { get; set; }

    //    [Category("Connection/Identity")]
    //    [ScriptAlias("UserName")]
    //    [DisplayName("ProGet user name")]
    //    [PlaceholderText("Use user name from credential")]
    //    [MappedCredential(nameof(ProGetApiKeyCredentials.UserName))]
    //    public string UserName { get; set; }

    //    [Category("Connection/Identity")]
    //    [ScriptAlias("Password")]
    //    [DisplayName("ProGet password")]
    //    [PlaceholderText("Use password from credential")]
    //    [MappedCredential(nameof(ProGetApiKeyCredentials.Password))]
    //    public string Password { get; set; }

    //    public override async Task ExecuteAsync(IOperationExecutionContext context)
    //    {
    //        var client = new ProGetClient(this.Server, this.FromFeedName, this.UserName, this.Password, this);

    //        try
    //        {
    //            var id = PackageName.Parse(this.Name);

    //            this.LogInformation($"Promoting package '{id}' from '{this.FromFeedName}' to '{this.ToFeedName}'...");

    //            await client.PromotePackageAsync(this.ApiKey, id, this.Version, this.FromFeedName, this.ToFeedName, this.Comments).ConfigureAwait(false);
    //        }
    //        catch (ProGetException ex)
    //        {
    //            this.LogError(ex.FullMessage);
    //            return;
    //        }

    //        this.LogInformation("Package promoted.");
    //    }

    //    protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
    //    {
    //        return new ExtendedRichDescription(
    //            new RichDescription(
    //                "Promote ", 
    //                new Hilite(config[nameof(this.Name)]), 
    //                " from ", 
    //                new Hilite(config[nameof(this.FromFeedName)]), 
    //                " to ", 
    //                new Hilite(config[nameof(this.ToFeedName)])
    //            )
    //        );
    //    }
    //}
}
