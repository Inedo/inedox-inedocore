using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;

#nullable enable

namespace Inedo.Extensions.Operations.ProGet.Packages;

[Tag("proget")]
[ScriptAlias("Repackage")]
[ScriptAlias("Repack-Package", Obsolete = true)]
[DisplayName("Repackage")]
[ScriptNamespace(Namespaces.ProGet)]
[Description("Connects to ProGet to repackage an unstable (pre-release) package into a new package with the same contents.")]
public sealed class RepackageOperation : ExecuteOperation, IFeedPackageConfiguration
{
    [ScriptAlias("PackageSource")]
    [ScriptAlias("From", Obsolete = true)]
    [DisplayName("Package source")]
    [SuggestableValue(typeof(RepackPromoteSourceSuggestionProvider))]
    public string? PackageSourceName { get; set; }

    [Required]
    [ScriptAlias("Name")]
    [DisplayName("Package name")]
    [SuggestableValue(typeof(PackageNameSuggestionProvider))]
    public string? PackageName { get; set; }

    [ScriptAlias("Version")]
    [DisplayName("Package version")]
    [PlaceholderText("\"attached\" (BuildMaster only; otherwise required)")]
    [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
    public string? PackageVersion { get; set; }

    [Required]
    [ScriptAlias("NewVersion")]
    [DisplayName("New version")]
    public string? NewVersion { get; set; }

    [ScriptAlias("Reason")]
    [DisplayName("Reason")]
    [PlaceholderText("Unspecified")]
    public string? Reason { get; set; }

    [ScriptAlias("ToFeed")]
    [DisplayName("To feed")]
    [PlaceholderText("keep in same feed")]
    public string? ToFeed { get; set; }

    [Category("Connection/Identity")]
    [ScriptAlias("Feed")]
    [DisplayName("Feed name")]
    [PlaceholderText("Use Feed from package source")]
    public string? FeedName { get; set; }

    [ScriptAlias("EndpointUrl")]
    [DisplayName("API endpoint URL")]
    [Category("Connection/Identity")]
    [PlaceholderText("Use URL from package source")]
    public string? ApiUrl { get; set; }

    [Category("Connection/Identity")]
    [ScriptAlias("UserName")]
    [DisplayName("ProGet user name")]
    [Description("The name of a user in ProGet that can access this feed.")]
    [PlaceholderText("Use user name from package source")]
    public string? UserName { get; set; }

    [Category("Connection/Identity")]
    [ScriptAlias("Password")]
    [DisplayName("ProGet password")]
    [PlaceholderText("Use password from package source")]
    [Description("The password of a user in ProGet that can access this feed.")]
    public string? Password { get; set; }

    [Category("Connection/Identity")]
    [ScriptAlias("ApiKey")]
    [DisplayName("ProGet API Key")]
    [PlaceholderText("Use API Key from package source")]
    [Description("An API Key that can access this feed.")]
    public string? ApiKey { get; set; }

    [Undisclosed]
    [ScriptAlias("Group")]
    public string? PackageGroup { get; set; }
    [Undisclosed]
    [ScriptAlias("FeedUrl")]
    public string? FeedUrl { get; set; }

    public override async Task ExecuteAsync(IOperationExecutionContext context)
    {
        if (!string.IsNullOrEmpty(this.PackageGroup))
            this.PackageName = this.PackageGroup + "/" + this.PackageName;

        await this.EnsureProGetConnectionInfoAsync(context, context.CancellationToken);

        await this.ResolveAttachedPackageAsync(context);
        var packageManager = await context.TryGetServiceAsync<IPackageManager>();

        this.LogInformation($"Repackaging {this.PackageName} {this.PackageVersion} to {this.NewVersion} on {this.PackageSourceName} ({this.FeedName} feed)...");

        if (string.Equals(this.PackageVersion, this.NewVersion, StringComparison.OrdinalIgnoreCase))
        {
            this.LogWarning("\"Version\" and \"NewVersion\" are the same; nothing to do.");
            return;
        }

        var client = new ProGetApiClient(this, this);

        await client.RepackageAsync(this.PackageName!, this.PackageVersion!, this.NewVersion!, this.Reason, this.ToFeed, context.CancellationToken);

        if (!string.IsNullOrWhiteSpace(this.PackageSourceName))
        {
            var packageType = AttachedPackageType.Universal;

            foreach (var p in await packageManager.GetBuildPackagesAsync(context.CancellationToken))
            {
                if (p.Active && string.Equals(p.Name, this.PackageName, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Version, this.PackageVersion, StringComparison.OrdinalIgnoreCase))
                {
                    packageType = p.PackageType;
                    await packageManager.DeactivatePackageAsync(p.Name, p.Version, p.PackageSource);
                }
            }

            await packageManager.AttachPackageToBuildAsync(
                new AttachedPackage(packageType, this.PackageName, this.NewVersion, null, AH.NullIf((string?)this.PackageSourceName, string.Empty)),
                context.CancellationToken
            );
        }
    }

    protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
    {
        return new ExtendedRichDescription(
            new RichDescription(
                "Repackage ",
                new Hilite(config[nameof(PackageName)]),
                " ",
                new Hilite(config[nameof(PackageVersion)]),
                " to ",
                new Hilite(config[nameof(NewVersion)])
            ),
            new RichDescription(
                "on ",
                new Hilite(config[nameof(PackageSourceName)])
            )
        );
    }
}
