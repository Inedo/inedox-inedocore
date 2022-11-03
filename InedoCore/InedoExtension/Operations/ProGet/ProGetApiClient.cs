using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Inedo.Extensions.UniversalPackages;
using Inedo.UPack;
using Inedo.UPack.Net;

#nullable enable

namespace Inedo.Extensions.Operations.ProGet;

internal sealed class ProGetApiClient
{
    private readonly HttpClient http;
    private readonly string feedName;
    private readonly ILogSink? log;
    private readonly UniversalFeedClient upackClient;

    public ProGetApiClient(IFeedConfiguration config, ILogSink? log = null)
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

        this.log.LogInformation("Repackage was successful.");
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

        this.log.LogInformation("Promotion was successful.");
    }
    public async IAsyncEnumerable<string> ListPackagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var p in this.upackClient.EnumeratePackagesAsync(null, null, cancellationToken))
            yield return p.FullName.ToString();
    }
    public async IAsyncEnumerable<string> ListPackageVersionsAsync(string packageId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var v in await this.upackClient.ListPackageVersionsAsync(UniversalPackageId.Parse(packageId), cancellationToken: cancellationToken))
            yield return v.Version.ToString();
    }

    private async Task LogHttpErrorAsync(HttpResponseMessage response)
    {
        var message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.InternalServerError && message.StartsWith("<!DOCTYPE"))
            message = response.ReasonPhrase;
        else
            message = $"{response.ReasonPhrase}: {message}";

        this.log.LogError(message);
    }
}
