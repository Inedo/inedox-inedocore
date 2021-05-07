#if !NET452
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Inedo.Diagnostics;
using Novell.Directory.Ldap;
using Logger = Inedo.Diagnostics.Logger;

namespace Inedo.Extensions.UserDirectories
{
    internal sealed class NovellLdapClient : LdapClient
    {
        private LdapConnection connection;

        public override void Connect(string server, int? port, bool ldaps)
        {
            this.connection = new LdapConnection();
            this.connection.Connect(server, port ?? (ldaps ? 636 : 389));
        }
        public override void Bind(NetworkCredential credentials)
        {
            Logger.Log(MessageLevel.Debug, "Novell", $"User dn: {credentials.UserName}{(string.IsNullOrWhiteSpace(credentials.Domain) ? string.Empty : "@" + credentials.Domain)}");
            this.connection.Bind($"{credentials.UserName}{(string.IsNullOrWhiteSpace(credentials.Domain) ? string.Empty : "@" + credentials.Domain)}", credentials.Password);
        }
        public override IEnumerable<LdapClientEntry> Search(string distinguishedName, string filter, LdapClientSearchScope scope)
        {
            return getResults(this.connection.Search(distinguishedName, (int)scope, filter, null, false));

            static IEnumerable<LdapClientEntry> getResults(ILdapSearchResults results)
            {
                while (results.HasMore())
                {
                    LdapEntry entry;
                    try
                    {
                        entry = results.Next();
                    }
                    catch (LdapReferralException)
                    {
                        entry = null;
                    }

                    if (entry != null)
                        yield return new Entry(entry);
                }
            }
        }

        private sealed class Entry : LdapClientEntry
        {
            private readonly LdapEntry entry;

            public Entry(LdapEntry entry) => this.entry = entry;

            public override string DistinguishedName => this.entry.Dn;

            public override string GetPropertyValue(string propertyName)
            {
                try
                {
                    return this.entry.GetAttribute(propertyName)?.StringValue;
                }
                catch
                {
                    return null;
                }
            }

            public override ISet<string> ExtractGroupNames()
            {
                var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var memberOf in this.entry.GetAttribute("memberof")?.StringValueArray ?? new string[0])
                    {
                        var groupNames = from part in memberOf.Split(',')
                                         where part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
                                         let name = part.Substring("CN=".Length)
                                         where !string.IsNullOrWhiteSpace(name)
                                         select name;

                        groups.UnionWith(groupNames);
                    }
                }
                catch
                {
                }

                return groups;
            }
        }
    }
}
#endif
