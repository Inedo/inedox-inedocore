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
    internal sealed class FeedNameSuggestionProvider : ISuggestionProvider
    {
#if BuildMaster
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<ProGetCredentials>(credentialName);
            string baseUrl = new Uri(credentials.Url, UriKind.Absolute).GetLeftPart(UriPartial.Authority);
            var client = new ProGetClient(baseUrl, null, credentials.UserName, ConvertValue(credentials.Password));

            return await client.GetFeedNamesAsync().ConfigureAwait(false);
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