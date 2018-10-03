using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Extensions.UniversalPackages;
using Inedo.Serialization;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Web;
using System.Collections.Generic;

namespace Inedo.Extensions.Configurations.ProGet
{
    [Serializable]
    [DisplayName("ProGet Package")]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.ProGet.ProGetPackageConfiguration,OtterCoreEx")]
#pragma warning disable CS0618 // Type or member is obsolete
    public sealed class ProGetPackageConfiguration : PersistedConfiguration, IHasCredentials<ProGetCredentials>, IProGetPackageInstallTemplate
#pragma warning restore CS0618 // Type or member is obsolete
        , IHasCredentials<InedoProductCredentials>
    {
        [Persistent]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("Feed")]
        [DisplayName("Feed name")]
        [SuggestableValue(typeof(FeedNameSuggestionProvider))]
        public string FeedName { get; set; }

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
        [SuggestableValue(typeof(PackageVersionSuggestionProvider))]
        public string PackageVersion { get; set; }

        [Persistent]
        [ScriptAlias("DeleteExtra")]
        [DisplayName("Delete files not in Package")]
        public bool DeleteExtra { get; set; }

        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Directory")]
        [DisplayName("Target directory")]
        [Description("The directory path on disk of the package contents.")]
        public string TargetDirectory { get; set; }

        [ScriptAlias("Include")]
        [PlaceholderText("** (all items)")]
        [DefaultValue("@(**)")]
        [MaskingDescription]
        public IEnumerable<string> Includes { get; set; } = new[] { "**" };
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }

        [Category("Connection/Identity")]
        [Persistent]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet server URL")]
        [PlaceholderText("Use server URL from credential")]
#pragma warning disable CS0618 // Type or member is obsolete
        [MappedCredential(nameof(ProGetCredentials.Url))]
#pragma warning restore CS0618 // Type or member is obsolete
        public string FeedUrl { get; set; }

        [Category("Connection/Identity")]
        [Persistent]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access this feed.")]
        [PlaceholderText("Use user name from credential")]
#pragma warning disable CS0618 // Type or member is obsolete
        [MappedCredential(nameof(ProGetCredentials.UserName))]
#pragma warning restore CS0618 // Type or member is obsolete
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [Persistent]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [PlaceholderText("Use password from credential")]
        [Description("The password of a user in ProGet that can access this feed.")]
#pragma warning disable CS0618 // Type or member is obsolete
        [MappedCredential(nameof(ProGetCredentials.Password))]
#pragma warning restore CS0618 // Type or member is obsolete
        public string Password { get; set; }

        [Persistent]
        public bool Current { get; set; }
    }
}
