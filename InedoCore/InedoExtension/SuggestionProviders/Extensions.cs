using Inedo.Extensibility;
using Inedo.Extensions.UniversalPackages;

namespace Inedo.Extensions.SuggestionProviders
{
    internal static class Extensions
    {
        public static IFeedPackageConfiguration AsFeedPackageConfiguration(this IComponentConfiguration config) => new ProGetFeedConfiguration(config);
        
        private struct ProGetFeedConfiguration : IFeedPackageConfiguration
        {
            public ProGetFeedConfiguration(IComponentConfiguration config) => this.config = config;
            private readonly IComponentConfiguration config;
            string IFeedPackageConfiguration.PackageSourceName => this.config[nameof(IFeedPackageConfiguration.PackageSourceName)];
            string IFeedPackageConfiguration.FeedName => this.config[nameof(IFeedPackageConfiguration.FeedName)];
            string IFeedPackageConfiguration.FeedUrl => this.config[nameof(IFeedPackageConfiguration.FeedUrl)];
            string IFeedPackageConfiguration.UserName => this.config[nameof(IFeedPackageConfiguration.UserName)];
            string IFeedPackageConfiguration.Password => this.config[nameof(IFeedPackageConfiguration.Password)];
            string IFeedPackageConfiguration.ApiKey => this.config[nameof(IFeedPackageConfiguration.ApiKey)];
            string IFeedPackageConfiguration.PackageName => this.config[nameof(IFeedPackageConfiguration.PackageName)];
            string IFeedPackageConfiguration.PackageVersion => this.config[nameof(IFeedPackageConfiguration.PackageVersion)];
        }
    }
}