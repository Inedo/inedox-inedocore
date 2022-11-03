namespace Inedo.Extensions.UniversalPackages;

#nullable enable

internal interface IFeedConfiguration
{
    string? PackageSourceName { get; set; }
    string? FeedName { get; set; }
    string? ApiUrl { get; set; }
    string? FeedUrl { get; set; }
    string? UserName { get; set; }
    string? Password { get; set; }
    string? ApiKey { get; set; }
}

internal interface IFeedPackageConfiguration : IFeedConfiguration
{
    string? PackageName { get; set; }
    string? PackageVersion { get; set; }
}

internal interface IFeedPackageInstallationConfiguration : IFeedPackageConfiguration
{
    string? TargetDirectory { get; set; }
    bool DirectDownload { get; set; }
    LocalRegistryOptions LocalRegistry { get; set; }
}

public enum LocalRegistryOptions
{
    None,
    Machine,
    User
}
