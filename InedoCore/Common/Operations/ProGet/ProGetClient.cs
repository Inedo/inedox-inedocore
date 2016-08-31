using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using Inedo.IO;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;
using Inedo.Diagnostics;
#if BuildMaster
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    internal sealed class ProGetClient
    {
        public ProGetClient(string feedUrl, string userName, string password, ILogger log)
        {
            if (string.IsNullOrEmpty(feedUrl))
                throw new ProGetException(400, "A feed URL must be specified for this operation either in the operation itself or in the credential.");
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            this.FeedUrl = feedUrl.TrimEnd('/') + "/";
            this.UserName = AH.NullIf(userName, string.Empty);
            this.Password = AH.NullIf(password, string.Empty);
            this.Log = log;
        }

        public string FeedUrl { get; }
        public string UserName { get; }
        public string Password { get; }
        public ILogger Log { get; }

        public async Task<ProGetPackageInfo> GetPackageInfoAsync(string group, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            var request = this.CreateRequest($"packages?group={Uri.EscapeDataString(group ?? string.Empty)}&name={Uri.EscapeDataString(name)}");
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
        public async Task<ProGetPackageVersionInfo> GetPackageVersionInfoAsync(string group, string name, string version)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            var request = this.CreateRequest($"versions?group={Uri.EscapeDataString(group ?? string.Empty)}&name={Uri.EscapeDataString(name)}&version={Uri.EscapeDataString(version)}&includeFileList=true");
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
        public async Task<ZipArchive> DownloadPackageAsync(string group, string name, string version)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version));

            var url = Uri.EscapeDataString(name) + "/" + Uri.EscapeDataString(version);
            if (!string.IsNullOrEmpty(group))
                url = group + "/" + url;

            var request = this.CreateRequest("download/" + url);
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

        private HttpWebRequest CreateRequest(string relativePath)
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

            return request;
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
}
