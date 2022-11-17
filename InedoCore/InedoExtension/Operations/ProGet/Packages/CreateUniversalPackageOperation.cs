using System.Text;
using Inedo.ExecutionEngine;
using Inedo.Extensions.PackageSources;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.UPack;
using Inedo.UPack.Packaging;

namespace Inedo.Extensions.Operations.ProGet.Packages
{
    [ScriptAlias("Create-Package")]
    [DisplayName("Create Universal Package")]
    [Description("Creates a universal package from the specified directory and publishes to a feed.")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("proget")]
    [Example(@"ProGet::Create-Package
(
    Name: MyAppPackage,
    Version: 3.4.2,

    From: $WorkingDirectory,
    PushTo: MyPackageSource
);
")]
    public sealed class CreateUniversalPackageOperation : RemoteExecuteOperation, IFeedPackageConfiguration
    {
        private IPackageManager packageManager;
        private string originalPackageSourceName;

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }
        [Required]
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }
        [ScriptAlias("From")]
        [PlaceholderText("$WorkingDirectory")]
        [DisplayName("Source directory")]
        public string SourceDirectory { get; set; }
        [ScriptAlias("PushTo")]
        [ScriptAlias("PackageSource")]
        [DisplayName("To package source")]
        [SuggestableValue(typeof(UniversalPackageSourceSuggestionProvider))]
        public string PackageSourceName { get; set; }

        [Category("Packaging options")]
        [ScriptAlias("To")]
        [DisplayName("Package file name")]
        [PlaceholderText("<Name>-<Version>.upack")]
        [Description("This may either be a file name or a directory. If the value ends with .upack, then this is treated as a file name. Otherwise, it is treated as an output directory into which the package file will be written.")]
        public string Output { get; set; }
        [Category("Packaging options")]
        [ScriptAlias("Include")]
        [MaskingDescription]
        [PlaceholderText("** (all items in directory)")]
        [DisplayName("Include files")]
        public IEnumerable<string> Includes { get; set; } = new[] { "**" };
        [Category("Packaging options")]
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        [DisplayName("Exclude files")]
        [PlaceholderText("include all files")]
        public IEnumerable<string> Excludes { get; set; }
        [Category("Packaging options")]
        [ScriptAlias("Metadata")]
        [DisplayName("Additional metadata")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Description("Additional properties may be specified using map syntax. For example: %(description: my package description)")]
        public IReadOnlyDictionary<string, RuntimeValue> Metadata { get; set; }
        [Category("Packaging options")]
        [ScriptAlias("Overwrite")]
        [DisplayName("Overwrite existing package")]
        public bool Overwrite { get; set; }

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

        [Undisclosed]
        [ScriptAlias("Group")]
        public string PackageGroup { get; set; }
        [Undisclosed]
        [ScriptAlias("FeedUrl")]
        public string FeedUrl { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (UniversalPackageId.Parse(this.PackageName) == null)
                throw new ExecutionFailureException("Invalid package name specified.");

            if (UniversalPackageVersion.TryParse(this.PackageVersion) == null)
                throw new EncoderFallbackException("Specified package version is not a valid semantic version.");

            this.originalPackageSourceName = this.PackageSourceName;

            if (!string.IsNullOrEmpty(this.PackageGroup))
                this.PackageName = this.PackageGroup + "/" + this.PackageName;

            await this.EnsureProGetConnectionInfoAsync(context, context.CancellationToken);
            await this.ResolveAttachedPackageAsync(context);
            this.packageManager = await context.TryGetServiceAsync<IPackageManager>();
        }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var outputFileName = context.ResolvePath(this.Output);
            if (Directory.Exists(outputFileName) || !outputFileName.EndsWith(".upack", StringComparison.OrdinalIgnoreCase))
                outputFileName = Path.Combine(outputFileName, $"{this.PackageName}-{this.PackageVersion}.upack");

            var sourceDirectory = context.ResolvePath(this.SourceDirectory);

            this.LogDebug("Package file name: " + outputFileName);
            this.LogDebug("Source directory: " + sourceDirectory);

            if (!this.Overwrite && File.Exists(outputFileName))
            {
                this.LogError(outputFileName + " already exists and Overwrite is set to false.");
                return null;
            }

            var packageName = UniversalPackageId.Parse(this.PackageName);
            var metadata = new UniversalPackageMetadata
            {
                Group = packageName.Group,
                Name = packageName.Name,
                Version = UniversalPackageVersion.Parse(this.PackageVersion)
            };

            if (this.Metadata != null && this.Metadata.Count > 0)
            {
                this.LogDebug("Additional metadata is specified.");
                foreach (var m in this.Metadata)
                {
                    if (isIgnored(m.Key))
                        continue;

                    this.LogDebug($"Setting \"{m.Key}\" = {m.Value}...");
                    metadata[m.Key] = convert(m.Value);
                }
            }

            // Ensure output directory exists
            DirectoryEx.Create(PathEx.GetDirectoryName(outputFileName));

            if (!DirectoryEx.Exists(sourceDirectory))
            {
                this.LogWarning($"Source directory {sourceDirectory} does not exist.");
                return null;
            }

            var mask = new MaskingContext(this.Includes, this.Excludes);

            var matches = DirectoryEx.GetFileSystemInfos(sourceDirectory, mask).Select(i => i.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (matches.Count == 0)
            {
                this.LogWarning($"Nothing was captured in {sourceDirectory} using the specified mask.");
                return null;
            }

            using (var package = new UniversalPackageBuilder(new FileStream(outputFileName, this.Overwrite ? FileMode.Create : FileMode.CreateNew), metadata))
            {
                this.LogDebug($"Adding {matches.Count} items to package...");

                await package.AddContentsAsync(sourceDirectory, string.Empty, mask.Recurse, matches.Contains, context.CancellationToken);

                this.LogInformation("Package created.");
            }

            // when package source is specified, upload it
            if (!string.IsNullOrWhiteSpace(this.ApiUrl))
                return await new ProGetFeedClient(this, this).UploadPackageAndComputeHashAsync(outputFileName, context.CancellationToken);

            return null;

            bool isIgnored(string propertyName)
            {
                bool ignored = string.Equals(propertyName, "group", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(propertyName, "name", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(propertyName, "version", StringComparison.OrdinalIgnoreCase);

                if (ignored)
                    this.LogWarning($"Property \"{propertyName}\" specified in \"Metadata\" argument will be ignored.");

                return ignored;
            }

            object convert(RuntimeValue value)
            {
                return value.ValueType switch
                {
                    RuntimeValueType.Scalar => value.AsString(),
                    RuntimeValueType.Vector => value.AsEnumerable().Select(convert).ToArray(),
                    RuntimeValueType.Map => value.AsDictionary().ToDictionary(p => p.Key, p => convert(p.Value)),
                    _ => throw new ArgumentOutOfRangeException(nameof(value))
                };
            }
        }

        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            await base.AfterRemoteExecuteAsync(result);

            if (this.packageManager != null && !string.IsNullOrWhiteSpace(this.originalPackageSourceName))
            {
                this.LogDebug("Attaching package to build...");
                await this.packageManager.AttachPackageToBuildAsync(
                    new AttachedPackage(AttachedPackageType.Universal, this.PackageName, this.PackageVersion, (byte[])result, this.originalPackageSourceName),
                    default
                );
                this.LogDebug("Package attached.");
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Create ",
                    new Hilite(config[nameof(PackageName)] + " " + config[nameof(PackageVersion)]),
                    " universal package"
                ),
                new RichDescription(
                    "from ",
                    new DirectoryHilite(config[nameof(SourceDirectory)])
                )
            );
        }
    }
}
