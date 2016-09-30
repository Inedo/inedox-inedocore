using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensions.SuggestionProviders;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions.Credentials;
using Inedo.Otter.Web.Controls;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
#endif
using Inedo.Serialization;

namespace Inedo.Extensions.Configurations.ProGet
{
    [Serializable]
    [DisplayName("ProGet Package")]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.ProGet.ProGetPackageConfiguration,OtterCoreEx")]
    public sealed class ProGetPackageConfiguration : PersistedConfiguration, IHasCredentials<ProGetCredentials>
    {
        [Persistent]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("Feed")]
        [DisplayName("Feed name")]
        [SuggestibleValue(typeof(FeedNameSuggestionProvider))]
        public string FeedName { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [SuggestibleValue(typeof(PackageNameSuggestionProvider))]
        public string PackageName { get; set; }

        [Persistent]
        [ScriptAlias("Version")]
        [DisplayName("Package version")]
        [PlaceholderText("latest")]
        [SuggestibleValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory path on disk of the package contents.")]
        public string TargetDirectory { get; set; }
        
        [Category("Connection/Identity")]
        [Persistent]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from credential")]
        [MappedCredential(nameof(ProGetCredentials.Url))]
        public string FeedUrl { get; set; }

        [Category("Connection/Identity")]
        [Persistent]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access this feed.")]
        [PlaceholderText("Use user name from credential")]
        [MappedCredential(nameof(ProGetCredentials.UserName))]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
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
