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
        public abstract ISet<string> ExtractGroupNames(string memberOfPropertyName = null);
        public string GetDomainPath()
        {
            return string.Join(".",
                from p in this.DistinguishedName.Split(',')
                where p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase)
                select p.Substring("DC=".Length)
            );
        }
    }
}
