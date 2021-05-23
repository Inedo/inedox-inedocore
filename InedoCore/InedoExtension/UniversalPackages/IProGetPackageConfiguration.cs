namespace Inedo.Extensions.UniversalPackages
{
    internal interface IFeedPackageConfiguration
    {
        string PackageSourceName { get; }
        string FeedName { get; }
        string FeedUrl { get; }
        string UserName { get; }
        string Password { get; }
        string ApiKey { get; }

        string PackageName { get; }
        string PackageVersion { get; }
    }
}
