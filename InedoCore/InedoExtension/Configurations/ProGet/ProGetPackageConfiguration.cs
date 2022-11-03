using Inedo.Extensibility.Configurations;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.Serialization;

namespace Inedo.Extensions.Configurations.ProGet
{
    [DisplayName("Universal Package")]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.ProGet.ProGetPackageConfiguration,OtterCoreEx")]
    public sealed class ProGetPackageConfiguration : PersistedConfiguration, IFeedPackageInstallationConfiguration, IExistential
    {
        public override string ConfigurationKey
        {
            get
            {
                if (this.LocalRegistry == LocalRegistryOptions.Machine)
                    return this.PackageName;
                else if (this.LocalRegistry == LocalRegistryOptions.User)
                    return $"User::{this.PackageName}";
                
                
                return $"{this.PackageName}::{this.TargetDirectory}";
            }
        }

        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("Package source")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<UniversalPackageSource>))]
        public string PackageSourceName { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestableValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }

        [Persistent]
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("latest")]
        [DefaultValue("latest")]
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("To")]
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory path on disk of the package contents.")]
        [PlaceholderText("$WorkingDirectory")]
        public string TargetDirectory { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("Exists")]
        [DefaultValue(true)]
        [PlaceholderText("true")]
        public bool Exists { get; set; } = true;

        [Category("Local registry")]
        [ScriptAlias("LocalRegistry")]
        [DisplayName("Use Local Registry")]
        [PlaceholderText("Record installation in Machine registry")]
        [DefaultValue(LocalRegistryOptions.Machine)]
        [Persistent]
        public LocalRegistryOptions LocalRegistry { get; set; }

        [Category("Local registry")]
        [Description("Cache Package")]
        [ScriptAlias("LocalCache")]
        [DefaultValue(false)]
        [PlaceholderText("package is not cached locally")]
        public bool LocalCache { get; set; }

        [Persistent]
        [Category("File comparison")]
        [ScriptAlias("FileCompare")]
        [DisplayName("Compare files")]
        [DefaultValue(FileCompareOptions.FileSize)]
        public FileCompareOptions FileCompare { get; set; }

        [Category("File comparison")]
        [ScriptAlias("Ignore")]
        [PlaceholderText("compare all files")]
        [MaskingDescription]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IEnumerable<string> IgnoreFiles { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("DirectDownload")]
        [DisplayName("Direct download")]
        [PlaceholderText("download package file on remote server")]
        [Description("Set this to value to false if your remote server doesn't have direct access to the ProGet feed.")]
        [DefaultValue(true)]
        public bool DirectDownload { get; set; }

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
        [ScriptAlias("DeleteExtra")]
        public bool DeleteExtra { get; set; }
        [Undisclosed]
        [ScriptAlias("Include")]
        public IEnumerable<string> Includes { get; set; }
        [Undisclosed]
        [ScriptAlias("Exclude")]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("FeedUrl")]
        public string FeedUrl { get; set; }

        [Persistent]
        public bool DriftedFiles { get; set; }

        [Persistent]
        public List<string> DriftedFileNames { get; set; } = new List<string>();

    }

    public enum FileCompareOptions
    {
        DoNotCompare,
        FileSize,
        FileSizeAndLastModified
    }
}
