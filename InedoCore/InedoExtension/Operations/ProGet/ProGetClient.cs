using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using Newtonsoft.Json;

namespace Inedo.Extensions.Operations.ProGet
{
    internal sealed class ProGetClient
    {
        private static readonly LazyRegex FeedNameRegex = new LazyRegex(@"(?<1>(https?://)?[^/]+)/upack(/?(?<2>.+))", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public ProGetClient(string serverUrl, string feedName, string userName, string password, ILogSink log = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(serverUrl))
                throw new ProGetException(400, "A ProGet server URL must be specified for this operation either in the operation itself or in the credential.");

            this.UserName = AH.NullIf(userName, string.Empty);
            this.Password = AH.NullIf(password, string.Empty);
            this.Log = log ?? (ILogSink)Logger.Null;
            var result = ResolveFeedNameAndUrl(serverUrl, feedName, this.Log);
            this.FeedUrl = result.feedUrl;
            this.FeedName = result.feedName;
            this.ServerUrl = serverUrl.TrimEnd('/') + '/';
            this.CancellationToken = cancellationToken;
        }

        public string FeedUrl { get; }
        public string FeedName { get; }
        public string ServerUrl { get; }
        public string UserName { get; }
        public string Password { get; }
        public ILogSink Log { get; }
        private CancellationToken CancellationToken { get; }

        public string GetViewPackageUrl(PackageName id, string version)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));

            return $"{this.ServerUrl }feeds/{Uri.EscapeDataString(this.FeedName)}/{id.ToString()}/{version}";
        }

        public async Task<string[]> GetFeedNamesAsync()
        {
            using (var client = this.CreateClient())
            using (var response = await client.GetAsync(this.FeedUrl + "?list-feeds", HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false))
            {
                await HandleError(response).ConfigureAwait(false);

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    return serializer.Deserialize<string[]>(jsonReader);
                }
            }
        }
        public async Task<ProGetPackageInfo[]> GetPackagesAsync()
        {
            using (var client = this.CreateClient())
            using (var response = await client.GetAsync(this.FeedUrl + "packages", HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false))
            {
                await HandleError(response).ConfigureAwait(false);

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    return serializer.Deserialize<ProGetPackageInfo[]>(jsonReader);
                }
            }
        }
        public async Task<ProGetPackageInfo> GetPackageInfoAsync(PackageName id)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));

            using (var client = this.CreateClient())
            using (var response = await client.GetAsync(this.FeedUrl + $"packages?group={Uri.EscapeDataString(id.Group)}&name={Uri.EscapeDataString(id.Name)}", HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false))
            {
                await HandleError(response).ConfigureAwait(false);

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    return serializer.Deserialize<ProGetPackageInfo>(jsonReader);
                }
            }
        }
        public async Task<ProGetPackageVersionInfo> GetPackageVersionInfoAsync(PackageName id, string version)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));

            using (var client = this.CreateClient())
            using (var response = await client.GetAsync(this.FeedUrl + $"versions?group={Uri.EscapeDataString(id.Group)}&name={Uri.EscapeDataString(id.Name)}&version={Uri.EscapeDataString(version)}&includeFileList=true", HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false))
            {
                await HandleError(response).ConfigureAwait(false);

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    return serializer.Deserialize<ProGetPackageVersionInfo>(jsonReader);
                }
            }
        }
        public async Task<Stream> DownloadPackageContentAsync(PackageName id, string version, PackageDeploymentData deployInfo, Action<long, long> progressUpdate = null)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version));

            var url = Uri.EscapeDataString(id.Name) + "/" + Uri.EscapeDataString(version);
            if (!string.IsNullOrEmpty(id.Group))
                url = id.Group + "/" + url;

            using (var client = this.CreateClient(deployInfo))
            using (var response = await client.GetAsync(this.FeedUrl + "download/" + url, HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false))
            {
                await HandleError(response).ConfigureAwait(false);

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var tempStream = TemporaryStream.Create(response.Content.Headers.ContentLength ?? 0L);
                    await responseStream.CopyToAsync(tempStream, 81920, this.CancellationToken, position =>
                    {
                        progressUpdate?.Invoke(position, response.Content.Headers.ContentLength ?? 0L);
                    }).ConfigureAwait(false);
                    tempStream.Position = 0;
                    return tempStream;
                }
            }
        }
        public async Task<ZipArchive> DownloadPackageAsync(PackageName id, string version, PackageDeploymentData deployInfo)
        {
            var stream = await this.DownloadPackageContentAsync(id, version, deployInfo).ConfigureAwait(false);
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }
        public async Task PushPackageAsync(string group, string name, string version, ProGetPackagePushData packageData, Stream content)
        {
            if (packageData == null)
                throw new ArgumentNullException(nameof(packageData));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version));

            var url = Uri.EscapeDataString(name) + "/" + Uri.EscapeDataString(version);
            if (!string.IsNullOrEmpty(group))
                url = group + "/" + url;

            using (var client = this.CreateClient())
            using (var streamContent = new StreamContent(content))
            {
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

                using (var response = await client.PostAsync(this.FeedUrl + "upload/" + url + packageData.ToQueryString(), streamContent, this.CancellationToken).ConfigureAwait(false))
                {
                    await HandleError(response).ConfigureAwait(false);
                }
            }
        }
        public async Task PromotePackageAsync(SecureString apiKey, PackageName id, string version, string fromFeed, string toFeed, string comments = null)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version));
            if (string.IsNullOrWhiteSpace(fromFeed))
                throw new ArgumentNullException(nameof(fromFeed));
            if (string.IsNullOrWhiteSpace(toFeed))
                throw new ArgumentNullException(nameof(toFeed));

            using (var client = this.CreateClient(apiKey: apiKey))
            {
                string json = JsonConvert.SerializeObject(
                    new
                    {
                        packageName = id.Name,
                        groupName = id.Group,
                        version = version,
                        fromFeed = fromFeed,
                        toFeed = toFeed,
                        comments = comments
                    }
                );

                using (var streamContent = new StringContent(json))
                {
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    using (var response = await client.PostAsync(this.ServerUrl + "api/promotions/promote", streamContent, this.CancellationToken).ConfigureAwait(false))
                    {
                        await HandleError(response).ConfigureAwait(false);
                    }
                }
            }
        }

        private HttpClient CreateClient(PackageDeploymentData deployInfo = null, SecureString apiKey = null)
        {
            HttpClient client;
            if (!string.IsNullOrWhiteSpace(this.UserName))
            {
                this.Log.LogDebug($"Making request as {this.UserName}...");
                client = new HttpClient(new HttpClientHandler { Credentials = new NetworkCredential(this.UserName, this.Password ?? string.Empty) });
            }
            else
            {
                client = new HttpClient();
            }

            client.Timeout = Timeout.InfiniteTimeSpan;

            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(typeof(Operation).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product, typeof(Operation).Assembly.GetName().Version.ToString()));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InedoCore", typeof(ProGetClient).Assembly.GetName().Version.ToString()));

            if (apiKey != null)
                client.DefaultRequestHeaders.Add("X-ApiKey", AH.Unprotect(apiKey));

            if (deployInfo != null)
            {
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Application, deployInfo.Application ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Description, deployInfo.Description ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Url, deployInfo.Url ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Target, deployInfo.Target ?? string.Empty);
            }

            return client;
        }
        private static async Task HandleError(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.InternalServerError && message.StartsWith("<!DOCTYPE"))
                message = "Invalid feed URL. Ensure the feed URL follows the format: http://{proget-server}/upack/{feed-name}";

            throw new ProGetException((int)response.StatusCode, message);
        }
        private static (string feedUrl, string feedName) ResolveFeedNameAndUrl(string baseUrl, string feedName, ILogSink log)
        {
            var match = FeedNameRegex.Match(baseUrl);

            if (match.Success)
            {
                string credentialUrl = match.Groups[1].Value;
                string credentialFeedName = match.Groups[2].Value;
                string resolvedFeedName = AH.CoalesceString(Uri.EscapeUriString(feedName ?? ""), credentialFeedName);

                return (feedUrl: credentialUrl + "/upack/" + resolvedFeedName.TrimEnd('/') + '/', feedName: Uri.UnescapeDataString(resolvedFeedName));
            }
            else
            {
                return (feedUrl: baseUrl.TrimEnd('/') + "/upack/" + Uri.EscapeUriString(feedName ?? "") + '/', feedName: feedName ?? "");
            }
        }
    }

    internal sealed class PackageName
    {
        public static PackageName Parse(string fullName)
        {
            fullName = fullName?.Trim('/');
            if (string.IsNullOrEmpty(fullName))
                return new PackageName(fullName);

            int index = fullName.LastIndexOf('/');

            if (index > 0)
                return new PackageName(fullName.Substring(0, index), fullName.Substring(index + 1));
            else
                return new PackageName(fullName);
        }

        public PackageName(string name)
        {
            this.Group = "";
            this.Name = name ?? "";
        }
        public PackageName(string group, string name)
        {
            this.Group = group ?? "";
            this.Name = name ?? "";
        }
        public string Group { get; }
        public string Name { get; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.Group))
                return this.Name;
            else
                return this.Group + '/' + this.Name;
        }
    }

    internal sealed class ProGetException : Exception
    {
        public ProGetException(int statusCode, string message)
            : base(message)
        {
            this.StatusCode = statusCode;
        }

        public ProGetException(int statusCode, string message, WebException ex)
            : base(message, ex)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; set; }

        public string FullMessage => $"The server returned an error ({this.StatusCode}): {this.Message}";
    }

    internal sealed class ProGetPackageInfo
    {
        public string group { get; set; }
        public string name { get; set; }
        public string latestVersion { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public int downloads { get; set; }
        public bool isLocal { get; set; }
        public bool isCached { get; set; }
        public string icon { get; set; }
        public string[] versions { get; set; }
    }

    internal sealed class ProGetPackageVersionInfo
    {
        public string group { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public int downloads { get; set; }
        public bool isLocal { get; set; }
        public bool isCached { get; set; }
        public string icon { get; set; }
        public ProGetPackageFileInfo[] fileList { get; set; }
    }

    internal sealed class ProGetPackageFileInfo
    {
        public string name { get; set; }
        public long? size { get; set; }
        public DateTime? date { get; set; }
    }

    internal sealed class ProGetPackagePushData
    {
        public string Title { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public string[] Dependencies { get; set; }

        public string ToQueryString()
        {
            var buffer = new StringBuilder();
            buffer.Append('?');

            if (!string.IsNullOrEmpty(this.Title))
            {
                buffer.Append("title=");
                buffer.Append(Uri.EscapeDataString(this.Title));
                buffer.Append('&');
            }
            if (!string.IsNullOrEmpty(this.Icon))
            {
                buffer.Append("icon=");
                buffer.Append(Uri.EscapeDataString(this.Icon));
                buffer.Append('&');
            }
            if (!string.IsNullOrEmpty(this.Description))
            {
                buffer.Append("description=");
                buffer.Append(Uri.EscapeDataString(this.Description));
                buffer.Append('&');
            }
            if (this.Dependencies != null)
            {
                buffer.Append("dependencies=");
                bool first = true;
                foreach (string dependency in this.Dependencies)
                {
                    if (!first)
                        buffer.Append(',');

                    buffer.Append(dependency);
                    first = false;
                }
            }

            char trimChar = buffer[buffer.Length - 1];
            if (trimChar == '?' || trimChar == '&')
                buffer.Remove(buffer.Length - 1, 1);

            return buffer.ToString();
        }
    }

    internal sealed class PackageDeploymentData
    {

        public static PackageDeploymentData Create(IOperationExecutionContext context, ILogSink log, string description)
        {
            string baseUrl = SDK.BaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
            {
                log.LogDebug("Deployment will not be recorded in ProGet because the System.BaseUrl configuration setting is not set.");
                return null;
            }

            string serverName = AH.CoalesceString(context?.ServerName, Environment.MachineName);
            string relativeUrl;
            if (SDK.ProductName == "BuildMaster")
            {
                dynamic bmContext = context;
                relativeUrl = $"applications/{bmContext.ApplicationId}/builds/build?releaseNumber={Uri.EscapeDataString(bmContext.ReleaseNumber)}&buildNumber={Uri.EscapeDataString(bmContext.BuildNumber)}";
            }
            else
            {
                relativeUrl = "/deployment-sets/details?deploymentSetId=" + ((IStandardContext)context).DeploymentSetId;
            }

            return new PackageDeploymentData(SDK.ProductName, baseUrl, relativeUrl, serverName, description);
        }


        public PackageDeploymentData(string application, string baseUrl, string relativeUrl, string target, string description)
        {
            if (baseUrl == null)
                throw new ArgumentNullException(nameof(baseUrl));
            if (relativeUrl == null)
                throw new ArgumentNullException(nameof(relativeUrl));

            this.Application = application ?? throw new ArgumentNullException(nameof(application));
            this.Url = baseUrl.TrimEnd('/') + '/' + relativeUrl.TrimStart('/');
            this.Target = target ?? throw new ArgumentNullException(nameof(target));
            this.Description = description ?? "";
        }

        public string Application { get; }
        public string Description { get; }
        public string Url { get; }
        public string Target { get; }

        public static class Headers
        {
            public const string Application = "X-ProGet-Deployment-Application";
            public const string Description = "X-ProGet-Deployment-Description";
            public const string Url = "X-ProGet-Deployment-Url";
            public const string Target = "X-ProGet-Deployment-Target";
        }
    }
}
