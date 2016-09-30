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
#if BuildMaster
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Data;
#elif Otter
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
            this.Log = log ?? new NullLogger();
            this.FeedUrl = ResolveFeedUrl(serverUrl, feedName, this.Log);
        }

        private static string ResolveFeedUrl(string baseUrl, string feedName, ILogger log)
        {
            var match = FeedNameRegex.Match(baseUrl);
            
            if (match.Success)
            {
                log.LogWarning("As of v5.4 of the ProGet extension, specific ProGet feed URLs should not be used in the ProGet resource credential. Instead, use the server or hostname only (i.e. 'http://proget-server:81' instead of 'http://proget-server:81/upack/feedName') and update the Get/Ensure-Package operation to include the name of the feed via the Feed property.");
                string credentialUrl = match.Groups[1].Value;
                string credentialFeedName = match.Groups[2].Value;
                string resolvedFeedName = AH.CoalesceString(Uri.EscapeUriString(feedName ?? ""), credentialFeedName);

                return credentialUrl + "/upack/" + resolvedFeedName.TrimEnd('/') + '/';
            }
            else
            {
                return baseUrl.TrimEnd('/') + "/upack/" + Uri.EscapeUriString(feedName ?? "") + '/';
            }
        }

        public string FeedUrl { get; }
        public string UserName { get; }
        public string Password { get; }
        public ILogger Log { get; }

        public async Task<string[]> GetFeedNamesAsync()
        {
            var request = this.CreateRequest("?list-feeds");
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var responseStream = response.GetResponseStream())
                using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    return serializer.Deserialize<string[]>(jsonReader);
                }
            }
            catch (WebException wex)
            {
                throw ProGetException.Wrap(wex);
            }
        }

        public async Task<ProGetPackageInfo[]> GetPackagesAsync()
        {
            var request = this.CreateRequest("packages");
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var responseStream = response.GetResponseStream())
                using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    return serializer.Deserialize<ProGetPackageInfo[]>(jsonReader);
                }
            }
            catch (WebException wex)
            {
                throw ProGetException.Wrap(wex);
            }
        }

        public async Task<ProGetPackageInfo> GetPackageInfoAsync(PackageName id)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));

            var request = this.CreateRequest($"packages?group={Uri.EscapeDataString(id.Group)}&name={Uri.EscapeDataString(id.Name)}");
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var responseStream = response.GetResponseStream())
                using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    return serializer.Deserialize<ProGetPackageInfo>(jsonReader);
                }
            }
            catch (WebException wex)
            {
                throw ProGetException.Wrap(wex);
            }
        }
        public async Task<ProGetPackageVersionInfo> GetPackageVersionInfoAsync(PackageName id, string version)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));

            var request = this.CreateRequest($"versions?group={Uri.EscapeDataString(id.Group)}&name={Uri.EscapeDataString(id.Name)}&version={Uri.EscapeDataString(version)}&includeFileList=true");
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var responseStream = response.GetResponseStream())
                using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    return serializer.Deserialize<ProGetPackageVersionInfo>(jsonReader);
                }
            }
            catch (WebException wex)
            {
                throw ProGetException.Wrap(wex);
            }
        }
        public async Task<ZipArchive> DownloadPackageAsync(PackageName id, string version, PackageDeploymentData deployInfo)
        {
            if (string.IsNullOrWhiteSpace(id?.Name))
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version));

            var url = Uri.EscapeDataString(id.Name) + "/" + Uri.EscapeDataString(version);
            if (!string.IsNullOrEmpty(id.Group))
                url = id.Group + "/" + url;

            var request = this.CreateRequest("download/" + url, deployInfo);
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                using (var responseStream = response.GetResponseStream())
                {
                    var tempStream = TemporaryStream.Create(response.ContentLength);
                    await responseStream.CopyToAsync(tempStream).ConfigureAwait(false);
                    tempStream.Position = 0;
                    return new ZipArchive(tempStream, ZipArchiveMode.Read);
                }
            }
            catch (WebException wex)
            {
                throw ProGetException.Wrap(wex);
            }
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

            var request = this.CreateRequest("upload/" + url + packageData.ToQueryString());
            request.Method = "POST";
            request.ContentType = "application/zip";
            try
            {
                using (var stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await content.CopyToAsync(stream).ConfigureAwait(false);
                }

                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                {
                }
            }
            catch (WebException wex)
            {
                throw ProGetException.Wrap(wex);
            }
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

    internal sealed class NullLogger : ILogger
    {
#pragma warning disable CS0067
        public event EventHandler<LogMessageEventArgs> MessageLogged;
#pragma warning restore CS0067

        public void Log(MessageLevel logLevel, string message)
        {
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
            // this can be changed to use OtterConfig class when Otter SDK is updated to v1.4
            var config = DB.Configuration_GetValues().FirstOrDefault(v => v.Key_Name == "OtterConfig.OtterBaseUrl");
            if (config == null)
            {
                log.LogDebug("Deployment will not be recorded in ProGet because the OtterBaseUrl configuration setting is not set.");
                return null;
            }

            string relativeUrl = $"servers/details?serverId={context.ServerId}";

            return new PackageDeploymentData("Otter", config.Value_Text, relativeUrl, context.ServerName, description);
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
}
