using Inedo.Diagnostics;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Configurations.ProGet;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.SecureResources;
using Inedo.Extensions.UniversalPackages;
using Inedo.IO;
using Inedo.UPack;
using Inedo.UPack.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Operations.ProGet
{
    /// <summary>
    /// This is a rewrite of <see cref="ProGetClient"/> to use the <see cref="UniversalFeedClient"/> when possible
    /// </summary>
    internal sealed class ProGetFeedClient
    {
        private static readonly LazyRegex ApiEndPointUrlRegex = new LazyRegex(@"(?<baseUrl>(https?://)?[^/]+)/(?<feedType>upack)(/?(?<feedName>[^/]+)/?)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public string FeedApiEndpointUrl { get; }
        public string ProGetBaseUrl { get;  }
        public string FeedName { get;  }
        public string FeedType { get; }
        public SecureCredentials Credentials { get; }

        private ILogSink Log { get; }
        private CancellationToken CancellationToken { get;  }

        public ProGetFeedClient(string feedApiEndpointUrl, SecureCredentials credentials = null, ILogSink log = null, CancellationToken cancellationToken = default)
        {
            if (feedApiEndpointUrl == null)
                throw new ArgumentNullException(nameof(feedApiEndpointUrl));

            var match = ApiEndPointUrlRegex.Match(feedApiEndpointUrl ?? "");
            if (!match.Success) 
                throw new ArgumentException($"{feedApiEndpointUrl} is not a valid {nameof(UniversalPackageSource)} url.", nameof(feedApiEndpointUrl));

            this.Log = log;
            this.CancellationToken = cancellationToken;
            this.FeedApiEndpointUrl = feedApiEndpointUrl;
            this.ProGetBaseUrl = match.Groups["baseUrl"].Value;
            this.FeedName = match.Groups["feedName"].Value;
            this.FeedType = match.Groups["feedType"].Value;
            this.Credentials = credentials;
        }

        public static ProGetFeedClient TryCreate(IFeedPackageConfiguration feedConfig, ICredentialResolutionContext context, ILogSink log = null, CancellationToken cancellationToken = default)
        {
            if (feedConfig == null)
                return null;

            UniversalPackageSource packageSource = null;
            if (!string.IsNullOrEmpty(feedConfig.PackageSourceName))
            {
                packageSource = SecureResource.TryCreate(feedConfig.PackageSourceName, context) as UniversalPackageSource;
                if (packageSource == null)
                {
                    log.LogDebug($"No {nameof(UniversalPackageSource)} with the name {feedConfig.PackageSourceName} was found.");
                    return null;
                }
            }

            // endpoint can be specified via secure resource or directly
            string apiEndpointUrl = null;
            {
                if (!string.IsNullOrEmpty(feedConfig.FeedUrl))
                    apiEndpointUrl = feedConfig?.FeedUrl;

                else if (!string.IsNullOrEmpty(packageSource?.ApiEndpointUrl))
                    apiEndpointUrl = packageSource.ApiEndpointUrl;

                else
                {
                    log.LogDebug($"No Api Endpoint URL was specified.");
                    return null;
                }
            }

            // rebuild URL if FeedName is overriden
            if (!string.IsNullOrEmpty(feedConfig.FeedName))
            {
                var match = ApiEndPointUrlRegex.Match(apiEndpointUrl ?? "");
                if (!match.Success)
                {
                    log.LogDebug($"{apiEndpointUrl} is not a valid {nameof(UniversalPackageSource)} url.");
                    return null;
                }

                apiEndpointUrl = $"{match.Groups["baseUrl"]}/upack/{feedConfig.FeedName}/";
            }

            SecureCredentials credentials;
            if (!string.IsNullOrEmpty(feedConfig.ApiKey))
                credentials = new TokenCredentials { Token = AH.CreateSecureString(feedConfig.ApiKey) };
            else if (!string.IsNullOrEmpty(feedConfig.UserName))
                credentials = new UsernamePasswordCredentials { UserName = feedConfig.UserName, Password = AH.CreateSecureString(feedConfig.Password) };
            else
                credentials = packageSource?.GetCredentials(context);

            return new ProGetFeedClient(apiEndpointUrl, credentials, log, cancellationToken);
        }

        public async Task<string[]> GetFeedNamesAsync()
        {
            using var client = this.CreateHttpClient();
            using var response = await client.GetAsync(this.ProGetBaseUrl + "/upack?list-feeds", HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);

            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding);
            using var jsonReader = new JsonTextReader(streamReader);
            var serializer = JsonSerializer.Create();
            return serializer.Deserialize<string[]>(jsonReader);
        }
        public Task<IReadOnlyList<RemoteUniversalPackage>> ListPackagesAsync() => this.CreateUPacklient().ListPackagesAsync(null, null, this.CancellationToken);

        public Task<IReadOnlyList<RemoteUniversalPackageVersion>> ListPackageVersionsAsync(string packageName) => this.CreateUPacklient().ListPackageVersionsAsync(UniversalPackageId.Parse(packageName), false, null, this.CancellationToken);
        public Task<RemoteUniversalPackageVersion> FindPackageVersionAsync(IFeedPackageConfiguration config) => this.FindPackageVersionAsync(config.PackageName, config.PackageVersion);

        public async Task<RemoteUniversalPackageVersion> FindPackageVersionAsync(string packageName, string packageVersion)
        {

            var id = UniversalPackageId.Parse(packageName);
            var upack = this.CreateUPacklient();

#warning handle this with ProGetPackageVersionSpecifier
            if (packageVersion?.StartsWith("latest") == true)
            {
                this.Log.LogDebug($"Looking for {packageVersion}...");

                var stableOnly = packageVersion == "latest-stable";
                foreach (var package in await upack.ListPackageVersionsAsync(id, false, null, this.CancellationToken).ConfigureAwait(false))
                {
                    if (stableOnly && !string.IsNullOrEmpty(package.Version.Prerelease))
                        continue;

                    this.Log.LogDebug($"Found version {package.Version}.");
                    return package;
                }
                this.Log.LogDebug($"No versions found.");
                return null;
            }

            return await upack.GetPackageVersionAsync(id, UniversalPackageVersion.Parse(packageVersion), false, this.CancellationToken).ConfigureAwait(false);
        }
        public async Task<ProGetPackageVersionInfo> GetPackageVersionWithFilesAsync(UniversalPackageId id, UniversalPackageVersion version)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));

            var url = $"{this.ProGetBaseUrl}/upack/{this.FeedName}/versions?group={Uri.EscapeDataString(id.Group)}&name={Uri.EscapeDataString(id.Name)}&version={Uri.EscapeDataString(version.ToString())}&includeFileList=true";

            using var client = this.CreateHttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);

            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding);
            using var jsonReader = new JsonTextReader(streamReader);
            var serializer = JsonSerializer.Create();
            return serializer.Deserialize<ProGetPackageVersionInfo>(jsonReader);
        }
        public async Task<Stream> DownloadPackageContentAsync(UniversalPackageId id, UniversalPackageVersion version, PackageDeploymentData deployInfo, Action<long, long> progressUpdate = null)
        {
            var url = Uri.EscapeDataString(id.Name) + "/" + Uri.EscapeDataString(version.ToString());
            if (!string.IsNullOrEmpty(id.Group))
                url = id.Group + "/" + url;

            using var client = this.CreateHttpClient();
            if (deployInfo != null)
            {
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Application, deployInfo.Application ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Description, deployInfo.Description ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Url, deployInfo.Url ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Target, deployInfo.Target ?? string.Empty);
            }
            using var response = await client.GetAsync(this.FeedApiEndpointUrl + "download/" + url, HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false);
            await HandleError(response).ConfigureAwait(false);

            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var tempStream = TemporaryStream.Create(response.Content.Headers.ContentLength ?? 0L);
            await responseStream.CopyToAsync(tempStream, 81920, this.CancellationToken, position =>
            {
                progressUpdate?.Invoke(position, response.Content.Headers.ContentLength ?? 0L);
            }).ConfigureAwait(false);
            tempStream.Position = 0;
            return tempStream;
        }

        private UniversalFeedClient CreateUPacklient()
        {
            if (this.Credentials is TokenCredentials tcreds)
                return new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(this.FeedApiEndpointUrl), "api", tcreds.Token));
            else if (this.Credentials is UsernamePasswordCredentials upcreds)
                return new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(this.FeedApiEndpointUrl), upcreds.UserName, upcreds.Password));
            else
                return new UniversalFeedClient(this.FeedApiEndpointUrl);

        }
        private HttpClient CreateHttpClient()
        {
            HttpClient client;
            if (this.Credentials is TokenCredentials tcreds)
            {
                this.Log.LogDebug($"Making request with API Key...");
                client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-ApiKey", AH.Unprotect(tcreds.Token));
            }
            else if (this.Credentials is UsernamePasswordCredentials upcreds)
            {
                this.Log.LogDebug($"Making request as {upcreds.UserName}...");
                client = new HttpClient(new HttpClientHandler { Credentials = new NetworkCredential(upcreds.UserName, AH.Unprotect(upcreds.Password) ?? "") });
            }
            else
                client = new HttpClient();

            client.Timeout = Timeout.InfiniteTimeSpan;

            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(typeof(Operation).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product, typeof(Operation).Assembly.GetName().Version.ToString()));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InedoCore", typeof(ProGetFeedClient).Assembly.GetName().Version.ToString()));

            return client;
        }

        public Task<Stream> GetPackageStreamAsync(IFeedPackageConfiguration config) 
            => this.CreateUPacklient().GetPackageStreamAsync(UniversalPackageId.Parse(config.PackageName), UniversalPackageVersion.Parse(config.PackageVersion), this.CancellationToken);

        private static async Task HandleError(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.InternalServerError && message.StartsWith("<!DOCTYPE"))
                message = "Invalid feed URL. Ensure the feed URL follows the format: http://{proget-server}/upack/{feed-name}";

            throw new ProGetException((int)response.StatusCode, message);
        }
    }
}
