using System.Runtime.CompilerServices;
using Inedo.Agents;
using Inedo.DependencyScan;
using Inedo.Extensibility.SecureResources;

namespace Inedo.Extensions.Operations.ProGet
{
    [Obsolete("Use ProGet::Scan instead.")]
    [Tag("proget")]
    [ScriptAlias("Record-Dependencies")]
    [ScriptNamespace(Namespaces.ProGet)]
    [DisplayName("Record Project Dependencies")]
    [Description("Scans for NuGet, npm, or PyPI package dependencies of a project and sends information about them to a ProGet instance.")]
    [Example(@"# Records NuGet dependencies for MyProject.csproj in the Libraries feed of the LocalProGet instance
ProGet::Record-Dependencies
(
    Project: MyProject.csproj,
    Resource: LocalProGet,
    Feed: Libraries,
    ConsumerVersion: $ReleaseNumber
);")]
    public sealed class RecordDependenciesOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("Project")]
        [DisplayName("Project path")]
        public string SourcePath { get; set; }
        [ScriptAlias("Resource")]
        [DisplayName("Secure resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<ProGetSecureResource>))]
        public string ResourceName { get; set; }
        [ScriptAlias("ProGetUrl")]
        [DisplayName("ProGet base URL")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use URL from secure resource")]
        public string ProGetUrl { get; set; }
        [Required]
        [ScriptAlias("Feed")]
        [DisplayName("Feed")]
        public string ProGetFeed { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ProjectType")]
        [DisplayName("Project type")]
        [DefaultValue(DependencyScannerType.Auto)]
        public DependencyScannerType ProjectType { get; set; }
        [ScriptAlias("ApiKey")]
        [DisplayName("API key")]
        [Category("Connection/Identity")]
        [FieldEditMode(FieldEditMode.Password)]
        [PlaceholderText("Use token from secure credentials")]
        public string ApiKey { get; set; }
        [ScriptAlias("Comments")]
        [PlaceholderText("none")]
        public string Comments { get; set; }

        [ScriptAlias("ConsumerFeed")]
        [DisplayName("Consumer feed")]
        [PlaceholderText("same as dependency feed")]
        public string ConsumerFeed { get; set; }
        [ScriptAlias("ConsumerName")]
        [DisplayName("Consumer name")]
        [PlaceholderText("project name")]
        public string ConsumerName { get; set; }
        [Required]
        [ScriptAlias("ConsumerVersion")]
        [DisplayName("Consumer version")]
        public string ConsumerVersion { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(this.ResourceName))
            {
                var resource = SecureResource.TryCreate(this.ResourceName, (IResourceResolutionContext)context);
                if (resource is ProGetSecureResource res)
                {
                    if (string.IsNullOrWhiteSpace(this.ProGetUrl))
                        this.ProGetUrl = res.ServerUrl;

                    if (res.GetCredentials((ICredentialResolutionContext)context) is Credentials.TokenCredentials token)
                    {
                        if (string.IsNullOrWhiteSpace(this.ApiKey))
                            this.ApiKey = AH.Unprotect(token.Token);
                    }
                }
                else
                {
                    this.LogError($"Specified resource \"{this.ResourceName}\" is not a ProGetSecureResource.");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(this.ProGetUrl))
            {
                this.LogError("ProGetUrl was not specified.");
                return;
            }

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();

            var sourcePath = context.ResolvePath(this.SourcePath);
            this.LogInformation($"Looking for dependencies in {sourcePath}...");
            var scanner = DependencyScanner.GetScanner(sourcePath, this.ProjectType, new RemoteFileSystem(fileOps));
            this.LogDebug($"Project type is {scanner.Type}.");
            var projects = await scanner.ResolveDependenciesAsync(cancellationToken: context.CancellationToken).ConfigureAwait(false);

            string name = null;
            string group = null;

            if (!string.IsNullOrWhiteSpace(this.ConsumerName))
                (name, group) = ParseName(this.ConsumerName);

            int totalDependencies = 0;

            foreach (var project in projects)
            {
                var dependents = new HashSet<DependencyPackage>();

                foreach (var package in project.Dependencies)
                {
                    totalDependencies++;
                    if (dependents.Add(package))
                        this.LogInformation($"Publishing consumer data for {package}...");
                }

                await DependencyPackage.PublishDependenciesAsync(
                    dependents,
                    this.ProGetUrl,
                    this.ProGetFeed,
                    new PackageConsumer
                    {
                        Feed = AH.CoalesceString(this.ProGetFeed, this.ConsumerFeed),
                        Name = AH.CoalesceString(name, project.Name),
                        Group = group,
                        Version = this.ConsumerVersion
                    },
                    this.ApiKey,
                    this.Comments
                );
            }

            this.LogInformation($"Recorded {totalDependencies} across {projects.Count} projects.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Record project dependencies for ",
                    new DirectoryHilite(config[nameof(SourcePath)])
                ),
                new RichDescription(
                    "and publish information to ",
                    new Hilite(AH.CoalesceString(config[nameof(ProGetUrl)], config[nameof(ResourceName)]))
                )
            );
        }

        private static (string name, string group) ParseName(string fullName)
        {
            var parts = fullName.Split(new[] { '/' }, 2);
            if (parts.Length > 1)
                return (parts[1], parts[0]);
            else
                return (fullName, null);
        }

        private sealed class RemoteFileSystem : ISourceFileSystem
        {
            private readonly IFileOperationsExecuter fileOps;

            public RemoteFileSystem(IFileOperationsExecuter fileOps) => this.fileOps = fileOps;

            public string Combine(string path1, string path2) => this.fileOps.CombinePath(path1, path2);
            public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken) => new(this.fileOps.FileExistsAsync(path));
            public async IAsyncEnumerable<SimpleFileInfo> FindFilesAsync(string path, string filter, bool recursive, [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var include = recursive ? this.fileOps.CombinePath("**", filter) : filter;
                var mask = new MaskingContext(new[] { include }, null);

                foreach (var info in await this.fileOps.GetFileSystemInfosAsync(path, mask))
                {
                    if (info is SlimFileInfo)
                        yield return new SimpleFileInfo(info.FullName, info.LastWriteTimeUtc);
                }
            }
            public string GetDirectoryName(string path) => PathEx.GetDirectoryName(path);
            public string GetFileName(string path) => PathEx.GetFileName(path);
            public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
            public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken) => this.fileOps.OpenFileAsync(path, FileMode.Open, FileAccess.Read);
        }
    }
}
