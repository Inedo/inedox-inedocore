using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Inedo.Extensions.UserDirectories
{
    /****************************************************************************************************
    * Helper
    ****************************************************************************************************/
    internal static class LdapHelperV4
    {
        private static readonly LazyRegex LdapEscapeRegex = new LazyRegex(@"[,\\#+<>;""=]", RegexOptions.Compiled);
        private static readonly LazyRegex LdapUnescapeRegex = new LazyRegex(@"\\([,\\#+<>;""=])", RegexOptions.Compiled);
        private static readonly LazyRegex LdapSplitRegex = new LazyRegex(@"(?<!\\),", RegexOptions.Compiled);

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
        /// RFC 4515 §3 compatible LDAP escape method for use with LDAP filters
        /// 
        /// Supports properly escaping a sitringuished name to be used when searching for user and groups.
        /// </summary>
        /// <param name="s">LDAP string</param>
        /// <returns>string representing a RFC 4515 §3 LDAP filter parameter</returns>
        public static string EscapeFilterValue(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length + 8);
            foreach (var b in Encoding.UTF8.GetBytes(s))
            {
                if (b >= 0x80 || b is 0x2A or 0x28 or 0x29 or 0x5C or 0x00)  // * ( ) \ NUL
                    sb.Append('\\').Append(b.ToString("X2"));
                else
                    sb.Append((char)b);
            }
            return sb.ToString();
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
        
        public static string GetValue(this SearchResultEntry sr, string propertyName)
        {
            if (sr == null)
                throw new ArgumentNullException(nameof(sr));

            var propertyCollection = sr.Attributes?[propertyName];
            if (propertyCollection == null || propertyCollection.Count == 0)
                return string.Empty;

            return propertyCollection[0]?.ToString() ?? string.Empty;
        }

        public static string GetDomainQualifiedName(this string username, string domainName)
        {
            if (username.IndexOfAny(new[] { '\\', '@' }) >= 0)
                return username;
            else
                return username + "@" + domainName;
        }

        public static string GetDomainQualifiedNameV2(this string username, string domainName)
        {
            if (username.IndexOfAny(new[] { '\\', '@' }) >= 0 || string.IsNullOrWhiteSpace(domainName))
                return username;
            else
                return username + "@" + domainName;
        }
    }
}
