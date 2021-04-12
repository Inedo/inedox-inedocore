﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Operations.ProGet
{
    [Serializable]
    [Tag("proget")]
    [ScriptAlias("Repack-Package")]
    [DisplayName("Repackage Package")]
    [ScriptNamespace(Namespaces.ProGet)]
    [Description("Creates a new package with an altered version number to a ProGet feed and adds a repackaging entry to its metadata for auditing.")]
    public sealed class ChangePackageVersionOperation : RemotePackageOperationBase
    {
        protected override bool ResolveNuGetPackageSources => true;

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

        [SlimSerializable]
        private string ApiKey { get; set; }
        [SlimSerializable]
        private string HostName { get; set; }
        [SlimSerializable]
        private string FeedName { get; set; }

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
            this.LogInformation($"Repackaging {GetFullPackageName(this.PackageGroup, this.PackageName)} {this.PackageVersion} to {this.NewVersion} on {this.HostName} ({this.FeedName} feed)...");

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
            request.UserAgent = $"{SDK.ProductName}/{SDK.ProductVersion} InedoCore/{typeof(ProGetClient).Assembly.GetName().Version}";
            request.UseDefaultCredentials = true;

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

            this.ApiKey = password;

            Uri uri;
            try
            {
                uri = new Uri(feedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? feedUrl : ("http://" + feedUrl));
            }
            catch (Exception ex)
            {
                throw new ExecutionFailureException("Feed URL is invalid: " + ex.Message);
            }

            this.HostName = uri.GetLeftPart(UriPartial.Authority);

            var pathParts = uri.AbsolutePath.Trim('/').Split(new[] { '/' });
            if (pathParts.Length < 2)
                throw new ExecutionFailureException("Could not determine feed name from feed URL " + feedUrl);

            this.FeedName = Uri.UnescapeDataString(pathParts[1]);
        }

        [SlimSerializable]
        private sealed class RepackageInfo
        {
            public RepackageInfo()
            {
            }
            public RepackageInfo(string packageName, string originalVersion, string newVersion)
            {
                this.PackageName = packageName;
                this.OriginalVersion = originalVersion;
                this.NewVersion = newVersion;
            }

            [SlimSerializable]
            public string PackageName { get; set; }
            [SlimSerializable]
            public string OriginalVersion { get; set; }
            [SlimSerializable]
            public string NewVersion { get; set; }
        }
    }
}
