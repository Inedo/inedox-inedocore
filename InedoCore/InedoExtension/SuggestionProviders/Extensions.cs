using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Configurations.ProGet;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Extensions.UniversalPackages;

namespace Inedo.Extensions.SuggestionProviders
{
    internal static class Extensions
    {
        public static ProGetFeedClient TryCreateProGetFeedClient(this IComponentConfiguration config)
        {
            var packageConfig = new ProGetPackageConfiguration
            {
                PackageSourceName = config[nameof(IFeedPackageConfiguration.PackageSourceName)],
                FeedName = config[nameof(IFeedPackageConfiguration.FeedName)],
                FeedUrl = config[nameof(IFeedPackageConfiguration.FeedUrl)],
                UserName = config[nameof(IFeedPackageConfiguration.UserName)],
                Password = config[nameof(IFeedPackageConfiguration.Password)],
                ApiKey = config[nameof(IFeedPackageConfiguration.ApiKey)]
            };
            return packageConfig.TryCreateProGetFeedClient(config.EditorContext as ICredentialResolutionContext ?? CredentialResolutionContext.None);
        }
    }
}