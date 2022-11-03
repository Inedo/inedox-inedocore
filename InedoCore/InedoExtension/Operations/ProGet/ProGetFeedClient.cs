using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inedo.Extensions.UniversalPackages;
using Inedo.UPack;
using Inedo.UPack.Net;

namespace Inedo.Extensions.Operations.ProGet;

internal sealed class ProGetFeedClient
{
    public static readonly LazyRegex ApiEndPointUrlRegex = new(@"(?<baseUrl>(https?://)?[^/]+)/(?<feedType>upack|nuget)(/?(?<feedName>[^/]+)/?)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    public string FeedApiEndpointUrl { get; }
    public string ProGetBaseUrl { get; }
    public string FeedName { get; }
    public string FeedType { get; }
    public SecureCredentials Credentials { get; }

    private ILogSink Log { get; }
    private CancellationToken CancellationToken { get; }

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

    public async Task RepackageAsync(IFeedPackageConfiguration packageConfig, string newVersion, string reason)
    {
        var packageId = UniversalPackageId.Parse(packageConfig.PackageName);

        var data = new Dictionary<string, string>
        {
            ["feed"] = this.FeedName,
            ["packageName"] = packageId.Name,
            ["version"] = packageConfig.PackageVersion,
            ["newVersion"] = newVersion
        };

        if (!string.IsNullOrWhiteSpace(packageId.Group))
            data["group"] = packageId.Group;

        if (!string.IsNullOrWhiteSpace(reason))
            data["comments"] = reason;

        using var client = this.CreateHttpClient();
        using var response = await client.PostAsync(this.ProGetBaseUrl + "/api/repackaging/repackage", new FormUrlEncodedContent(data), this.CancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await this.LogHttpErrorAsync(response).ConfigureAwait(false);
            return;
        }

        this.Log.LogInformation("Repackage was successful.");
    }
    public async Task PromoteAsync(IFeedPackageConfiguration packageConfig, string newFeed, string reason)
    {
        var packageId = UniversalPackageId.Parse(packageConfig.PackageName);

        var data = new Dictionary<string, string>
        {
            ["fromFeed"] = this.FeedName,
            ["packageName"] = packageId.Name,
            ["version"] = packageConfig.PackageVersion,
            ["toFeed"] = newFeed
        };

        if (!string.IsNullOrWhiteSpace(packageId.Group))
            data["group"] = packageId.Group;

        if (!string.IsNullOrWhiteSpace(reason))
            data["comments"] = reason;

        using var client = this.CreateHttpClient();
        using var response = await client.PostAsync(this.ProGetBaseUrl + "/api/promotions/promote", new FormUrlEncodedContent(data), this.CancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await this.LogHttpErrorAsync(response).ConfigureAwait(false);
            return;
        }

        this.Log.LogInformation("Repackage was successful.");
    }
    public async Task<string[]> GetFeedNamesAsync()
    {
        using var client = this.CreateHttpClient();
        using var response = await client.GetAsync(this.ProGetBaseUrl + "/upack?list-feeds", HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await this.LogHttpErrorAsync(response).ConfigureAwait(false);
            return Array.Empty<string>();
        }

        using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<string[]>(responseStream);
    }
    public Task<IReadOnlyList<RemoteUniversalPackage>> ListPackagesAsync() => this.CreateUPackClient().ListPackagesAsync(null, null, this.CancellationToken);
    public Task<IReadOnlyList<RemoteUniversalPackage>> ListPackagesAsync(string groupName, int maxCount) => this.CreateUPackClient().ListPackagesAsync(groupName, maxCount, this.CancellationToken);

    public Task<IReadOnlyList<RemoteUniversalPackageVersion>> ListPackageVersionsAsync(string packageName) => this.CreateUPackClient().ListPackageVersionsAsync(UniversalPackageId.Parse(packageName), false, null, this.CancellationToken);
    public Task<RemoteUniversalPackageVersion> FindPackageVersionAsync(IFeedPackageConfiguration config) => this.FindPackageVersionAsync(config.PackageName, config.PackageVersion);

    public async Task<RemoteUniversalPackageVersion> FindPackageVersionAsync(string packageName, string packageVersion)
    {
        var id = UniversalPackageId.Parse(packageName);
        var versions = await this.CreateUPackClient().ListPackageVersionsAsync(id, false, null, this.CancellationToken).ConfigureAwait(false);
        return FindPackageVersion(versions.OrderByDescending(v => v.Version), packageVersion);
    }
    public async Task<ProGetPackageVersionInfo> GetPackageVersionWithFilesAsync(UniversalPackageId id, UniversalPackageVersion version)
    {
        if (string.IsNullOrWhiteSpace(id?.Name))
            throw new ArgumentNullException(nameof(id));

        var url = $"{this.ProGetBaseUrl}/upack/{this.FeedName}/versions?group={Uri.EscapeDataString(id.Group)}&name={Uri.EscapeDataString(id.Name)}&version={Uri.EscapeDataString(version.ToString())}&includeFileList=true";

        var client = this.CreateHttpClient();
        return await client.GetFromJsonAsync(url, ProGetJsonContext.Default.ProGetPackageVersionInfo, this.CancellationToken).ConfigureAwait(false);
    }
    public async Task<Stream> DownloadPackageContentAsync(UniversalPackageId id, UniversalPackageVersion version, PackageDeploymentData deployInfo, Action<long, long> progressUpdate = null)
    {
        var url = Uri.EscapeDataString(id.Name) + "/" + Uri.EscapeDataString(version.ToString());
        if (!string.IsNullOrEmpty(id.Group))
            url = id.Group + "/" + url;

        var client = this.CreateHttpClient();
        if (deployInfo != null)
        {
            client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Application, deployInfo.Application ?? string.Empty);
            client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Description, deployInfo.Description ?? string.Empty);
            client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Url, deployInfo.Url ?? string.Empty);
            client.DefaultRequestHeaders.Add(PackageDeploymentData.Headers.Target, deployInfo.Target ?? string.Empty);
        }

        using var response = await client.GetAsync(this.FeedApiEndpointUrl + "download/" + url, HttpCompletionOption.ResponseHeadersRead, this.CancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var tempStream = TemporaryStream.Create(response.Content.Headers.ContentLength ?? 0L);
        await responseStream.CopyToAsync(
            tempStream,
            81920,
            this.CancellationToken,
            position => progressUpdate?.Invoke(position, response.Content.Headers.ContentLength ?? 0L)
        ).ConfigureAwait(false);

        tempStream.Position = 0;
        return tempStream;
    }

    public async Task<byte[]> UploadPackageAndComputeHashAsync(string fileName)
    {
        // start computing the hash in the background
        var computeHashTask = Task.Factory.StartNew(computePackageHash, TaskCreationOptions.LongRunning, this.CancellationToken);

        this.Log.LogDebug("Package source URL: " + this.FeedApiEndpointUrl);

        using (var fileStream = FileEx.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan | FileOptions.Asynchronous))
        {
            var client = this.CreateUPackClient();
            await client.UploadPackageAsync(fileStream, this.CancellationToken);
        }

        this.Log.LogDebug("Package uploaded.");

        this.Log.LogDebug("Waiting for package hash to be computed...");
        var hash = await computeHashTask;
        this.Log.LogDebug("Package SHA1: " + new HexString(hash));
        return hash;

        byte[] computePackageHash(object arts)
        {
            using var fileStream = FileEx.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
            using var sha1 = SHA1.Create();
            return sha1.ComputeHash(fileStream);
        }
    }
    public async Task<byte[]> UploadVirtualPackageAndComputeHashAsync(string fileName)
    {
        var tempFileName = Path.GetTempFileName();
        try
        {
            using (var vpackStream = File.OpenRead(fileName))
            using (var tempZip = new ZipArchive(File.Create(tempFileName), ZipArchiveMode.Create))
            {
                var upackEntry = tempZip.CreateEntry("upack.json");
                using var upackStream = upackEntry.Open();
                vpackStream.CopyTo(upackStream);
            }

            return await this.UploadPackageAndComputeHashAsync(tempFileName);
        }
        finally
        {
            File.Delete(tempFileName);
        }
    }
    private UniversalFeedClient CreateUPackClient()
    {
        var t = new DefaultApiTransport { HttpClientFactory = r => SDK.CreateHttpClient() };

        if (this.Credentials is TokenCredentials tcreds)
            return new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(this.FeedApiEndpointUrl), "api", tcreds.Token), t);
        else if (this.Credentials is UsernamePasswordCredentials upcreds)
            return new UniversalFeedClient(new UniversalFeedEndpoint(new Uri(this.FeedApiEndpointUrl), upcreds.UserName, upcreds.Password), t);
        else
            return new UniversalFeedClient(new UniversalFeedEndpoint(this.FeedApiEndpointUrl), t);
    }
    private HttpClient CreateHttpClient()
    {
        var client = SDK.CreateHttpClient();

        if (this.Credentials is TokenCredentials tcreds)
        {
            this.Log.LogDebug("Making request with API Key...");
            client.DefaultRequestHeaders.Add("X-ApiKey", AH.Unprotect(tcreds.Token));
        }
        else if (this.Credentials is UsernamePasswordCredentials upcreds)
        {
            this.Log.LogDebug($"Making request as {upcreds.UserName}...");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes(upcreds.UserName + ":" + AH.Unprotect(upcreds.Password))));
        }

        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    public Task<Stream> GetPackageStreamAsync(UniversalPackageId id, UniversalPackageVersion version) => this.CreateUPackClient().GetPackageStreamAsync(id, version, this.CancellationToken);
    public Task<Stream> GetPackageStreamAsync(IFeedPackageConfiguration config) => this.GetPackageStreamAsync(UniversalPackageId.Parse(config.PackageName), UniversalPackageVersion.Parse(config.PackageVersion));

    private async Task LogHttpErrorAsync(HttpResponseMessage response)
    {
        var message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.InternalServerError && message.StartsWith("<!DOCTYPE"))
            message = "Invalid feed URL. Ensure the feed URL follows the format: http://{proget-server}/upack/{feed-name}";

        this.Log.LogError(message);
    }

    private static readonly LazyRegex FindVersionRegex = new(@"^(?<1>[0-9]+)(\.(?<2>[0-9]+)(?<3>\.([0-9]+(-.+)?)?)?)?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    private static RemoteUniversalPackageVersion FindPackageVersion(IEnumerable<RemoteUniversalPackageVersion> packages, string packageVersion)
    {
        if (string.Equals(packageVersion, "latest", StringComparison.OrdinalIgnoreCase))
        {
            return packages.FirstOrDefault();
        }
        else if (string.Equals(packageVersion, "latest-stable", StringComparison.OrdinalIgnoreCase))
        {
            return packages.FirstOrDefault(v => string.IsNullOrEmpty(v.Version.Prerelease));
        }

        var match = FindVersionRegex.Match(packageVersion ?? "");
        if (match.Groups[1].Success && !match.Groups[3].Success)
        {
            var major = BigInteger.Parse(match.Groups[1].Value);

            if (match.Groups[2].Success)
            {
                var minor = BigInteger.Parse(match.Groups[2].Value);
                return packages.FirstOrDefault(v => v.Version.Major == major && v.Version.Minor == minor);
            }

            return packages.FirstOrDefault(v => v.Version.Major == major);
        }

        var semver = UniversalPackageVersion.Parse(packageVersion);
        return packages.FirstOrDefault(v => v.Version == semver);
    }

    internal sealed class PackageDeploymentData
    {
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
        public PackageDeploymentData(string application, string url, string target, string description)
        {
            this.Application = application ?? throw new ArgumentNullException(nameof(application));
            this.Url = url ?? throw new ArgumentNullException(nameof(url));
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
