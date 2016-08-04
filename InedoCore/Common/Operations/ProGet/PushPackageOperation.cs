using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
#if Otter
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensions.Credentials;
#elif BuildMaster
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
#endif
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions;
using Inedo.IO;

namespace Inedo.Extensions.Operations.ProGet
{
    [DisplayName("Push Package")]
    [Description("Uploads a zip file containing the contents of a Universal Package to a ProGet feed.")]
    [ScriptAlias("Push-Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Tag("ProGet")]
    [Serializable]
    public sealed class PushPackageOperation : RemoteExecuteOperation, IHasCredentials<ProGetCredentials>
    {
        [Required]
        [ScriptAlias("FeedUrl")]
        [DisplayName("ProGet feed URL")]
        [Description("The ProGet feed API endpoint URL.")]
        [MappedCredential(nameof(ProGetCredentials.Url))]
        public string FeedUrl { get; set; }
        
        [Required]
        [ScriptAlias("FilePath")]
        [DisplayName("Package file path")]
        public string FilePath { get; set; }
        [ScriptAlias("Group")]
        [DisplayName("Group name")]
        public string Group { get; set; }
        [ScriptAlias("Name")]
        [DisplayName("Package name")]
        [PlaceholderText("$ApplicationName")]
        [DefaultValue("$ApplicationName")]
        public string Name { get; set; }
        [ScriptAlias("Version")]
        [PlaceholderText("$ReleaseNumber")]
        [DefaultValue("$ReleaseNumber")]
        public string Version { get; set; }
        
        [ScriptAlias("Title")]
        public string Title { get; set; }
        [ScriptAlias("Icon")]
        public string Icon { get; set; }
        [ScriptAlias("Description")]
        public string Description { get; set; }
        [ScriptAlias("Dependencies")]
        public IEnumerable<string> Dependencies { get; set; }

        [Category("Identity")]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        [Description("If a credential name is specified, the UserName and Password fields will be ignored.")]
        public string CredentialName { get; set; }
        [Category("Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("ProGet user name")]
        [Description("The name of a user in ProGet that can access this feed.")]
        [MappedCredential(nameof(ProGetCredentials.UserName))]
        public string UserName { get; set; }
        [Category("Identity")]
        [ScriptAlias("Password")]
        [DisplayName("ProGet password")]
        [Description("The password of a user in ProGet that can access this feed.")]
        [MappedCredential(nameof(ProGetCredentials.Password))]
        public string Password { get; set; }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            var client = new ProGetClient(this.FeedUrl, this.UserName, this.Password);

            this.LogInformation($"Pushing package {this.Name} to ProGet...");

            string path = context.ResolvePath(this.FilePath);

            this.LogDebug("Using package file: " + path);

            string content;
            using (var file = FileEx.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var stream = new SlimMemoryStream())
            {
                file.CopyTo(stream);
                var bytes = stream.ToArray();
                content = Convert.ToBase64String(bytes);
            }

            string name = this.Name;

            var data = new ProGetPackagePushData
            {
                title = this.Title,
                description = this.Description,
                icon = this.Icon,
                dependencies = this.Dependencies?.ToArray(),
                contentBase64 = content
            };

            await client.PushPackageAsync(this.Group, name, this.Version, data).ConfigureAwait(false);

            this.LogInformation("Package pushed.");
            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Push ", new Hilite(config[nameof(Name)]), " Package"),
                new RichDescription("to ProGet feed ", config[nameof(FeedUrl)])
            );
        }
    }
}
