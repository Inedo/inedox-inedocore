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
    [ScriptAlias("Promote-Package")]
    [DisplayName("Promote Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Description("Promotes a package from one feed to another in a ProGet instance.")]
    public sealed class PromotePackageOperation : RemotePackageOperationBase
    {
        protected override bool ResolveNuGetPackageSources => true;

        private string hostName;
        private string fromFeed;
        private string toFeed;
        private string apiKey;

        [DisplayName("From source")]
        [ScriptAlias("From")]
        [SuggestableValue(typeof(PackageSourceSuggestionProvider))]
        [PlaceholderText("Infer from package name")]
        public override string PackageSource { get; set; }
        [DisplayName("To source")]
        [ScriptAlias("To")]
        [SuggestableValue(typeof(PackageSourceSuggestionProvider))]
        [PlaceholderText("Same as From")]
        public string TargetPackageSource { get; set; }

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
        [ScriptAlias("Reason")]
        [DisplayName("Reason")]
        [PlaceholderText("Unspecified")]
        public string Reason { get; set; }

        protected override async Task BeforeRemoteExecuteAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.PackageName))
                throw new ExecutionFailureException("\"Name\" is required.");

            if (string.IsNullOrWhiteSpace(this.PackageVersion))
                throw new ExecutionFailureException("\"Version\" is required.");

            await base.BeforeRemoteExecuteAsync(context);

            this.TargetPackageSource = this.TargetPackageSource ?? this.PackageSource;

            this.ResolvePackageSource(context, this.TargetPackageSource, out var toUserName, out var toPassword, out var toFeedUrl);

            if (toPassword?.Length > 0)
                this.apiKey = AH.Unprotect(toPassword);
            else if (string.IsNullOrEmpty(this.apiKey))
                throw new ExecutionFailureException("This operation requires a ProGet API key. This can be specified either with Inedo Product credentials or with Username & Password credentials (with a UserName of \"api\").");

            var match = Regex.Match(toFeedUrl, @"^(?<1>.+)/[^/]+/(?<2>[^/]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
            if (!match.Success)
                throw new ExecutionFailureException($"Could not feed name from feedUrl: " + toFeedUrl);

            this.toFeed = match.Groups[2].Value;
        }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            this.LogInformation($"Promoting {GetFullPackageName(this.PackageGroup, this.PackageName)} {this.PackageVersion} from {this.fromFeed} to {this.toFeed} on {this.hostName}...");

            if (string.Equals(this.fromFeed, this.toFeed, StringComparison.OrdinalIgnoreCase))
            {
                this.LogWarning("Source and target feeds are the same; nothing to do.");
                return null;
            }

            var url = this.hostName;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "http://" + url;

            if (!url.EndsWith("/"))
                url += "/";

            url += "api/promotions/promote";

            this.LogDebug($"Making request to {url}...");
            var request = WebRequest.CreateHttp(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add("X-ApiKey", this.apiKey);
            request.UserAgent = $"{SDK.ProductName}/{SDK.ProductVersion} InedoCore/{typeof(ProGetClient).Assembly.GetName().Version}";
            request.UseDefaultCredentials = true;

            try
            {
                using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), InedoLib.UTF8Encoding))
                {
                    var data = new Dictionary<string, string>
                    {
                        ["fromFeed"] = this.fromFeed,
                        ["toFeed"] = this.toFeed,
                        ["packageName"] = this.PackageName,
                        ["version"] = this.PackageVersion
                    };

                    if (!string.IsNullOrWhiteSpace(this.PackageGroup))
                        data["groupName"] = this.PackageGroup;

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

                this.LogInformation("Promotion was successful.");

                return true;
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
        protected override async Task AfterRemoteExecuteAsync(object result)
        {
            await base.AfterRemoteExecuteAsync(result);

            if (this.PackageManager != null && result is bool b && b)
            {
                AttachedPackage package = null;

                foreach (var p in await this.PackageManager.GetBuildPackagesAsync(default))
                {
                    if (p.Active && string.Equals(p.Name, this.PackageName, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Version, this.PackageVersion, StringComparison.OrdinalIgnoreCase) && string.Equals(p.PackageSource, this.PackageSource, StringComparison.OrdinalIgnoreCase))
                    {
                        package = p;
                        await this.PackageManager.DeactivatePackageAsync(p.Name, p.Version, p.PackageSource);
                    }
                }

                if (package != null)
                {
                    await this.PackageManager.AttachPackageToBuildAsync(
                        new AttachedPackage(package.PackageType, package.Name, package.Version, package.Hash, this.TargetPackageSource),
                        default
                    );
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Promote ",
                    new Hilite(GetFullPackageName(config[nameof(PackageGroup)], config[nameof(PackageName)])),
                    " ",
                    new Hilite(config[nameof(PackageVersion)])
                ),
                new RichDescription(
                    "from ",
                    new Hilite(config[nameof(PackageSource)]),
                    " to ",
                    new Hilite(config[nameof(TargetPackageSource)])
                )
            );
        }

        private protected override void SetPackageSourceProperties(string userName, string password, string feedUrl)
        {
            if (string.Equals(userName, "api", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(password))
                this.apiKey = password;

            var match = Regex.Match(feedUrl, @"^(?<1>.+)/[^/]+/(?<2>[^/]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
            if (!match.Success)
                throw new ExecutionFailureException($"This operation requires a ProGet feed endpoint URL to be specified in the \"{this.PackageSource}\" package source.");

            this.hostName = match.Groups[1].Value;
            this.fromFeed = match.Groups[2].Value;
        }
    }
}
