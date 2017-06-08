using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Reflection;
using System.Text;
using Inedo.IO;
using Inedo.Diagnostics;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Runtime.InteropServices;
#if BuildMaster
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Data;
#elif Otter
using Inedo.Otter;
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    internal sealed class ProGetClient
    {
        private static readonly LazyRegex FeedNameRegex = new LazyRegex(@"(?<1>(https?://)?[^/]+)/upack(/?(?<2>.+))", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public ProGetClient(string serverUrl, string feedName, string userName, string password, ILogger log = null)
        {
            if (string.IsNullOrEmpty(serverUrl))
                throw new ProGetException(400, "A ProGet server URL must be specified for this operation either in the operation itself or in the credential.");

            this.UserName = AH.NullIf(userName, string.Empty);
            this.Password = AH.NullIf(password, string.Empty);
            this.Log = log ?? Logger.Null;
            var result = ResolveFeedNameAndUrl(serverUrl, feedName, this.Log);
            this.FeedUrl = result.feedUrl;
            this.FeedName = result.feedName;
            this.ServerUrl = serverUrl.TrimEnd('/') + '/';
        }

        public string FeedUrl { get; }
        public string FeedName { get; }
        public string ServerUrl { get; }
        public string UserName { get; }
        public string Password { get; }
        public ILogger Log { get; }

        public string GetViewPackageUrl(PackageName id, string version)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));

            return $"{this.ServerUrl }feeds/{Uri.EscapeDataString(this.FeedName)}/{id.ToString()}/{version}";
        }

        public async Task<string[]> GetFeedNamesAsync()
        {
            using (var client = this.CreateClient())
            using (var response = await client.GetAsync(this.FeedUrl + "?list-feeds").ConfigureAwait(false))
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
            using (var response = await client.GetAsync(this.FeedUrl + "packages").ConfigureAwait(false))
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
            using (var response = await client.GetAsync(this.FeedUrl + $"packages?group={Uri.EscapeDataString(id.Group)}&name={Uri.EscapeDataString(id.Name)}").ConfigureAwait(false))
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
            {
                using (var response = await client.GetAsync(this.FeedUrl + $"versions?group={Uri.EscapeDataString(id.Group)}&name={Uri.EscapeDataString(id.Name)}&version={Uri.EscapeDataString(version)}&includeFileList=true").ConfigureAwait(false))
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
        }
        public async Task<Stream> DownloadPackageContentAsync(PackageName id, string version, PackageDeploymentData deployInfo)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version));

            var url = Uri.EscapeDataString(id.Name) + "/" + Uri.EscapeDataString(version);
            if (!string.IsNullOrEmpty(id.Group))
                url = id.Group + "/" + url;

            using (var client = this.CreateClient(deployInfo))
            {
                using (var response = await client.GetAsync(this.FeedUrl + "download/" + url).ConfigureAwait(false))
                {
                    await HandleError(response).ConfigureAwait(false);

                    using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        var tempStream = TemporaryStream.Create(response.Content.Headers.ContentLength ?? 0L);
                        await responseStream.CopyToAsync(tempStream).ConfigureAwait(false);
                        tempStream.Position = 0;
                        return tempStream;
                    }
                }
            }
        }
        public async Task<ZipArchive> DownloadPackageAsync(PackageName id, string version, PackageDeploymentData deployInfo)
        {
            var stream = await this.DownloadPackageContentAsync(id, version, deployInfo);
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

                using (var response = await client.PostAsync(this.FeedUrl + "upload/" + url + packageData.ToQueryString(), streamContent).ConfigureAwait(false))
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

                    using (var response = await client.PostAsync(this.ServerUrl + "api/promotions/promote", streamContent).ConfigureAwait(false))
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
            
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(typeof(Operation).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product, typeof(Operation).Assembly.GetName().Version.ToString()));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InedoCore", typeof(ProGetClient).Assembly.GetName().Version.ToString()));

            if (apiKey != null)
            {
                client.DefaultRequestHeaders.Add("X-ApiKey", apiKey.ToUnsecureString());
            }

            if (deployInfo != null)
            {
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Application, deployInfo.Application ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Description, deployInfo.Description ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Url, deployInfo.Url ?? string.Empty);
                client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Target, deployInfo.Target ?? string.Empty);
            }

            return client;
        }
        private HttpWebRequest CreateRequest(string relativePath, PackageDeploymentData deployInfo = null)
        {
            string url = this.FeedUrl + relativePath;
            this.Log.LogDebug("Creating request: " + url);

            var asm = typeof(Operation).Assembly;
            var request = WebRequest.CreateHttp(url);
            request.UserAgent = $"{asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product} {asm.GetName().Version} ({Environment.OSVersion})";
            request.AutomaticDecompression = DecompressionMethods.GZip;
            if (!string.IsNullOrEmpty(this.UserName) && !string.IsNullOrEmpty(this.Password))
            {
                this.Log.LogDebug($"Using Basic Authentication; user name '{this.UserName}'.");
                request.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes(this.UserName + ":" + this.Password)));
            }
            else
            {
                this.Log.LogDebug($"Using integrated authentication; user account '{Environment.UserName}', domain '{Environment.UserDomainName}'.");
                request.UseDefaultCredentials = true;
                request.PreAuthenticate = true;
            }
            
            if (deployInfo != null)
            {
                request.Headers.Add(PackageDeploymentData.Headers.Application, deployInfo.Application);
                request.Headers.Add(PackageDeploymentData.Headers.Description, deployInfo.Description);
                request.Headers.Add(PackageDeploymentData.Headers.Url, deployInfo.Url);
                request.Headers.Add(PackageDeploymentData.Headers.Target, deployInfo.Target);
            }

            return request;
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
        private static (string feedUrl, string feedName) ResolveFeedNameAndUrl(string baseUrl, string feedName, ILogger log)
        {
            var match = FeedNameRegex.Match(baseUrl);

            if (match.Success)
            {
                log.LogWarning("Specific ProGet feed URLs should no longer be used in ProGet resource credentials. Instead, use the server or hostname only (i.e. 'http://proget-server:81' instead of 'http://proget-server:81/upack/feedName') and update the Get/Ensure-Package operation to include the name of the feed via the Feed property.");
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

        public static ProGetException Wrap(WebException ex)
        {
            var response = (HttpWebResponse)ex.Response;
            string message;
            try
            {
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, InedoLib.UTF8Encoding))
                {
                    message = reader.ReadToEnd();

                    if (response.StatusCode == HttpStatusCode.InternalServerError && message.StartsWith("<!DOCTYPE"))
                    {
                        message = "Invalid feed URL. Ensure the feed URL follows the format: http://{proget-server}/upack/{feed-name}";
                    }
                }
            }
            catch
            {
                message = "Unknown error.";
            }

            return new ProGetException((int)response.StatusCode, message, ex);
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
#if BuildMaster
        public static PackageDeploymentData Create(IOperationExecutionContext context, ILogger log, string description)
        {
            string baseUrl = DB.Configuration_GetValue("CoreEx", "BuildMaster_BaseUrl");
            if (string.IsNullOrEmpty(baseUrl))
            {
                log.LogDebug("Deployment will not be recorded in ProGet because the BuildMaster_BaseUrl configuration setting is not set.");
                return null;
            }

            var server = DB.Servers_GetServer(context.ServerId).Servers.FirstOrDefault();
            string serverName = server?.Server_Name ?? Environment.MachineName;

            string relativeUrl = $"applications/{context.ApplicationId}/builds/build?releaseNumber={Uri.EscapeDataString(context.ReleaseNumber)}&buildNumber={Uri.EscapeDataString(context.BuildNumber)}";

            return new PackageDeploymentData("BuildMaster", baseUrl, relativeUrl, serverName, description);
        }
#elif Otter
        public static PackageDeploymentData Create(IOperationExecutionContext context, ILogger log, string description)
        {
            if (string.IsNullOrEmpty(OtterConfig.OtterBaseUrl))
            {
                log.LogDebug("Deployment will not be recorded in ProGet because the OtterBaseUrl configuration setting is not set.");
                return null;
            }

            string relativeUrl = $"servers/details?serverId={context.ServerId}";

            return new PackageDeploymentData("Otter", OtterConfig.OtterBaseUrl, relativeUrl, context.ServerName, description);
        }
#endif

        public PackageDeploymentData(string application, string baseUrl, string relativeUrl, string target, string description)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));
            if (baseUrl == null)
                throw new ArgumentNullException(nameof(baseUrl));
            if (relativeUrl == null)
                throw new ArgumentNullException(nameof(relativeUrl));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            this.Application = application;
            this.Url = baseUrl.TrimEnd('/') + '/' + relativeUrl.TrimStart('/');
            this.Target = target;
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

#if Otter
    internal static class SecureStringExtensions
    {
        public static string ToUnsecureString(this SecureString s)
        {
            if (s == null)
                return null;

            var str = IntPtr.Zero;
            try
            {
                str = Marshal.SecureStringToGlobalAllocUnicode(s);
                return Marshal.PtrToStringUni(str, s.Length);
            }
            finally
            {
                if (str != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(str);
            }
        }
    }
#endif
}
