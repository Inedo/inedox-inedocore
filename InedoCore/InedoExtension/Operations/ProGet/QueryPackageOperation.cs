using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.UPack;
using Inedo.UPack.Net;
using Inedo.UPack.Packaging;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Operations.ProGet
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
    public sealed class QueryPackageOperation : ExecuteOperation
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From package source")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public string PackageSource { get; set; }

        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }

        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("latest")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Output]
        [ScriptAlias("Exists")]
        [DisplayName("Package exists")]
        [Description("When specified, this string variable will be set to \"true\" if the package exists or \"false\" if it does not.")]
        public bool Exists { get; set; }

        [ScriptAlias("PackageFile")]
        [DisplayName("Package file")]
        [Category("Advanced")]
        [Description("When specified, FeedUrl, UserName, Password, PackageName, and PackageVersion are ignored.")]
        public string PackageFile { get; set; }

        [ScriptAlias("FeedUrl")]
        [DisplayName("Feed URL")]
        [Category("Connection/Identity")]
        [PlaceholderText("url from package source")]
        public string FeedUrl { get; set; }

        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [Category("Connection/Identity")]
        [PlaceholderText("user name from package source's credentials")]
        public string UserName { get; set; }

        [ScriptAlias("Password")]
        [Category("Connection/Identity")]
        [PlaceholderText("password from package source's credentials")]
        public SecureString Password { get; set; }

        [Output]
        [Category("Advanced")]
        [ScriptAlias("Metadata")]
        [DisplayName("Package metadata")]
        [Description("When specified, this map variable will be assigned containing all of the package's metadata. If the package does not exist this value is not defined.")]
        public IDictionary<string, RuntimeValue> Metadata { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrEmpty(this.PackageSource))
            {
                var resource = SecureResource.TryCreate(this.PackageSource, (IResourceResolutionContext)context) as UniversalPackageSource;
                if (resource == null)
                {
                    this.LogError($"Package Source \"{this.PackageSource}\" was not found.");
                    return;
                }
                this.FeedUrl = resource.ApiEndpointUrl;

                var creds = resource.GetCredentials((ICredentialResolutionContext)context);
                if (creds is UsernamePasswordCredentials upc)
                {
                    this.LogDebug($"Using \"{resource.CredentialName}\" credential (UserName=\"{upc.UserName}\").");
                    this.UserName = upc.UserName;
                    this.Password = upc.Password;
                }
                else if (creds is TokenCredentials tc)
                {
                    this.LogDebug($"Using \"{resource.CredentialName}\" credential (api key).");
                    this.UserName = "api";
                    this.Password = tc.Token;
                }
            }

            if (string.IsNullOrWhiteSpace(this.PackageFile))
            {
                if (string.IsNullOrWhiteSpace(this.FeedUrl))
                {
                    this.LogError("FeedUrl is required if PackageFile is not specified.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(this.PackageName))
                {
                    this.LogError("Name is required if PackageFile is not specified.");
                    return;
                }

                var endpoint = new UniversalFeedEndpoint(new Uri(this.FeedUrl), this.UserName, this.Password);

                this.LogInformation($"Getting package information for {this.PackageName} from {endpoint}...");
                var client = new UniversalFeedClient(endpoint);
                var versions = await client.ListPackageVersionsAsync(UniversalPackageId.Parse(this.PackageName));
                this.LogDebug($"Server return info for {versions.Count} packages.");

                RemoteUniversalPackageVersion package;
                if (!string.IsNullOrWhiteSpace(this.PackageVersion))
                {
                    this.LogDebug($"Checking for {this.PackageVersion} in result set...");
                    var parsedVersion = UniversalPackageVersion.Parse(this.PackageVersion);
                    package = versions.FirstOrDefault(p => p.Version == parsedVersion);
                    if (package != null)
                        this.LogInformation($"Package {this.PackageName} {this.PackageVersion} found.");
                    else
                        this.LogInformation($"Package {this.PackageName} {this.PackageVersion} not found.");
                }
                else
                {
                    if (versions.Count > 0)
                    {
                        this.LogDebug($"Determining latest version of {this.PackageName}...");
                        package = versions.Aggregate((p1, p2) => p1.Version >= p2.Version ? p1 : p2);
                        this.LogInformation($"Latest version of {this.PackageName} is {package.Version}.");
                    }
                    else
                    {
                        this.LogInformation($"Package {this.PackageName} not found.");
                        package = null;
                    }
                }

                if (package != null)
                {
                    this.Exists = true;
                    this.Metadata = this.Convert(package.AllProperties).AsDictionary();
                }
                else
                {
                    this.Exists = false;
                }
            }
            else
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
                        using (var stream = await fileOps.OpenFileAsync(fullPath, FileMode.Open, FileAccess.Read))
                        using (var package = new UniversalPackage(stream))
                        {
                            metadata = package.GetFullMetadata();
                        }
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
