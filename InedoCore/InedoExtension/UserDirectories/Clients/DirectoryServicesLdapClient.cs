using System.DirectoryServices.Protocols;
using System.Net;

namespace Inedo.Extensions.UserDirectories.Clients;

internal sealed class DirectoryServicesLdapClient : LdapClient
{
    private LdapConnection connection;

    public override void Connect(string server, int? port, bool ldaps, bool bypassSslCertificate)
    {
        this.connection = new LdapConnection(new LdapDirectoryIdentifier(server, port ?? (ldaps ? 636 : 389)));
        if (ldaps)
        {
            this.connection.SessionOptions.SecureSocketLayer = true;

            if (bypassSslCertificate)
                this.connection.SessionOptions.VerifyServerCertificate = new VerifyServerCertificateCallback((connection, certifacte) => true);
        }
    }
    public override void Bind(NetworkCredential credentials)
    {
        this.connection.Bind(credentials);
    }

    public override void Bind(string bindDn, string password)
    {
        this.connection.AuthType = AuthType.Basic;
        Bind(new NetworkCredential(bindDn, password));
    }

    public override IEnumerable<LdapClientEntry> Search(string distinguishedName, string filter, LdapDomains.LdapClientSearchScope scope)
    {
        var request = new SearchRequest(distinguishedName, filter, (SearchScope)scope);
        var response = this.connection.SendRequest(request);

        if (response is SearchResponse sr)
            return sr.Entries.Cast<SearchResultEntry>().Select(r => new Entry(r));
        else
            return Enumerable.Empty<Entry>();
    }
    public override IEnumerable<LdapClientEntry> SearchV2(string distinguishedName, string filter, LdapDomains.LdapClientSearchScope scope, params string[] attributes)
    {
        var request = new SearchRequest(distinguishedName, filter, (SearchScope)scope, attributes);
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

    private sealed class Entry(SearchResultEntry result) : LdapClientEntry
    {
        private readonly SearchResultEntry result = result;

        public override string DistinguishedName => this.result.DistinguishedName;

        public override string GetPropertyValue(string propertyName)
        {
            var propertyCollection = this.result.Attributes?[propertyName];
            if (propertyCollection == null || propertyCollection.Count == 0)
                return string.Empty;

            return propertyCollection[0]?.ToString() ?? string.Empty;
        }
        public override ISet<string> ExtractGroupNames(string memberOfPropertyName = null)
        {

            Logger.Log(MessageLevel.Debug, "Begin ExtractGroupNames", "AD User Directory");
            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var memberOfAttr = this.result.Attributes[AH.NullIf(memberOfPropertyName, string.Empty) ?? "memberof"];
                if (memberOfAttr == null)
                    return groups;

                foreach (var memberOfObj in memberOfAttr)
                {
                    if (memberOfObj == null)
                        continue;
                    string memberOf = null;
                    switch (memberOfObj)
                    {
                        case byte[] memberOfBytes:
                            memberOf = InedoLib.UTF8Encoding.GetString(memberOfBytes, 0, memberOfBytes.Length);
                            break;
                        case string memberOfStr:
                            memberOf = memberOfStr;
                            break;
                        default:
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(memberOf))
                    {
                        var groupNames = from part in memberOf.Split(',')
                                         where part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
                                         let name = part["CN=".Length..]
                                         where !string.IsNullOrWhiteSpace(name)
                                         select name;

                        groups.UnionWith(groupNames);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(MessageLevel.Error, "Error extracting Group Names", "AD User Directory", null, ex);
            }
            Logger.Log(MessageLevel.Debug, "End ExtractGroupNames", "AD User Directory");

            return groups;
        }
    }
}
