using System.Text.Json.Serialization;

namespace Inedo.Extensions.Operations.ProGet;

#nullable enable

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProGetPackageVersionInfo))]
[JsonSerializable(typeof(ProGetRepackageInfo))]
[JsonSerializable(typeof(ProGetPromotionInfo))]
internal sealed partial class ProGetJsonContext : JsonSerializerContext
{
}

internal sealed class ProGetPackageVersionInfo
{
    public string? Group { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int Downloads { get; set; }
    public bool? IsLocal { get; set; }
    public bool? IsCached { get; set; }
    public string? Icon { get; set; }
    public ProGetPackageFileInfo[]? FileList { get; set; }
}

internal sealed class ProGetPackageFileInfo
{
    public string? Name { get; set; }
    public long? Size { get; set; }
    public DateTime? Date { get; set; }
}

internal sealed class ProGetRepackageInfo
{
    public string? Feed { get; set; }
    public string? Group { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? NewVersion { get; set; }
    public string? Comments { get; set; }
    public string? ToFeed { get; set; }
}

internal sealed class ProGetPromotionInfo
{
    public string? PackageName { get; set; }
    public string? GroupName { get; set; }
    public string? Version { get; set; }
    public string? FromFeed { get; set; }
    public string? ToFeed { get; set; }
    public string? Comments { get; set; }
}
