using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
using Inedo.Extensions.Operations.ProGet;
#elif Otter
using Inedo.Otter.Web.Controls;
#endif

namespace Inedo.Extensions.SuggestionProviders
{
    internal sealed class PackageVersionSuggestionProvider : ISuggestionProvider
    {
#if BuildMaster
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var feedName = config["FeedName"];
            var packageName = config["PackageName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(feedName) || string.IsNullOrEmpty(packageName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<ProGetCredentials>(credentialName);
            var client = new ProGetClient(credentials.Url, feedName, credentials.UserName, ConvertValue(credentials.Password));

            var package = await client.GetPackageInfoAsync(PackageName.Parse(packageName));

            return new[] { "latest" }.Concat(package.versions);
        }
#elif Otter
        public IEnumerable<string> GetSuggestions(object context)
        {
            throw new NotImplementedException();
        }
#endif

        private static string ConvertValue(SecureString s)
        {
            var ptr = Marshal.SecureStringToGlobalAllocUnicode(s);
            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
    }
}