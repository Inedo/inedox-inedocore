namespace Inedo.Extensions.UniversalPackages
{
    internal interface IProGetPackageInstallTemplate
    {
        string CredentialName { get; }
        string FeedName { get; }
        string PackageName { get; }
        string PackageVersion { get; }
        bool DeleteExtra { get; }
        string TargetDirectory { get; }
        string FeedUrl { get; }
        string UserName { get; }
        string Password { get; }
    }
}
