using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using Inedo.IO;
using Newtonsoft.Json;
using System.Reflection;
#if BuildMaster
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations.ProGet
{
    internal sealed class ProGetClient
    {
        public ProGetClient(string feedUrl, string userName, string password)
        {
            if (string.IsNullOrEmpty(feedUrl))
                throw new ArgumentNullException(nameof(feedUrl));

            this.FeedUrl = feedUrl.TrimEnd('/') + "/";
            this.UserName = AH.NullIf(userName, string.Empty);
            this.Password = AH.NullIf(password, string.Empty);
        }

        public string FeedUrl { get; }
        public string UserName { get; }
        public string Password { get; }

        public async Task<ProGetPackageInfo> GetPackageInfoAsync(string group, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            var request = this.CreateRequest($"packages?group={Uri.EscapeDataString(group ?? string.Empty)}&name={Uri.EscapeDataString(name)}");
            using (var response = await request.GetResponseAsync().ConfigureAwait(false))
            using (var responseStream = response.GetResponseStream())
            using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var serializer = JsonSerializer.Create();
                return serializer.Deserialize<ProGetPackageInfo>(jsonReader);
            }
        }
        public async Task<ProGetPackageVersionInfo> GetPackageVersionInfoAsync(string group, string name, string version)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            var request = this.CreateRequest($"versions?group={Uri.EscapeDataString(group ?? string.Empty)}&name={Uri.EscapeDataString(name)}&version={Uri.EscapeDataString(version)}&includeFileList=true");
            using (var response = await request.GetResponseAsync().ConfigureAwait(false))
            using (var responseStream = response.GetResponseStream())
            using (var streamReader = new StreamReader(responseStream, InedoLib.UTF8Encoding))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var serializer = JsonSerializer.Create();
                return serializer.Deserialize<ProGetPackageVersionInfo>(jsonReader);
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
            using (var response = await request.GetResponseAsync().ConfigureAwait(false))
            using (var responseStream = response.GetResponseStream())
            {
                var tempStream = TemporaryStream.Create(response.ContentLength);
                await responseStream.CopyToAsync(tempStream).ConfigureAwait(false);
                tempStream.Position = 0;
                return new ZipArchive(tempStream, ZipArchiveMode.Read);
            }
        }

        public async Task PushPackageAsync(string group, string name, string version, ProGetPackagePushData packageData)
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

            var request = this.CreateRequest("upload/" + url);
            request.Method = "POST";
            request.ContentType = "application/json";
            using (var stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
            using (var writer = new StreamWriter(stream, InedoLib.UTF8Encoding))
            using (var json = new JsonTextWriter(writer))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(json, packageData);
            }

            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                {
                }
            }
            catch (WebException wex)
            {
                using (var responseStream = wex.Response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    string message = reader.ReadToEnd();
                    throw new WebException(message, wex, wex.Status, wex.Response);
                }
            }
        }

        private HttpWebRequest CreateRequest(string relativePath)
        {
            var asm = typeof(Operation).Assembly;
            var request = WebRequest.CreateHttp(this.FeedUrl + relativePath);
            request.UserAgent = $"{asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product} {asm.GetName().Version} ({Environment.OSVersion})";
            request.AutomaticDecompression = DecompressionMethods.GZip;
            if (!string.IsNullOrEmpty(this.UserName) && !string.IsNullOrEmpty(this.Password))
            {
                request.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes(this.UserName + ":" + this.Password)));
            }
            else
            {
                request.UseDefaultCredentials = true;
                request.PreAuthenticate = true;
            }

            return request;
        }
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
        public string title { get; set; }
        public string icon { get; set; }
        public string description { get; set; }
        public string[] dependencies { get; set; }
        [JsonProperty("content-b64")]
        public string contentBase64 { get; set; }
    }
}
