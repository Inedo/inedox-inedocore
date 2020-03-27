using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [Tag("proget")]
    [ScriptAlias("Repack-Package")]
    [DisplayName("Repackage Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Description("Changes the version number of a package in a ProGet feed and adds a repackaging entry to its metadata.")]
    public sealed class ChangePackageVersionOperation : RemotePackageOperationBase
    {
        private string apiKey;
        private string hostName;
        private string feedName;

        [ScriptAlias("PackageSource")]
        [DisplayName("Package source")]
        [PlaceholderText("Infer from package name")]
        [SuggestableValue(typeof(PackageSourceSuggestionProvider))]
        public override string PackageSource { get; set; }

        [ScriptAlias("Group")]
        [DisplayName("Group")]
        public string PackageGroup { get; set; }
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Name")]
        public override string PackageName { get; set; }
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

        protected override Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.PackageName))
                throw new ExecutionFailureException("\"Name\" is required.");

            if (string.IsNullOrWhiteSpace(this.PackageVersion))
                throw new ExecutionFailureException("\"Version\" is required.");

            if (string.IsNullOrWhiteSpace(this.NewVersion))
                throw new ExecutionFailureException("\"NewVersion\" is required.");

            return base.BeforeRemoteExecuteAsync(context);
        }
        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            this.LogInformation($"Repackaging {GetFullPackageName(this.PackageGroup, this.PackageName)} {this.PackageVersion} to {this.NewVersion} on {this.hostName} ({this.feedName} feed)...");

            if (string.Equals(this.PackageVersion, this.NewVersion, StringComparison.OrdinalIgnoreCase))
            {
                this.LogWarning("\"Version\" and \"NewVersion\" are the same; nothing to do.");
                return null;
            }

            var url = this.hostName;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "http://" + url;

            if (!url.EndsWith("/"))
                url += "/";

            url += "api/repackaging/repackage";

            this.LogDebug($"Making request to {url}...");
            var request = WebRequest.CreateHttp(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add("X-ApiKey", this.apiKey);

            try
            {
                using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), InedoLib.UTF8Encoding))
                {
                    var data = new Dictionary<string, string>
                    {
                        ["feed"] = this.feedName,
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
                            data.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))
                        )
                    );
                }

                using (var response = await request.GetResponseAsync())
                {
                }

                this.LogInformation("Repackage was successful.");

                return new RepackageInfo(GetFullPackageName(this.PackageGroup, this.PackageName), this.PackageVersion, this.NewVersion);
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse exResponse)
            {
                using (var reader = new StreamReader(exResponse.GetResponseStream(), InedoLib.UTF8Encoding))
                {
                    var message = reader.ReadToEnd();
                    this.LogError($"The server responded with {(int)exResponse.StatusCode}: {message}");
                }
            }

            return null;
        }
        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            await base.AfterRemoteExecuteAsync(result);

            if (this.PackageManager != null && result is RepackageInfo info && !string.IsNullOrWhiteSpace(this.PackageSource))
            {
                var packageType = AttachedPackageType.Universal;

                foreach (var p in await this.PackageManager.GetBuildPackagesAsync(default))
                {
                    if (p.Active && string.Equals(p.Name, info.PackageName, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Version, info.OriginalVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        packageType = p.PackageType;
                        await this.PackageManager.DeactivatePackageAsync(p.Name, p.Version, p.PackageSource);
                    }
                }

                await this.PackageManager.AttachPackageToBuildAsync(
                    new AttachedPackage(packageType, info.PackageName, info.NewVersion, null, AH.NullIf(this.PackageSource, string.Empty)),
                    default
                );
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Repackage ",
                    new Hilite(GetFullPackageName(config[nameof(PackageGroup)], config[nameof(PackageName)])),
                    " ",
                    new Hilite(config[nameof(PackageVersion)]),
                    " to ",
                    new Hilite(config[nameof(NewVersion)])
                ),
                new RichDescription(
                    "on ",
                    new Hilite(config[nameof(PackageSource)])
                )
            );
        }

        private protected override void SetPackageSourceProperties(string userName, string password, string feedUrl)
        {
            if (!string.Equals(userName, "api", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(password))
                throw new ExecutionFailureException("This operation requires a ProGet API key. This can be specified either with Inedo Product credentials or with Username & Password credentials (with a UserName of \"api\").");

            this.apiKey = password;

            var match = Regex.Match(feedUrl, @"^(?<1>.+)/[^/]+/(?<2>[^/]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
            if (!match.Success)
                throw new ExecutionFailureException($"Could not determine host name or feed name from feedUrl: " + feedUrl);

            this.hostName = match.Groups[1].Value;
            this.feedName = match.Groups[2].Value;
        }

        [Serializable]
        private sealed class RepackageInfo
        {
            public RepackageInfo(string packageName, string originalVersion, string newVersion)
            {
                this.PackageName = packageName;
                this.OriginalVersion = originalVersion;
                this.NewVersion = newVersion;
            }

            public string PackageName { get; }
            public string OriginalVersion { get; }
            public string NewVersion { get; }
        }
    }
}
