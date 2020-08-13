using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Inedo.Extensions.UserDirectories
{
    internal static class LDAP
    {
        private static readonly LazyRegex LdapEscapeRegex = new LazyRegex(@"[,\\#+<>;""=]", RegexOptions.Compiled);
        private static readonly LazyRegex LdapUnescapeRegex = new LazyRegex(@"\\([,\\#+<>;""=])", RegexOptions.Compiled);
        private static readonly LazyRegex LdapSplitRegex = new LazyRegex(@"(?<!\\),", RegexOptions.Compiled);

        public static string GetDomainNameFromNetbiosName(string netbiosName, IDictionary<string, string> manualOverride, bool useLdaps)
        {
            if (manualOverride == null)
                throw new ArgumentNullException(nameof(manualOverride));
            if (string.IsNullOrEmpty(netbiosName))
                return null;

            if (manualOverride.TryGetValue(netbiosName, out string overridden))
                return overridden;

            using var conn = new LdapConnection(useLdaps ? new LdapDirectoryIdentifier(null) : new LdapDirectoryIdentifier(null, 636));
            var response = conn.SendRequest(new SearchRequest("", "(&(objectClass=*))", SearchScope.Base));
            if (response is SearchResponse sr && sr.Entries.Count > 0)
            {
                var cfg = sr.Entries[0].GetPropertyValue("configurationNamingContext");

                var response2 = conn.SendRequest(new SearchRequest("cn=Partitions," + cfg, "nETBIOSName=" + netbiosName, SearchScope.Subtree));
                if (response2 is SearchResponse sr2 && sr2.Entries.Count > 0)
                    return sr2.Entries[0].GetPropertyValue("dnsRoot");
            }

            return null;
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
        public static string GetDomainPath(this SearchResultEntry result) => GetDomainPath(result.DistinguishedName);
        /// <summary>
        /// Returns a period-separated concatenation of all of the domain components (fully-qualified) of the path
        /// </summary>
        /// <param name="path">The result.</param>
        public static string GetDomainPath(string path)
        {
            return string.Join(".",
                from p in path.Split(',')
                where p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase)
                select p.Substring("DC=".Length)
            );
        }
        public static string GetPropertyValue(this SearchResultEntry sr, string propertyName)
        {
            if (sr == null)
                throw new ArgumentNullException(nameof(sr));

            var propertyCollection = sr.Attributes?[propertyName];
            if (propertyCollection == null || propertyCollection.Count == 0)
                return string.Empty;

            return propertyCollection[0]?.ToString() ?? string.Empty;
        }

        public static ISet<string> ExtractGroupNames(SearchResultEntry user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string memberOf in user.Attributes["memberof"])
            {
                var groupNames = from part in memberOf.Split(',')
                                 where part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
                                 let name = part.Substring("CN=".Length)
                                 where !string.IsNullOrWhiteSpace(name)
                                 select name;

                groups.UnionWith(groupNames);
            }

            return groups;
        }
    }
}
