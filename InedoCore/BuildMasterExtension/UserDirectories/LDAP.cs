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

        public static string GetDomainNameFromNetbiosName(string netbiosName)
        {
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
        public static HashSet<GroupId> ExtractGroups(this SearchResult result)
        {
            var groups = new HashSet<GroupId>();

            if (result == null || !result.Properties.Contains("memberof"))
                return groups;

            foreach (object memberOfProperty in result.Properties["memberof"])
            {
                var memberOf = memberOfProperty.ToString();

                //memberof is CN=groupName,OU=something,OH=else
                foreach (string cat in LdapSplitRegex.Split(memberOf))
                {
                    var cats = cat.Split('=');
                    if (string.Equals(cats[0], "CN", StringComparison.OrdinalIgnoreCase))
                    {
                        var groupName = Unescape(cats[1]);
                        groups.Add(new GroupId(groupName, LDAP.GetDomainPath(memberOf)));
                    }
                }
            }

            return groups;
        }
        public static string GetPropertyValue(this SearchResult sr, string propertyName)
        {
            var propertyCollection = sr.Properties[propertyName];
            if (propertyCollection.Count == 0)
                return string.Empty;

            return propertyCollection[0].ToString();
        }
    }
}
