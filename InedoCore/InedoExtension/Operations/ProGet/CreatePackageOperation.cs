using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SuggestionProviders;
using Inedo.IO;
using Inedo.UPack;
using Inedo.UPack.Net;
using Inedo.UPack.Packaging;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [ScriptAlias("Create-Package")]
    [DisplayName("Create Package")]
    [Description("Creates a universal package from the specified directory.")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("proget")]
    public sealed class CreatePackageOperation : RemotePackageOperationBase
    {
        private string userName;
        private string password;
        private string feedUrl;

        [ScriptAlias("From")]
        [PlaceholderText("$WorkingDirectory")]
        [DisplayName("Source directory")]
        public string SourceDirectory { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Include")]
        [MaskingDescription]
        [PlaceholderText("* (top-level items)")]
        public IEnumerable<string> Includes { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("To")]
        [DisplayName("To")]
        [PlaceholderText("<Name>-<Version>.upack")]
        [Description("This may either be a file name or a directory. If the value ends with .upack, then this is treated as a file name. Otherwise, it is treated as an output directory into which the package file will be written.")]
        public string Output { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Overwrite")]
        public bool Overwrite { get; set; }

        [ScriptAlias("Group")]
        [DisplayName("Package group")]
        public string Group { get; set; }
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        public string Name { get; set; }
        [Required]
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        public string Version { get; set; }

        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [Category("Package Source (Preview)")]
        [SuggestableValue(typeof(PackageSourceSuggestionProvider))]
        [Description("When specified, the package will be uploaded to this package source and attached to the current build.")]
        public override string PackageSource { get; set; }

        [Category("Advanced")]
        [ScriptAlias("Metadata")]
        [DisplayName("Additional metadata")]
        [Description("Additional properties may be specified using map syntax. For example: %(description: my package description)")]
        public IReadOnlyDictionary<string, RuntimeValue> Metadata { get; set; }

        protected override Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            this.ValidateArguments();
            return base.BeforeRemoteExecuteAsync(context);
        }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var outputFileName = context.ResolvePath(this.Output);
            if (Directory.Exists(outputFileName) || !outputFileName.EndsWith(".upack", StringComparison.OrdinalIgnoreCase))
                outputFileName = Path.Combine(outputFileName, $"{this.Name}-{this.Version}.upack");

            var sourceDirectory = context.ResolvePath(this.SourceDirectory);

            this.LogDebug("Package file name: " + outputFileName);
            this.LogDebug("Source directory: " + sourceDirectory);

            if (!this.Overwrite && File.Exists(outputFileName))
            {
                this.LogError(outputFileName + " already exists and Overwrite is set to false.");
                return null;
            }

            var metadata = new UniversalPackageMetadata
            {
                Group = this.Group,
                Name = this.Name,
                Version = UniversalPackageVersion.Parse(this.Version)
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

            using (var package = new UniversalPackageBuilder(new FileStream(outputFileName, this.Overwrite ? FileMode.Create : FileMode.CreateNew), metadata))
            {
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

                this.LogDebug($"Adding {matches.Count} items to package...");

                await package.AddContentsAsync(sourceDirectory, string.Empty, mask.Recurse, matches.Contains, context.CancellationToken);

                this.LogInformation("Package created.");
            }

            // when package source is specified, upload it
            if (!string.IsNullOrWhiteSpace(this.PackageSource))
            {
                // start computing the hash in the background
                var computeHashTask = Task.Factory.StartNew(() => computePackageHash(outputFileName), TaskCreationOptions.LongRunning);

                this.LogDebug("Package source URL: " + this.feedUrl);
                this.LogInformation($"Uploading package to {this.PackageSource}...");

                using (var fileStream = FileEx.Open(outputFileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan | FileOptions.Asynchronous))
                {
                    var client = new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(this.feedUrl), this.userName, AH.CreateSecureString(this.password)));
                    await client.UploadPackageAsync(fileStream, context.CancellationToken);
                }

                this.LogDebug("Package uploaded.");

                this.LogDebug("Waiting for package hash to be computed...");
                var hash = await computeHashTask;
                this.LogDebug("Package SHA1: " + new HexString(hash));
                return hash;
            }

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
                switch (value.ValueType)
                {
                    case RuntimeValueType.Scalar:
                        return value.AsString();
                    case RuntimeValueType.Vector:
                        return value.AsEnumerable().Select(convert).ToArray();
                    case RuntimeValueType.Map:
                        return value.AsDictionary().ToDictionary(p => p.Key, p => convert(p.Value));
                    default:
                        throw new ArgumentException();
                }
            }

            byte[] computePackageHash(string fileName)
            {
                using (var fileStream = FileEx.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan))
                using (var sha1 = SHA1.Create())
                {
                    return sha1.ComputeHash(fileStream);
                }
            }
        }

        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            await base.AfterRemoteExecuteAsync(result);

            if (this.PackageManager != null && !string.IsNullOrWhiteSpace(this.PackageSource))
            {
                this.LogDebug("Attaching package to build...");
                await this.PackageManager.AttachPackageToBuildAsync(
                    new AttachedPackage(AttachedPackageType.Universal, GetFullPackageName(this.Group, this.Name), this.Version, (byte[])result, this.PackageSource),
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
                    new Hilite((config[nameof(Group)] + "/" + config[nameof(Name)]).Trim('/') + " " + config[nameof(Version)]),
                    " universal package"
                ),
                new RichDescription(
                    "from ",
                    new DirectoryHilite(config[nameof(SourceDirectory)])
                )
            );
        }

        private protected override void SetPackageSourceProperties(string userName, string password, string feedUrl)
        {
            this.userName = userName;
            this.password = password;
            this.feedUrl = feedUrl;
        }

        private void ValidateArguments()
        {
            if (string.IsNullOrEmpty(this.Name))
                throw new ExecutionFailureException("Missing \"Name\" argument.");

            if (string.IsNullOrEmpty(this.Version))
                throw new ExecutionFailureException("Missing \"Version\" argument.");

            if (!string.IsNullOrEmpty(this.Group) && !UniversalPackageId.IsValidGroup(this.Group))
                throw new ExecutionFailureException("Invalid package group specified.");

            if (!UniversalPackageId.IsValidName(this.Name))
                throw new ExecutionFailureException("Invalid package name specified.");

            if (UniversalPackageVersion.TryParse(this.Version) == null)
                throw new EncoderFallbackException("Specified package version is not a valid semantic version.");
        }
    }
}
