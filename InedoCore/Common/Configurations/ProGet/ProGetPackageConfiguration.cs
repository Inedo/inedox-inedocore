using System;
using System.ComponentModel;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions.Credentials;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Credentials;
#endif
using Inedo.Serialization;

namespace Inedo.Extensions.Configurations.ProGet
{
    [Serializable]
    [DisplayName("ProGet Package")]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.ProGet.ProGetPackageConfiguration,OtterCoreEx")]
    public sealed class ProGetPackageConfiguration : PersistedConfiguration, IHasCredentials<ProGetCredentials>
    {
        [Required]
        [Persistent]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        public string PackageName { get; set; }
        [Persistent]
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [Description("The version of the package. Use \"latest\" to ensure the latest available version.")]
        public string PackageVersion { get; set; }
        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory path on disk of the package contents.")]
        public string TargetDirectory { get; set; }

        [Persistent]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }
        [Persistent]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet feed URL")]
        [Description("The ProGet feed API endpoint URL.")]
        [PlaceholderText("Use feed URL from credential")]
        [MappedCredential(nameof(ProGetCredentials.Url))]
        public string FeedUrl { get; set; }
        [Persistent]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access this feed.")]
        [PlaceholderText("Use user name from credential")]
        [MappedCredential(nameof(ProGetCredentials.UserName))]
        public string UserName { get; set; }
        [Persistent]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [PlaceholderText("Use password from credential")]
        [Description("The password of a user in ProGet that can access this feed.")]
        [MappedCredential(nameof(ProGetCredentials.Password))]
        public string Password { get; set; }
        [Persistent]
        public bool Current { get; set; }
    }
}
