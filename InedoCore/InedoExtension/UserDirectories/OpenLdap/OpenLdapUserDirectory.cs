using System.Security;
using Inedo.Extensibility.UserDirectories;
using Inedo.Serialization;

namespace Inedo.Extensions.UserDirectories.OpenLdap;

[DisplayName("OpenLDAP/Generic LDAP")]
[Description("Queries an OpenLDAP server or other generic LDAP server for users and group membership.")]
public sealed partial class OpenLdapUserDirectory : UserDirectory
{
    public OpenLdapUserDirectory()
    {
        this.NetBiosNameMapsDict = new(this.BuildNetBiosNameMaps);
    }

    /****************************************************************************************************
    * Connection
    ****************************************************************************************************/

    [Persistent]
    [DisplayName("LDAP Connection")]
    [DefaultValue(LdapConnectionType.Ldap)]
    [Description("When connecting to your local LDAP Server, connect via LDAP, LDAP over SSL, or LDAP over SSL and bypass certificate errors.")]
    public LdapConnectionType LdapConnection { get; set; }

    [Persistent]
    [Required]
    [DisplayName("Host")]
    [PlaceholderText("ex: kramerica.local")]
    public string Host { get; set; }

    [Persistent]
    [DisplayName("Bind DN")]
    [Description("DN for LDAP credentials that have READ access to the domain")]
    [PlaceholderText("ex: CN=Admin,CN=Users,DC=kramerica,DC=local")]
    public string BindDn { get; set; }

    [Persistent(Encrypted = true)]
    [DisplayName("Bind Password")]
    [Description("Password for LDAP credentials that have READ access to the domain")]
    public SecureString BindPassword { get; set; }



    /****************************************************************************************************
    * LDAP User Queries
    ****************************************************************************************************/
    [Persistent]
    [Category("LDAP User Filters")]
    [DisplayName("User Search Base")]
    [PlaceholderText("Root of the directory")]
    [Description("When not specified, this will convert the host into a root path. For example: kramerica.local will covert to \"DC=kramerica,DC=local\", but if you wanted to use only the OU Users, you would specify \"CN=Users,DC=kramerica,DC=local\"")]
    public string UserSearchRootPath { get; set; }


    [Persistent]
    [Category("LDAP User Filters")]
    [DisplayName("Users")]
    [Description("`%s` will be replaced with the search string or username for a user")]
    [DefaultValue("(&(objectClass=inetOrgPerson)(uid=%s))")]
    [PlaceholderText("(&(objectClass=inetOrgPerson)(uid=%s))")]

    public string UsersFilter { get; set; } = "(&(objectClass=inetOrgPerson)(uid=%s))";

    [Persistent]
    [Category("LDAP User Filters")]
    [DisplayName("List User's Groups")]
    [Description("`%s` will be replaced with the object's Distinguished Name")]
    [DefaultValue("(|(&(objectClass=groupOfNames)(member=%s))(&(objectClass=groupOfUniqueNames)(uniqueMember=%s)))")]
    [PlaceholderText("(|(&(objectClass=groupOfNames)(member=%s))(&(objectClass=groupOfUniqueNames)(uniqueMember=%s)))")]
    public string UserGroupsFilter { get; set; } = "(|(&(objectClass=groupOfNames)(member=%s))(&(objectClass=groupOfUniqueNames)(uniqueMember=%s)))";


    /****************************************************************************************************
    * LDAP Group Queries
    ****************************************************************************************************/
    [Persistent]
    [Category("LDAP Group Filters")]
    [DisplayName("Group Search Base")]
    [PlaceholderText("Root of the directory")]
    [Description("When not specified, this will convert the host into a root path. For example: kramerica.local will covert to \"DC=kramerica,DC=local\", but if you wanted to use only the OU Users, you would specify \"CN=Groups,DC=kramerica,DC=local\"")]
    public string GroupSearchRootPath { get; set; }

    [Persistent]
    [Category("LDAP Group Filters")]
    [DisplayName("Groups")]
    [Description("`%s` will be replaced with the search string or group name for a group")]
    [DefaultValue("(&(objectClass=groupOfNames)(cn=%s))")]
    [PlaceholderText("(&(objectClass=groupOfNames)(cn=%s))")]
    public string GroupsFilter { get; set; } = "(&(objectClass=groupOfNames)(cn=%s))";

    [Persistent]
    [Category("LDAP Group Filters")]
    [DisplayName("List Group's Members")]
    [Description("`%s` will be replaced with the object's Distinguished Name.  This may require recursion to be enabled in your LDAP provider.")]
    [DefaultValue("(&(objectClass=inetOrgPerson)(memberof=%s))")]
    [PlaceholderText("(&(objectClass=inetOrgPerson)(memberof=%s))")]
    public string GroupMembersFilter { get; set; } = "(&(objectClass=inetOrgPerson)(memberof=%s))";

    /****************************************************************************************************
    * LDAP Objects
    ****************************************************************************************************/
    [Persistent]
    [Category("LDAP Object")]
    [DisplayName("User name Property Value")]
    [DefaultValue("uid")]
    [PlaceholderText("uid")]
    public string UserNamePropertyName { get; set; } = "uid";

    [Persistent]
    [Category("LDAP Object")]
    [DisplayName("Display Name Value")]
    [DefaultValue("displayName")]
    [PlaceholderText("displayName")]
    public string DisplayNamePropertyName { get; set; } = "displayName";

    [Persistent]
    [Category("LDAP Object")]
    [DisplayName("Email Property Value")]
    [DefaultValue("mail")]
    [PlaceholderText("mail")]
    public string EmailAddressPropertyName { get; set; } = "mail";

    [Persistent]
    [Category("LDAP Object")]
    [DisplayName("Group Name Property Value")]
    [DefaultValue("cn")]
    [PlaceholderText("cn")]
    public string GroupNamePropertyName { get; set; } = "cn";

    /****************************************************************************************************
    * Advanced
    ****************************************************************************************************/
    [Persistent]
    [Category("Advanced")]
    [DisplayName("NETBIOS name mapping")]
    [PlaceholderText("Don't use NETBIOS names")]
    [Description("A list of key/value pairs that map NETBIOS names to domain names (one per line); e.g. KRAMUS=us.kramerica.local")]
    public string[] NetBiosNameMaps { get; set; }

    [Persistent]
    [Category("Advanced")]
    [DisplayName("Port")]
    [PlaceholderText("Use default port")]
    public string Port { get; set; }

    
    /****************************************************************************************************
    * User Directory Methods
    ****************************************************************************************************/
    public override IEnumerable<IUserDirectoryPrincipal> FindPrincipals(string searchTerm) => this.Search(PrincipalSearchType.UsersAndGroups, $"{LdapHelperV4.Escape(searchTerm)}*");

    public override IEnumerable<IUserDirectoryUser> GetGroupMembers(string groupName)
    {
        var group = (GenericLdapGroup)this.TryGetGroup(groupName);
        if (group == null)
            return [];

        return this.GetMembers(group.PrincipalId);
    }

    public override IUserDirectoryUser TryGetAndValidateUser(string userName, string password)
    {
        // Convert domain\user to user@domain
        if (userName.Contains('\\'))
        {
            userName = this.TryParseLoginUserName(userName);
            if (userName == null)
                return null;
        }

        var user = (GenericLdapUser)this.TryGetUser(userName);
        if (user == null)
            return null;

        using var ldapClient = GetClientAndConnect(false);
        ldapClient.BindV2(user.DistinguishedName, password);

        return user;
    }

    public override IUserDirectoryGroup TryGetGroup(string groupName)
    {
        var principalId = GroupId.Parse(groupName);
        var groups = this.Search(PrincipalSearchType.Groups, $"{LdapHelperV4.Escape(principalId?.Principal ?? groupName)}");
        if (!string.IsNullOrWhiteSpace(principalId?.DomainAlias) && groups.Count() > 1)
            groups = groups.OfType<GenericLdapGroup>().OrderBy(u => u.PrincipalId.DomainAlias.Equals(principalId.DomainAlias, StringComparison.OrdinalIgnoreCase) ? 0 : 1);
        return (IUserDirectoryGroup)groups.FirstOrDefault();
    }

    public override IUserDirectoryUser TryGetUser(string userName)
    {
        var principalId = UserId.Parse(userName);
        var users = this.Search(PrincipalSearchType.Users, $"{LdapHelperV4.Escape(principalId?.Principal ?? userName)}");
        if (!string.IsNullOrWhiteSpace(principalId?.DomainAlias) && users.Count() > 1)
            users = users.OfType<GenericLdapUser>().OrderBy(u => u.PrincipalId.DomainAlias.Equals(principalId.DomainAlias, StringComparison.OrdinalIgnoreCase) ? 0 : 1);
        return (IUserDirectoryUser)users.FirstOrDefault();
    }

    public override IUserDirectoryUser TryParseLogonUser(string logonUser)
    {
        if (string.IsNullOrEmpty(logonUser))
            throw new ArgumentNullException(nameof(logonUser));

        var domainLogin = this.TryParseLoginUserName(logonUser);
        if (domainLogin == null)
            return null;
        return this.TryGetUser(domainLogin);
    }

    /****************************************************************************************************
    * Internal Methods
    ****************************************************************************************************/

    /// <summary>
    /// Returns or generates the User Base DN
    /// </summary>
    private string UserBaseDn => string.IsNullOrEmpty(this.UserSearchRootPath) ? GetDomainDistinguishedName(this.Host) : this.UserSearchRootPath;
    /// <summary>
    /// Returns or generates the Group Base DN
    /// </summary>
    private string GroupBaseDn => string.IsNullOrEmpty(this.GroupSearchRootPath) ? GetDomainDistinguishedName(this.Host) : this.GroupSearchRootPath;

    private string GetDomainDistinguishedName(string domain) => string.Join(",", domain.Split('.').Select(s => $"DC={s}"));

    /// <summary>
    /// Cached map of NetBios Names, if configured.  Mainly used to integrated auth and AD LDAP directories
    /// </summary>
    private readonly Lazy<IDictionary<string, string>> NetBiosNameMapsDict;

    /// <summary>
    /// Parse NetBios Name Maps from Input
    /// </summary>
    /// <returns>IDictionary<string, string> of NetBios Maps</returns>
    private Dictionary<string, string> BuildNetBiosNameMaps()
    {
        if (this.NetBiosNameMaps == null || this.NetBiosNameMaps.Length == 0)
            return [];

        var maps = this.NetBiosNameMaps
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Split('=', 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(m => m.Length == 2)
            .ToDictionary(m => m[0], m => m[1], StringComparer.OrdinalIgnoreCase);

        return maps;
    }

    /// <summary>
    /// Queries the configured mapping list or the current domain to find it's current netBios mapping
    /// </summary>
    /// <param name="netbiosName">NetBios mapping</param>
    /// <returns>The DNS Root of the mapped NetBios name</returns>
    private string GetDomainNameFromNetbiosName(string netbiosName)
    {
        if (string.IsNullOrEmpty(netbiosName))
            return null;

        if (this.NetBiosNameMapsDict.Value.TryGetValue(netbiosName, out string overridden))
            return overridden;

        return null;
    }

    /// <summary>
    /// Parses the login name in the format of domain\user to return as user@domain
    /// </summary>
    /// <param name="logonUser">Username in the format of domain\user</param>
    /// <returns>NULL if parsed else username@domain</returns>
    private string TryParseLoginUserName(string logonUser)
    {
        if (logonUser.Contains('\\'))
        {
            var parts = logonUser.Split('\\', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return null;

            var domain = GetDomainNameFromNetbiosName(parts[0]);
            if (string.IsNullOrWhiteSpace(domain))
                return parts[1];
            return $"{parts[1]}@{domain}";
        }
        return null;
    }

    /// <summary>
    /// Gets client and connects to the LDAP server.
    /// </summary>
    /// <param name="bind">True if the connection should also bind using the Bind DN</param>
    /// <returns>An LDAP Client</returns>
    private LdapClient GetClientAndConnect(bool bind)
    {
        LdapClient ldapClient = OperatingSystem.IsWindows() ? new DirectoryServicesLdapClient() : new NovellLdapClient();
        ldapClient.Connect(this.Host, int.TryParse(this.Port, out var port) ? port : null, this.LdapConnection != LdapConnectionType.Ldap, this.LdapConnection == LdapConnectionType.LdapsWithBypass);
        if(bind)
            ldapClient.BindV2(this.BindDn, AH.Unprotect(this.BindPassword));
        return ldapClient;
    }

    /// <summary>
    /// Searches the LDAP server for users and groups
    /// </summary>
    /// <param name="searchType">LDAP object type to search for</param>
    /// <param name="searchTerm">search string</param>
    /// <returns>An array of LDAP Users and/or Groups</returns>
    private IEnumerable<IUserDirectoryPrincipal> Search(PrincipalSearchType searchType, string searchTerm)
    {
        using var ldapClient = this.GetClientAndConnect(true);
        
        if(searchType.HasFlag(PrincipalSearchType.Users))
        {
            var userFilter = this.UsersFilter.Replace("%s", searchTerm);
            string[] attributes = ["distinguishedName", "objectCategory", "objectClass", this.UserNamePropertyName, this.DisplayNamePropertyName, this.EmailAddressPropertyName];
            var entries = ldapClient.SearchV2(this.UserBaseDn, userFilter, LdapClientSearchScope.Subtree, attributes);
            foreach(var user in entries)
                yield return CreatePrincipal(user, true);
        }
        if(searchType.HasFlag(PrincipalSearchType.Groups))
        {
            var groupFilter = this.GroupsFilter.Replace("%s", searchTerm);
            string[] attributes = ["distinguishedName", "objectCategory", "objectClass", this.GroupNamePropertyName];
            var entries = ldapClient.SearchV2(this.GroupBaseDn, groupFilter, LdapClientSearchScope.Subtree, attributes);
            foreach (var group in entries)
                yield return CreatePrincipal(group, false);
        }
    }

    /// <summary>
    /// Get's group names for a user or group principal
    /// </summary>
    /// <param name="principalId">Principal object</param>
    /// <returns>A list of group names</returns>
    private HashSet<string> GetGroupNames(PrincipalId principalId)
    {
        using var ldapClient = this.GetClientAndConnect(true);
        var groups = new HashSet<string>();

        var groupFilter = this.UserGroupsFilter.Replace("%s", principalId.DistinguishedName);
        var groupEntries = ldapClient.SearchV2(this.GroupBaseDn, groupFilter, LdapClientSearchScope.Subtree, ["distinguishedName", "objectCategory", "objectClass", this.GroupNamePropertyName]).ToList();
        foreach(var groupEntry in groupEntries)
        {
            var groupName = groupEntry.GetPropertyValue(this.GroupNamePropertyName);
            if (!string.IsNullOrWhiteSpace(groupName))
                groups.Add(groupName);
        }

        return groups;
    }

    /// <summary>
    /// Get members of a group
    /// </summary>
    /// <param name="principalId">Group Principal</param>
    /// <returns>A list of LDAP Users</returns>
    private IEnumerable<IUserDirectoryUser> GetMembers(PrincipalId principalId)
    {
        using var ldapClient = this.GetClientAndConnect(true);
        var memberFilter = this.GroupMembersFilter.Replace("%s", principalId.DistinguishedName);

        var memberEntries = ldapClient.SearchV2(this.UserBaseDn, memberFilter, LdapClientSearchScope.Subtree, ["distinguishedName", "objectCategory", "objectClass", this.UserNamePropertyName, this.DisplayNamePropertyName, this.EmailAddressPropertyName]).Select(u => CreatePrincipal(u, true)).OfType<IUserDirectoryUser>();
        return memberEntries;
    }

    /// <summary>
    /// Converts an LdapClientEntry to a User or Group Principal
    /// </summary>
    /// <param name="result">LdapClientEntry</param>
    /// <param name="isUser">True if User and False if Group</param>
    /// <returns>User or Group Principal</returns>
    private IUserDirectoryPrincipal CreatePrincipal(LdapClientEntry result, bool isUser)
    {
        var principalId = CreatePrincipleId(result, isUser);
        if (principalId == null)
            return null;

        if (principalId is UserId userId)
            return new GenericLdapUser(
                this,
                userId,
                result.GetPropertyValue(this.DisplayNamePropertyName),
                result.GetPropertyValue(this.EmailAddressPropertyName)
            );

        return new GenericLdapGroup(this, (GroupId)principalId);
    }

    //Copy pasta from PrincipalId.cs, but uses configured LDAP filters
    /// <summary>
    /// Creates a PrincipalId from an LdapClientEntry
    /// </summary>
    /// <param name="result">LdapClientEntry</param>
    /// <param name="isUser">True if User and False if Group</param>
    /// <returns>PrincipalId representing the LDAP user or group</returns>
    private PrincipalId CreatePrincipleId(LdapClientEntry result, bool isUser)
    {
        if (result == null)
            return null;

        var principalName = result.GetPropertyValue(this.UserNamePropertyName);

        if (isUser != true && string.IsNullOrWhiteSpace(principalName))
            principalName = result.GetPropertyValue(this.GroupNamePropertyName);

        if (string.IsNullOrWhiteSpace(principalName))
            return null;

        var domain = result.GetDomainPath();
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        if (isUser == true)
            return new UserId(principalName, domain) { DistinguishedName = result.DistinguishedName };
        else
            return new GroupId(principalName, domain) { DistinguishedName = result.DistinguishedName };
    }

    /****************************************************************************************************
    * User and Group Classes
    ****************************************************************************************************/
    private sealed class GenericLdapUser(OpenLdapUserDirectory directory, UserId userId, string displayName, string emailAddress) : GenericLdapPrincipal(directory, userId), IUserDirectoryUser
    {
        public string EmailAddress { get; } = emailAddress;

        public override string DisplayName { get; } = AH.CoalesceString(displayName, userId.Principal);

        public bool Equals(IUserDirectoryUser other) => this.Equals(other as GenericLdapPrincipal);
    }

    private sealed class GenericLdapGroup(OpenLdapUserDirectory directory, GroupId groupId) : GenericLdapPrincipal(directory, groupId), IUserDirectoryGroup
    {
        internal IEnumerable<IUserDirectoryUser> GetMembers() => this.directory.GetMembers(this.principalId);
    }

    private abstract class GenericLdapPrincipal : IUserDirectoryPrincipal, IEquatable<GenericLdapPrincipal>
    {
        protected readonly PrincipalId principalId;
        protected readonly OpenLdapUserDirectory directory;
        protected readonly HashSet<string> isMemberOfGroupCache = new(StringComparer.OrdinalIgnoreCase);
        protected readonly Lazy<HashSet<string>> groups;

        public GenericLdapPrincipal(OpenLdapUserDirectory directory, PrincipalId principalId)
        {
            this.directory = directory;
            this.principalId = principalId ?? throw new ArgumentNullException(nameof(principalId));
            this.groups = new Lazy<HashSet<string>>(() => this.directory.GetGroupNames(this.principalId));
        }

        internal PrincipalId PrincipalId => this.principalId;
        public string Name => this.principalId.ToFullyQualifiedName();
        public virtual string DisplayName => this.principalId.Principal;
        public string DistinguishedName => this.principalId.DistinguishedName;

        public bool Equals(GenericLdapPrincipal other) => this.principalId.Equals(other?.principalId);
        public bool Equals(IUserDirectoryGroup other) => this.Equals(other as GenericLdapPrincipal);
        public bool Equals(IUserDirectoryPrincipal other) => this.Equals(other as GenericLdapPrincipal);
        public override bool Equals(object obj) => this.Equals(obj as GenericLdapPrincipal);
        public override int GetHashCode() => this.principalId.GetHashCode();

        public bool IsMemberOfGroup(string groupName)
        {
            if (this.isMemberOfGroupCache.Contains(groupName))
                return true;

            ArgumentNullException.ThrowIfNull(groupName);

            var compareName = GroupId.Parse(groupName)?.Principal ?? groupName;
            if (this.groups.Value.Contains(compareName))
            {
                this.isMemberOfGroupCache.Add(groupName);
                return true;
            }

            return false;
        }

        public override string ToString() => this.principalId.Principal;
    }
    
    [Flags]
    private enum PrincipalSearchType
    {
        None = 0,
        Users = 1,
        Groups = 2,
        UsersAndGroups = Users | Groups
    }
}

