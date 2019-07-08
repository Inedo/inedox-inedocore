using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [DisplayName("Repackage Package")]
    [Description("Changes the version number of a package in a ProGet feed and adds a repackaging entry to its metadata.")]
    [ScriptNamespace(Namespaces.ProGet)]
    [ScriptAlias("Repack-Package")]
    public sealed class ChangePackageVersionOperation : RemoteExecuteOperation, IHasCredentials<InedoProductCredentials>
    {
        [Required]
        [ScriptAlias("Feed")]
        [DisplayName("Feed")]
        public string FeedName { get; set; }
        [ScriptAlias("Group")]
        [DisplayName("Group")]
        public string PackageGroup { get; set; }
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Name")]
        public string PackageName { get; set; }
        [Required]
        [ScriptAlias("Version")]
        [DisplayName("Version")]
        public string PackageVersion { get; set; }
        [Required]
        [ScriptAlias("NewVersion")]
        [DisplayName("New version")]
        public string NewVersion { get; set; }
        [ScriptAlias("Reason")]
        [DisplayName("Reason")]
        [PlaceholderText("Unspecified")]
        public string Reason { get; set; }

        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }
        [ScriptAlias("Host")]
        [DisplayName("ProGet host")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use server URL from credentials")]
        [MappedCredential(nameof(InedoProductCredentials.Host))]
        [Description("This should be the host name (or URL) of the server only, without the /api/ endpoint or the feed name.")]
        public string HostName { get; set; }
        [ScriptAlias("ApiKey")]
        [DisplayName("API key")]
        [Category("Connection/Identity")]
        [FieldEditMode(FieldEditMode.Password)]
        [PlaceholderText("Use API key from credentials")]
        [MappedCredential(nameof(InedoProductCredentials.ApiKey))]
        [Description("The API key must have permission to use the Repackaging API in the connected ProGet instance.")]
        public string ApiKey { get; set; }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.PackageName))
            {
                this.LogError("\"Name\" is required.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(this.PackageVersion))
            {
                this.LogError("\"Version\" is required.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(this.NewVersion))
            {
                this.LogError("\"NewVersion\" is required.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(this.FeedName))
            {
                this.LogError("\"Feed\" is required.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(this.HostName))
            {
                this.LogError("\"Host\" is required.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(this.ApiKey))
            {
                this.LogError("\"ApiKey\" is required.");
                return null;
            }

            this.LogInformation($"Repackaging {GetPackageDisplayName(this.PackageGroup, this.PackageName)} {this.PackageVersion} to {this.NewVersion} on {this.HostName} ({this.FeedName} feed)...");

            if (string.Equals(this.PackageVersion, this.NewVersion, StringComparison.OrdinalIgnoreCase))
            {
                this.LogWarning("\"Version\" and \"NewVersion\" are the same; nothing to do.");
                return null;
            }

            var url = this.HostName;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "http://" + url;

            if (!url.EndsWith("/"))
                url += "/";

            url += "api/repackaging/repackage";

            this.LogDebug($"Making request to {url}...");
            var request = WebRequest.CreateHttp(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add("X-ApiKey", this.ApiKey);

            try
            {
                using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), InedoLib.UTF8Encoding))
                {
                    var data = new Dictionary<string, string>
                    {
                        ["feed"] = this.FeedName,
                        ["packageName"] = this.PackageName,
                        ["version"] = this.PackageVersion,
                        ["newVersion"] = this.NewVersion
                    };

                    if (!string.IsNullOrWhiteSpace(this.PackageGroup))
                        data["group"] = this.PackageGroup;

                    if (!string.IsNullOrWhiteSpace(this.Reason))
                        data["comments"] = this.Reason;

                    writer.Write(
                        string.Join(
                            "&",
                            data.Select(p => p.Key + "=" + Uri.EscapeDataString(p.Value))
                        )
                    );
                }

                using (var response = await request.GetResponseAsync())
                {
                }

                this.LogInformation("Repackage was successful.");
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse exResponse)
            {
                using (var reader = new StreamReader(exResponse.GetResponseStream(), InedoLib.UTF8Encoding))
                {
                    var message = reader.ReadToEnd();
                    this.LogError($"The server responsed with {(int)exResponse.StatusCode}: {message}");
                }
            }

            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            RichDescription longDesc;
            var creds = (string)config[nameof(CredentialName)];
            var host = (string)config[nameof(HostName)];
            var feed = (string)config[nameof(FeedName)];

            if (!string.IsNullOrWhiteSpace(host))
            {
                longDesc = new RichDescription(
                    "on ",
                    new Hilite(feed),
                    " feed at ",
                    new Hilite(host)
                );
            }
            else if (!string.IsNullOrWhiteSpace(creds))
            {
                longDesc = new RichDescription(
                    "on ",
                    new Hilite(feed),
                    " feed at server specified in ",
                    new Hilite(creds),
                    " credentials"
                );
            }
            else
            {
                longDesc = new RichDescription(
                    "on ",
                    new Hilite(feed)
                );
            }

            return new ExtendedRichDescription(
                new RichDescription(
                    "Repackage ",
                    new Hilite(GetPackageDisplayName(config[nameof(PackageGroup)], config[nameof(PackageName)])),
                    " ",
                    new Hilite(config[nameof(PackageVersion)]),
                    " to ",
                    new Hilite(config[nameof(NewVersion)])
                ),
                longDesc
            );
        }

        private static string GetPackageDisplayName(string group, string name) => string.IsNullOrWhiteSpace(group) ? name : (group + "/" + name);
    }
}
