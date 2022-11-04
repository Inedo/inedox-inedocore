using Inedo.Extensions.Configurations.ProGet;
using Inedo.Extensions.Operations.ProGet;
using Inedo.Extensions.UniversalPackages;

#nullable enable

namespace Inedo.Extensions.SuggestionProviders
{
    internal static class Extensions
    {
        public static async ValueTask<ProGetFeedClient?> TryCreateProGetFeedClientAsync(this IComponentConfiguration config, CancellationToken cancellationToken = default)
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

                return new ProGetFeedClient(packageConfig);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null;
            }
        }
    }
}