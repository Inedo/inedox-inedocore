using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;

namespace Inedo.Extensions.UserDirectories
{
    internal sealed class DirectoryServicesLdapClient : LdapClient
    {
        private LdapConnection connection;

        public override void Connect(string server, int? port, bool ldaps)
        {
            this.connection = new LdapConnection(new LdapDirectoryIdentifier(server, port ?? (ldaps ? 636 : 389)));
            if (ldaps)
                this.connection.SessionOptions.SecureSocketLayer = true;
        }
        public override void Bind(NetworkCredential credentials)
        {
            this.connection.Bind(credentials);
        }
        public override IEnumerable<LdapClientEntry> Search(string distinguishedName, string filter, LdapClientSearchScope scope)
        {
            var request = new SearchRequest(distinguishedName, filter, (SearchScope)scope);
            var response = this.connection.SendRequest(request);

            if (response is SearchResponse sr)
                return sr.Entries.Cast<SearchResultEntry>().Select(r => new Entry(r));
            else
                return Enumerable.Empty<Entry>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this.connection?.Dispose();

            base.Dispose(disposing);
        }

        private sealed class Entry : LdapClientEntry
        {
            private readonly SearchResultEntry result;

            public Entry(SearchResultEntry result) => this.result = result;

            public override string DistinguishedName => this.result.DistinguishedName;

            public override string GetPropertyValue(string propertyName)
            {
                var propertyCollection = this.result.Attributes?[propertyName];
                if (propertyCollection == null || propertyCollection.Count == 0)
                    return string.Empty;

                return propertyCollection[0]?.ToString() ?? string.Empty;
            }
            public override ISet<string> ExtractGroupNames()
            {
                var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string memberOf in this.result.Attributes["memberof"])
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
}
