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
    internal sealed class PackageNameSuggestionProvider : ISuggestionProvider
    {
#if BuildMaster
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var feedName = config["FeedName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(feedName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<ProGetCredentials>(credentialName);
            var client = new ProGetClient(credentials.Url, feedName, credentials.UserName, ConvertValue(credentials.Password));

            var packages = await client.GetPackagesAsync().ConfigureAwait(false);

            return from p in packages
                   let name = new PackageName(p.@group, p.name)
                   orderby name.Group, name.Name
                   select name.ToString();
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