using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Inedo.Extensions.UserDirectories
{
    internal static class LDAP
    {
        private static readonly LazyRegex LdapEscapeRegex = new LazyRegex(@"[,\\#+<>;""=]", RegexOptions.Compiled);
        private static readonly LazyRegex LdapUnescapeRegex = new LazyRegex(@"\\([,\\#+<>;""=])", RegexOptions.Compiled);
        private static readonly LazyRegex LdapSplitRegex = new LazyRegex(@"(?<!\\),", RegexOptions.Compiled);

        public static string GetDomainNameFromNetbiosName(string netbiosName, IDictionary<string, string> manualOverride)
        {
            if (manualOverride == null)
                throw new ArgumentNullException(nameof(manualOverride));
            if (string.IsNullOrEmpty(netbiosName))
                return null;

            if (manualOverride.TryGetValue(netbiosName, out string overridden))
                return overridden;

            using (var rootDSE = new DirectoryEntry("LDAP://RootDSE"))
            using (var rootDSEConfig = new DirectoryEntry("LDAP://cn=Partitions," + rootDSE.Properties["configurationNamingContext"][0].ToString()))
            using (var searcher = new DirectorySearcher(rootDSEConfig))
            {
                searcher.SearchScope = SearchScope.OneLevel;
                searcher.PropertiesToLoad.Add("dnsRoot");
                searcher.Filter = "nETBIOSName=" + netbiosName;

                return searcher.FindOne()?.Properties["dnsRoot"]?[0]?.ToString();
            }
        }

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            return LdapEscapeRegex.Replace(s, m => "\\" + ((byte)m.Value[0]).ToString("X2"));
        }
        public static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            return LdapUnescapeRegex.Replace(s,
                m =>
                {
                    var value = m.Value.Substring(1);
                    if (value.Length == 2)
                        return ((char)byte.Parse(value, NumberStyles.HexNumber)).ToString();
                    else
                        return value;
                }
            );
        }
        /// <summary>
        /// Returns a period-separated concatenation of all of the domain components (fully-qualified) of the path
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        public static string GetDomainPath(this SearchResult result) => GetDomainPath(result.Path);
        /// <summary>
        /// Returns a period-separated concatenation of all of the domain components (fully-qualified) of the path
        /// </summary>
        /// <param name="path">The result.</param>
        /// <returns></returns>
        public static string GetDomainPath(string path)
        {
            return string.Join(".",
                from p in path.Split(',')
                where p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase)
                select p.Substring("DC=".Length)
            );
        }
        public static string GetPropertyValue(this SearchResult sr, string propertyName)
        {
            if (sr == null)
                throw new ArgumentNullException(nameof(sr));

            var propertyCollection = sr.Properties[propertyName];
            if (propertyCollection.Count == 0)
                return string.Empty;

            return propertyCollection[0].ToString();
        }
    }
}
