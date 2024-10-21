using System;
using System.Collections.Generic;
using System.Linq;

namespace Inedo.Extensions.UserDirectories
{
    internal abstract class LdapClientEntry
    {
        protected LdapClientEntry()
        {
        }

        public abstract string DistinguishedName { get; }

        public abstract string GetPropertyValue(string propertyName);
        public abstract ISet<string> ExtractGroupNames(string memberOfPropertyName = "memberof", string groupNamePropertyName = "CN", bool includeDomainPath = false);
        public string GetDomainPath()
        {
            return GetDomainPath(this.DistinguishedName);
        }

        public static string GetDomainPath(string distinguishedName)
        {
            return string.Join(".",
                from p in distinguishedName.Split(',')
                where p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase)
                select p.Substring("DC=".Length)
            );
        }
    }
}
