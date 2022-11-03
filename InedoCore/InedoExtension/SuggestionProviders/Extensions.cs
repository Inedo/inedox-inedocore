using Inedo.Extensions.Configurations.ProGet;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Extensions.UniversalPackages;

#nullable enable

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

            return packageConfig.TryCreateProGetFeedClient(config.EditorContext as ICredentialResolutionContext ?? CredentialResolutionContext.None)!;
        }

        public static async ValueTask<ProGetApiClient?> TryCreateProGetFeedClientAsync(this IComponentConfiguration config, CancellationToken cancellationToken = default)
        {
            try
            {
                var packageConfig = new ProGetPackageConfiguration
                {
                    PackageSourceName = config["PackageSource"],
                    FeedName = config[nameof(IFeedPackageConfiguration.FeedName)],
                    FeedUrl = config[nameof(IFeedPackageConfiguration.FeedUrl)],
                    UserName = config[nameof(IFeedPackageConfiguration.UserName)],
                    Password = config[nameof(IFeedPackageConfiguration.Password)],
                    ApiKey = config[nameof(IFeedPackageConfiguration.ApiKey)],
                    ApiUrl = config[nameof(IFeedPackageConfiguration.ApiUrl)]
                };

                await packageConfig.EnsureProGetConnectionInfoAsync(CredentialResolutionContext.None, cancellationToken).ConfigureAwait(false);

                return new ProGetApiClient(packageConfig);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null;
            }
        }
    }
}