using System.Collections;
using Inedo.Agents;
using Inedo.ExecutionEngine;
using Inedo.Extensions.PackageSources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.UPack.Packaging;

namespace Inedo.Extensions.Operations.ProGet.Packages
{
    [DisplayName("Query Package")]
    [ScriptAlias("Query-Package")]
    [ScriptNamespace(Namespaces.UPack, PreferUnqualified = false)]
    [Description("Tests whether a universal package exists and optionally extracts its metadata.")]
    [Tag("upack")]
    [Example(@"
# test whether a package exists in a feed and capture its metadata
Query-Package
(
    Credentials: MyExternalFeed,
    PackageName: Group/Package,
    Exists => $exists,
    Metadata => %packageData
);

if $exists
{
    Log-Debug 'Package $(%packageData.name) exists. Latest version is $(%packageData.version).';
}
")]
    [Example(@"
# extract metadata from a locally-stored package file
Query-Package
(
    PackageFile: C:\MyPackages\Package-1.0.0.upack,
    Metadata => %packageData
);

Log-Debug 'Package name is $(%packageData.name).';
")]
    public sealed class QueryUniversalPackageOperation : ExecuteOperation, IFeedPackageConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(UniversalPackageSourceSuggestionProvider))]
        public string PackageSourceName { get; set; }

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }

        [Required]
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("latest")]
        [DefaultValue("latest")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Required]
        [ScriptAlias("NewVersion")]
        [DisplayName("New version")]
        public string NewVersion { get; set; }

        [ScriptAlias("Reason")]
        [DisplayName("Reason")]
        [PlaceholderText("Unspecified")]
        public string Reason { get; set; }

        [ScriptAlias("PackageFile")]
        [DisplayName("Package file")]
        [Category("Connection/Identity")]
        [Description("When specified, FeedUrl, UserName, Password, PackageName, and PackageVersion are ignored.")]
        public string PackageFile { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Feed")]
        [DisplayName("Feed name")]
        [PlaceholderText("Use Feed from package source")]
        public string FeedName { get; set; }

        [ScriptAlias("EndpointUrl")]
        [DisplayName("API endpoint URL")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use URL from package source")]
        public string ApiUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access this feed.")]
        [PlaceholderText("Use user name from package source")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [PlaceholderText("Use password from package source")]
        [Description("The password of a user in ProGet that can access this feed.")]
        public string Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiKey")]
        [DisplayName("ProGet API Key")]
        [PlaceholderText("Use API Key from package source")]
        [Description("An API Key that can access this feed.")]
        public string ApiKey { get; set; }

        [Output]
        [Category("Output Variables")]
        [ScriptAlias("Exists")]
        [DisplayName("Package exists")]
        [Description("When specified, this string variable will be set to \"true\" if the package exists or \"false\" if it does not.")]
        [PlaceholderText("e.g. $PackageExists")]
        public bool Exists { get; set; }

        [Output]
        [Category("Output Variables")]
        [ScriptAlias("Metadata")]
        [DisplayName("Package metadata")]
        [Description("When specified, this map variable will be assigned containing all of the package's metadata. If the package does not exist this value is not defined.")]
        public IDictionary<string, RuntimeValue> Metadata { get; set; }

        [Undisclosed]
        [ScriptAlias("FeedUrl")]
        public string FeedUrl { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrEmpty(this.PackageFile))
            {
                if (!string.IsNullOrWhiteSpace(this.FeedUrl))
                    this.LogWarning("FeedUrl is ignored when PackageFile is specified.");

                if (!string.IsNullOrWhiteSpace(this.PackageName))
                    this.LogError("Name is ignored when PackageFile is specified.");

                if (!string.IsNullOrWhiteSpace(this.PackageVersion))
                    this.LogError("Version is ignored when PackageFile is specified.");

                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
                var fullPath = context.ResolvePath(this.PackageFile);

                this.LogInformation($"Reading {fullPath}...");

                if (await fileOps.FileExistsAsync(fullPath))
                {
                    this.LogDebug("Package file exists; reading metadata...");
                    UniversalPackageMetadata metadata;
                    try
                    {
                        using var stream = await fileOps.OpenFileAsync(fullPath, FileMode.Open, FileAccess.Read);
                        using var packageFile = new UniversalPackage(stream);
                        metadata = packageFile.GetFullMetadata();
                    }
                    catch (Exception ex)
                    {
                        this.LogError("Error reading package: " + ex);
                        return;
                    }

                    this.Exists = true;
                    this.Metadata = this.Convert(metadata).AsDictionary();
                }
                else
                {
                    this.LogInformation("Package file not found.");
                    this.Exists = false;
                }

                return;
            }

            var client = this.TryCreateProGetFeedClient(context);
            var package = await client.FindPackageVersionAsync(this);
            if (package != null)
            {
                this.LogInformation($"Package {package.FullName} {package.Version} found.");
                this.Exists = true;
                this.Metadata = this.Convert(package.AllProperties).AsDictionary();
            }
            else
            {

                this.LogInformation($"Package {package.FullName} {package.Version} not found.");
                this.Exists = false;
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            RichDescription desc;
            var fileName = config[nameof(PackageFile)];
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                desc = new RichDescription(
                    "Determine if ",
                    new DirectoryHilite(fileName),
                    " exists as a valid universal package"
                );
            }
            else
            {
                desc = new RichDescription(
                    "Determine if ",
                    new Hilite(config[nameof(PackageName)])
                );

                var version = config[nameof(PackageVersion)];

                if (!string.IsNullOrWhiteSpace(version))
                {
                    desc.AppendContent(
                        " ",
                        new Hilite(version)
                    );
                }

                desc.AppendContent(
                    " exists on ",
                    new Hilite(config[nameof(FeedUrl)])
                );
            }

            return new ExtendedRichDescription(
                desc,
                new RichDescription(
                    "and set variables ",
                    new ListHilite(new[] { config[nameof(Exists)], config[nameof(Metadata)] }.Select(v => v.ToString()).Where(v => !string.IsNullOrEmpty(v)))
                )
            );
        }

        private RuntimeValue Convert(object obj)
        {
            try
            {
                return obj switch
                {
                    string s => s,
                    IReadOnlyDictionary<string, object> d => new RuntimeValue(d.ToDictionary(p => p.Key, p => this.Convert(p.Value))),
                    IDictionary<string, object> d => new RuntimeValue(d.ToDictionary(p => p.Key, p => this.Convert(p.Value))),
                    IEnumerable e => new RuntimeValue(e.Cast<object>().Select(this.Convert).ToList()),
                    _ => obj?.ToString()
                };
            }
            catch (Exception ex)
            {
                this.LogWarning($"Error converting {obj} to an OtterScript value: " + ex.Message);
                return "<ERROR>";
            }
        }
    }
}
