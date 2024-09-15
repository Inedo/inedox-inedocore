using System.DirectoryServices.Protocols;
using System.Net;

namespace Inedo.Extensions.UserDirectories;

internal sealed class DirectoryServicesLdapClient : LdapClient
{
    private LdapConnection connection;
    private readonly AuthType? authType;
    private readonly string[] attributes;

    /// <inheritdoc />
    public DirectoryServicesLdapClient(AuthType? authType = null, string[] attributes = null)
    {
        this.authType = authType;
        this.attributes = attributes;
    }

    public override void Connect(string server, int? port, bool ldaps, bool bypassSslCertificate)
    {
        this.connection = new LdapConnection(new LdapDirectoryIdentifier(server, port ?? (ldaps ? 636 : 389)));
        if (authType != null)
        {
            connection.AuthType = authType.Value;
        }

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
    public override IEnumerable<LdapClientEntry> Search(string distinguishedName, string filter, LdapClientSearchScope scope)
    {
        var request = new SearchRequest(distinguishedName, filter, (SearchScope)scope);
        if (attributes != null)
        {
            request.Attributes.AddRange(attributes);
        }

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
        public override ISet<string> ExtractGroupNames(string memberOfPropertyName = "memberof", string groupNamePropertyName = "CN", bool includeDomainPath = false)
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
                                         where part.StartsWith($"{groupNamePropertyName}=", StringComparison.OrdinalIgnoreCase)
                                         let name = part[$"{groupNamePropertyName}=".Length..]
                                         where !string.IsNullOrWhiteSpace(name)
                                         select name;
                        foreach (var groupName in groupNames)
                        {
                            string groupNameToAdd = groupName;
                            if (includeDomainPath)
                            {
                                groupNameToAdd = $"{groupName}@{GetDomainPath(memberOf)}";
                            }

                            groups.Add(groupNameToAdd);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logger.Log(MessageLevel.Error, "Error extracting Group Names", "AD User Directory", null, ex);
            }
            Logger.Log(MessageLevel.Debug, "End ExtractGroupNames", "AD User Directory");

            return groups;
        }
    }
}
