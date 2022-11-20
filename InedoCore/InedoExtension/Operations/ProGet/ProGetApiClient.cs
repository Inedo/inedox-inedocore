using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Inedo.Extensions.UniversalPackages;
using Inedo.UPack;
using Inedo.UPack.Net;

#nullable enable

namespace Inedo.Extensions.Operations.ProGet;

internal sealed class ProGetFeedClient
{
    private static readonly LazyRegex FindVersionRegex = new(@"^(?<1>[0-9]+)(\.(?<2>[0-9]+)(?<3>\.([0-9]+(-.+)?)?)?)?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    private readonly HttpClient http;
    private readonly string feedName;
    private readonly ILogSink? log;
    private readonly UniversalFeedClient upackClient;

    public ProGetFeedClient(IFeedConfiguration config, ILogSink? log = null)
    {
        if (string.IsNullOrEmpty(config.ApiUrl))
            throw new ArgumentException("ApiUrl is required.");
        if (string.IsNullOrEmpty(config.FeedName))
            throw new ArgumentException("FeedName is required.");

        var apiUrl = config.ApiUrl;

        if (!apiUrl.EndsWith('/'))
            apiUrl += "/";

        this.http = SDK.CreateHttpClient();
        this.http.BaseAddress = new Uri(apiUrl);

        UniversalFeedEndpoint? upackEndpoint = null;
        var upackUrl = $"{apiUrl}upack/{Uri.EscapeDataString(config.FeedName)}";

        if (!string.IsNullOrEmpty(config.UserName))
        {
            this.http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes($"{config.UserName}:{config.Password}")));
            upackEndpoint = new UniversalFeedEndpoint(new Uri(upackUrl), config.UserName, AH.CreateSecureString(config.Password)!);
        }

        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            this.http.DefaultRequestHeaders.Add("X-ApiKey", config.ApiKey);
            upackEndpoint = new UniversalFeedEndpoint(new Uri(upackUrl), "api", AH.CreateSecureString(config.ApiKey)!);
        }

        upackEndpoint ??= new UniversalFeedEndpoint(upackUrl);

        this.feedName = config.FeedName;
        this.log = log;

        this.upackClient = new UniversalFeedClient(upackEndpoint, new DefaultApiTransport { HttpClientFactory = r => SDK.CreateHttpClient() });
    }

    public async Task RepackageAsync(string packageId, string version, string newVersion, string? reason, string? toFeed, CancellationToken cancellationToken = default)
    {
        var id = UniversalPackageId.Parse(packageId);

        var data = new ProGetRepackageInfo
        {
            Feed = this.feedName,
            Name = id.Name,
            Group = id.Group,
            Version = version,
            NewVersion = newVersion,
            Comments = reason,
            ToFeed = toFeed
        };

        using var response = await this.http.PostAsJsonAsync("api/repackaging/repackage", data, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await this.LogHttpErrorAsync(response);
            return;
        }

        this.log?.LogInformation("Repackage was successful.");
    }
    public async Task PromoteAsync(string packageId, string version, string newFeed, string? reason, CancellationToken cancellationToken = default)
    {
        var id = UniversalPackageId.Parse(packageId);

        var data = new ProGetPromotionInfo
        {
            FromFeed = this.feedName,
            PackageName = id.Name,
            GroupName = id.Group,
            Version = version,
            ToFeed = newFeed,
            Comments = reason
        };

        using var response = await this.http.PostAsJsonAsync("api/promotions/promote", data, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await this.LogHttpErrorAsync(response);
            return;
        }

        this.log?.LogInformation("Promotion was successful.");
    }
    public async IAsyncEnumerable<string> ListPackagesAsync(string? group = null, int? maxCount = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var p in this.upackClient.EnumeratePackagesAsync(group, maxCount, cancellationToken))
            yield return p.FullName.ToString();
    }
    public async IAsyncEnumerable<string> ListPackageVersionsAsync(string packageId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var v in await this.upackClient.ListPackageVersionsAsync(UniversalPackageId.Parse(packageId), cancellationToken: cancellationToken))
            yield return v.Version.ToString();
    }

    public async Task<RemoteUniversalPackageVersion?> FindPackageVersionAsync(string packageName, string packageVersion, CancellationToken cancellationToken = default)
    {
        var id = UniversalPackageId.Parse(packageName);
        var versions = await this.upackClient.ListPackageVersionsAsync(id, cancellationToken: cancellationToken).ConfigureAwait(false);
        return FindPackageVersion(versions.OrderByDescending(v => v.Version), packageVersion);
    }
    public async Task<IReadOnlyList<ProGetPackageFileInfo>> GetPackageFilesAsync(UniversalPackageId id, UniversalPackageVersion version, CancellationToken cancellationToken = default)
    {
        var package = await this.upackClient.GetPackageVersionAsync(id, version, true, cancellationToken);
        if (package == null)
            throw new ExecutionFailureException($"Package {id} {version} not found.");

        var files = new List<ProGetPackageFileInfo>();
        var fileList = (object[])package.AllProperties["fileList"];
        foreach (var f in fileList)
        {
            var dict = (IReadOnlyDictionary<string, object>)f;
            var name = dict["name"].ToString();
            long? size = null;
            if (dict.TryGetValue("size", out var sizeObj))
                size = long.Parse(sizeObj.ToString()!);
            var date = DateTimeOffset.Parse(dict["date"].ToString()!);
            files.Add(new ProGetPackageFileInfo { Name = name, Size = size, Date = date.UtcDateTime });
        }

        return files;
    }

    public async Task<byte[]> UploadPackageAndComputeHashAsync(string fileName, CancellationToken cancellationToken = default)
    {
        // start computing the hash in the background
        var computeHashTask = Task.Factory.StartNew(computePackageHash, TaskCreationOptions.LongRunning, cancellationToken);

        this.log?.LogDebug($"Package source URL: {this.upackClient.Endpoint.Uri}");

        using (var fileStream = FileEx.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan | FileOptions.Asynchronous))
        {
            await this.upackClient.UploadPackageAsync(fileStream, cancellationToken);
        }

        this.log?.LogDebug("Package uploaded.");

        this.log?.LogDebug("Waiting for package hash to be computed...");
        var hash = await computeHashTask;
        this.log?.LogDebug($"Package SHA1: {new HexString(hash)}");
        return hash;

        byte[] computePackageHash(object? arts)
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

    public async Task<Stream> GetPackageStreamAsync(RemoteUniversalPackageVersion version, CancellationToken cancellationToken = default)
    {
        return await (this.upackClient.GetPackageStreamAsync(version.FullName, version.Version, cancellationToken))
            ?? throw new ExecutionFailureException($"Package {version.FullName} {version.Version} not found.");
    }

    private static RemoteUniversalPackageVersion? FindPackageVersion(IEnumerable<RemoteUniversalPackageVersion> packages, string packageVersion)
    {
        if (string.Equals(packageVersion, "latest", StringComparison.OrdinalIgnoreCase))
            return packages.FirstOrDefault();
        else if (string.Equals(packageVersion, "latest-stable", StringComparison.OrdinalIgnoreCase))
            return packages.FirstOrDefault(v => string.IsNullOrEmpty(v.Version.Prerelease));

        var match = FindVersionRegex.Match(packageVersion ?? string.Empty);
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

    private async Task LogHttpErrorAsync(HttpResponseMessage response)
    {
        var message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.InternalServerError && message.StartsWith("<!DOCTYPE"))
            message = response.ReasonPhrase;
        else
            message = $"{response.ReasonPhrase}: {message}";

        this.log?.LogError(message!);
    }
}
