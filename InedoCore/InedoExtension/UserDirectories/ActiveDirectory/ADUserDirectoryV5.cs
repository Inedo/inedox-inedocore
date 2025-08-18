using System.DirectoryServices.ActiveDirectory;
using System.Net;
using System.Runtime.Versioning;
using System.Security;
using Inedo.Extensibility.UserDirectories;
using Inedo.Serialization;

namespace Inedo.Extensions.UserDirectories.ActiveDirectory;

[DisplayName("V5: Active Directory")]
[Description("Queries the current Active Directory domain, global catalog for trusted domains, or a specific list of domains for users and group membership. Optimized for Microsoft Active Directory.")]
public sealed partial class ADUserDirectoryV5 : UserDirectory
{
    private readonly Lazy<HashSet<string>> localTrusts;
    private readonly Lazy<IDictionary<string, string>> NetBiosNameMapsDict;

    public ADUserDirectoryV5()
    {
        this.NetBiosNameMapsDict = new(this.BuildNetBiosNameMaps);
        this.localTrusts = new Lazy<HashSet<string>>(() => !string.IsNullOrWhiteSpace(this.Domain) ? [this.Domain] : OperatingSystem.IsWindows() ? this.GetCurrentDomainAndTrusts() : []);
    }

    /****************************************************************************************************
    * General
    ****************************************************************************************************/
    [Persistent]
    [DisplayName("Domain")]
    [PlaceholderText("Use domain server is joined to")]
    public string Domain { get; set; }

    [Persistent]
    [DisplayName("User name")]
    [PlaceholderText("Use trust to domain the server is joined to")]
    [Description("User name for AD/LDAP credentials that have READ access to the domain")]
    public string Username { get; set; }

    [Persistent(Encrypted = true)]
    [DisplayName("Password")]
    [PlaceholderText("Use trust to domain the server is joined to")]
    [Description("Password for AD/LDAP credentials that have READ access to the domain")]
    public SecureString Password { get; set; }

    /****************************************************************************************************
    * Connection
    ****************************************************************************************************/
    [Persistent]
    [Category("Connection")]
    [DisplayName("Domain controller host")]
    [PlaceholderText("Use Domain")]
    [Description("If the connection to the domain controller is different than what is specified in the Domain property, specify the host name or IP address of the domain controller here, e.g. kramerica.local")]
    public string DomainControllerAddress { get; set; }

    // Change to Dropdown: LDAP, LDAPS, LDAPS With Override

    [Persistent]
    [Category("Connection")]
    [DisplayName("LDAP Connection")]
    [DefaultValue(LdapConnectionType.Ldap)]
    [Description("When connecting to your local Active Directory, connect via LDAP, LDAP over SSL, or LDAP over SSL and bypass certificate errors.")]
    public LdapConnectionType LdapConnection { get; set; }

    [Persistent]
    [Category("Connection")]
    [DisplayName("LDAP Port Override")]
    [Description("This will override the port used to connect to LDAP or LDAPS.  If this is not set, then port 389 is used for LDAP and 636 is used for LDAPS.")]
    [PlaceholderText("Use default port")]
    public string Port { get; set; }

    /****************************************************************************************************
    * LDAP Overrides
    ****************************************************************************************************/

    [Persistent]
    [Category("LDAP Overrides")]
    [PlaceholderText("Root of the directory")]
    [Description("This will currently use the domain. For example: kramerica.local's root path is \"DC=kramerica,DC=local\", but if you wanted to use only the OU Users, you would specify \"CN=Users,DC=kramerica,DC=local\"")]
    [Undisclosed]
    public string SearchRootPath { get; set; }

    [Persistent]
    [Category("LDAP Overrides")]
    [DisplayName("User LDAP Filter")]
    [DefaultValue("(objectCategory=user)")]
    [PlaceholderText("(objectCategory=user)")]
    [Undisclosed]
    public string UsersFilterBase { get; set; } = "(objectCategory=user)";

    [Persistent]
    [Category("LDAP Overrides")]
    [DisplayName("gMSA LDAP Filter")]
    [DefaultValue("(objectCategory=msDS-GroupManagedServiceAccount)")]
    [PlaceholderText("(objectCategory=msDS-GroupManagedServiceAccount)")]
    [Undisclosed]
    public string GroupManagedServiceAccountFilterBase { get; set; } = "(objectCategory=msDS-GroupManagedServiceAccount)";

    [Persistent]
    [Category("LDAP Overrides")]
    [DisplayName("User Name Property Value")]
    [DefaultValue("sAMAccountName")]
    [PlaceholderText("sAMAccountName")]
    [Undisclosed]
    public string UserNamePropertyName { get; set; } = "sAMAccountName";


    [Persistent]
    [Category("LDAP Overrides")]
    [DisplayName("Display Name Value")]
    [DefaultValue("displayName")]
    [PlaceholderText("displayName")]
    [Undisclosed]
    public string DisplayNamePropertyName { get; set; } = "displayName";

    [Persistent]
    [Category("LDAP Overrides")]
    [DisplayName("Email Property Value")]
    [DefaultValue("mail")]
    [PlaceholderText("mail")]
    [Undisclosed]
    public string EmailAddressPropertyName { get; set; } = "mail";

    [Persistent]
    [Category("LDAP Overrides")]
    [DisplayName("Group LDAP Filter")]
    [DefaultValue("(objectCategory=group)")]
    [PlaceholderText("(objectCategory=group)")]
    [Undisclosed]
    public string GroupsFilterBase { get; set; } = "(objectCategory=group)";
    
    [Persistent]
    [Category("LDAP Overrides")]
    [DisplayName("Group Name Property Value")]
    [DefaultValue("name")]
    [PlaceholderText("name")]
    [Undisclosed]
    public string GroupNamePropertyName { get; set; } = "name";

    /****************************************************************************************************
    * Advanced
    ****************************************************************************************************/

    [Persistent]
    [Category("Advanced")]
    [DisplayName("NETBIOS name mapping")]
    [PlaceholderText("Automatically discover")]
    [Description("A list of key/value pairs that map NETBIOS names to domain names (one per line); e.g. KRAMUS=us.kramerica.local")]
    public string[] NetBiosNameMaps { get; set; }

    [Persistent]
    [Category("Advanced")]
    [DisplayName("Include gMSA")]
    [Description("When locating users in the directory, include Group Managed Service Accounts.")]

    public bool IncludeGroupManagedServiceAccounts { get; set; }

    /****************************************************************************************************
    * UserDirectory Methods
    ****************************************************************************************************/

    public override IEnumerable<IUserDirectoryPrincipal> FindPrincipals(string searchTerm) => this.Search(PrincipalSearchType.UsersAndGroups, $"{LdapHelperV4.Escape(searchTerm)}*");
    public override IEnumerable<IUserDirectoryUser> GetGroupMembers(string groupName) => (this.TryGetGroup(groupName) as ActiveDirectoryV5Group)?.GetMembers()?.ToList() ?? [];
    public override IUserDirectoryUser TryGetAndValidateUser(string userName, string password)
    {
        // Convert domain\user to user@domain
        if (userName.Contains('\\'))
        {
            userName = this.TryParseLoginUserName(userName);
            if (userName == null)
                return null;
        }

        var user = (ActiveDirectoryV5User)this.TryGetUser(userName);
        if (user == null)
            return null;

        try
        {
            using var ldapClient = GetClientAndConnect(false);
            ldapClient.BindUsingDn(user.DistinguishedName, password);

            return user;
        }
        catch
        {
            return null;
        }
    }
    public override IUserDirectoryUser TryGetUser(string userName)
    {
        var principalId = UserId.Parse(userName);
        var users = this.Search(PrincipalSearchType.Users, $"{LdapHelperV4.Escape(principalId?.Principal ?? userName)}");
        if (!string.IsNullOrWhiteSpace(principalId?.DomainAlias) && users.Count() > 1)
            users = users.OfType<ActiveDirectoryV5User>().OrderBy(u => u.PrincipalId.DomainAlias.Equals(principalId.DomainAlias, StringComparison.OrdinalIgnoreCase) ? 0 : 1);
        return (IUserDirectoryUser)users.FirstOrDefault();
    }
    public override IUserDirectoryGroup TryGetGroup(string groupName)
    {
        var principalId = GroupId.Parse(groupName);
        var groups = this.Search(PrincipalSearchType.Groups, $"{LdapHelperV4.Escape(principalId?.Principal ?? groupName)}");
        if (!string.IsNullOrWhiteSpace(principalId?.DomainAlias) && groups.Count() > 1)
            groups = groups.OfType<ActiveDirectoryV5Group>().OrderBy(u => u.PrincipalId.DomainAlias.Equals(principalId.DomainAlias, StringComparison.OrdinalIgnoreCase) ? 0 : 1);
        return (IUserDirectoryGroup)groups.FirstOrDefault();
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
    private LdapClient GetClientAndConnect(bool bind)
    {
        LdapClient ldapClient = OperatingSystem.IsWindows() ? new DirectoryServicesLdapClient() : new NovellLdapClient();

        ldapClient.Connect(AH.NullIf(this.DomainControllerAddress, string.Empty) ?? AH.NullIf(this.Domain, string.Empty), AH.ParseInt(this.Port), this.LdapConnection != LdapConnectionType.Ldap, this.LdapConnection == LdapConnectionType.LdapsWithBypass);
        
        if (bind)
        {
            var username = this.Username?.GetDomainQualifiedNameV2(this.Domain);
            if (username?.Contains('@') ?? false)
            {
                var userNameSplit = username.Split('@');
                ldapClient.Bind(new NetworkCredential(userNameSplit[0], this.Password, userNameSplit[1]));
            }
            else if (username?.Contains('\\') ?? false)
            {
                var userNameSplit = username.Split('\\');
                ldapClient.Bind(new NetworkCredential(userNameSplit[1], this.Password, userNameSplit[0]));
            }
            else
            {
                ldapClient.Bind(new NetworkCredential(username, string.IsNullOrWhiteSpace(username) ? null : this.Password));
            }
        }
        return ldapClient;
    }
    
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

    [Flags]
    private enum PrincipalSearchType
    {
        None = 0,
        Users = 1,
        Groups = 2,
        UsersAndGroups = Users | Groups
    }

    /****************************************************************************************************
    * Get/Create Principals Methods
    ****************************************************************************************************/
    
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
            return new ActiveDirectoryV5User(
                this,
                userId,
                result.GetPropertyValue(this.DisplayNamePropertyName),
                result.GetPropertyValue(this.EmailAddressPropertyName)
            );
        return new ActiveDirectoryV5Group(this, (GroupId)principalId);
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

        var principalName = isUser == true 
            ? result.GetPropertyValue(this.UserNamePropertyName)
            : result.GetPropertyValue(this.GroupNamePropertyName);

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
    * Internal Domain Discovery Methods
    ****************************************************************************************************/
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

        try
        {
            //incluse this.Domain
            using var conn = GetClientAndConnect(true);

            var response = conn.SearchV2("", "(&(objectClass=*))", LdapClientSearchScope.Base, ["configurationNamingContext"]).FirstOrDefault();
            if (response != null)
            {
                var cfg = response.GetPropertyValue("configurationNamingContext");

                var response2 = conn.SearchV2("cn=Partitions," + cfg, "nETBIOSName=" + netbiosName, LdapClientSearchScope.Subtree, ["dnsRoot"]).FirstOrDefault();
                if (response2 != null)
                {
                    var root = response2.GetPropertyValue("dnsRoot");
                    this.NetBiosNameMapsDict.Value.Add(netbiosName, root);
                    return root;
                }
            }
        }
        catch (Exception ex)
        {
            this.Log(MessageLevel.Error, ex.Message, "AD User Directory V5", ex.ToString(), ex);
        }

        return null;
    }

    /// <summary>
    /// Gets the current server's domain trusts
    /// </summary>
    /// <remarks><b>Windows Only</b></remarks>
    /// <returns>A string list of domains this computer trusts</returns>
    [SupportedOSPlatform("windows")]
    private HashSet<string> GetCurrentDomainAndTrusts()
    {
        HashSet<string> paths = [];
        using var domain = System.DirectoryServices.ActiveDirectory.Domain.GetCurrentDomain();
        paths.Add(domain.Name);
        this.Log(MessageLevel.Debug, $"Domain \"{domain.Name}\" added.", "AD User Directory V5");

        this.Log(MessageLevel.Debug, "Adding domain trust relationships...", "AD User Directory V5");
        addTrusts(domain.GetAllTrustRelationships());

        this.Log(MessageLevel.Debug, "Getting current forest...", "AD User Directory V5");

        using var forest = Forest.GetCurrentForest();
        this.Log(MessageLevel.Debug, $"Adding trust relationships from \"{forest.Name}\"...", "AD User Directory V5");
        addTrusts(forest.GetAllTrustRelationships());
        return paths;

        void addTrusts(TrustRelationshipInformationCollection trusts)
        {
            foreach (TrustRelationshipInformation trust in trusts)
            {
                this.Log(MessageLevel.Debug, $"Trust relationship found, source: {trust.SourceName}, target: {trust.TargetName}, type: {trust.TrustType}, direction: {trust.TrustDirection} ", "AD User Directory V5");
                if (trust.TrustDirection == TrustDirection.Outbound)
                {
                    this.Log(MessageLevel.Debug, "Trust direction was Outbound, ignoring.", "AD User Directory V5");
                }
                else
                {
                    paths.Add(trust.TargetName);
                }
            }

            if (trusts.Count == 0)
                this.Log(MessageLevel.Debug, "No trust relationships found.", "AD User Directory V5");
        }
    }

    /****************************************************************************************************
     * Internal Search Methods
     ****************************************************************************************************/
    /// <summary>
    /// Search configured domain or current server's domain trusts for users and groups
    /// </summary>
    /// <param name="searchType">LDAP object type to search for</param>
    /// <param name="searchTerm">search string</param>
    /// <param name="scope">LDAP search scope</param>
    /// <returns>An array of LDAP Users and/or Groups</returns>
    private IEnumerable<IUserDirectoryPrincipal> Search(PrincipalSearchType searchType, string searchTerm)
    {
        this.Log(MessageLevel.Debug, $"Search string is \"{searchTerm}\"...", "AD User Directory V5");

        using var client = GetClientAndConnect(true);
        var domains = !string.IsNullOrWhiteSpace(this.Domain) ? [this.Domain] : this.localTrusts.Value;

        if (searchType.HasFlag(PrincipalSearchType.Users))
        {
            var userSearchQuery = this.UsersFilterBase;
            if (this.IncludeGroupManagedServiceAccounts)
                userSearchQuery = $"(|{UsersFilterBase}{this.GroupManagedServiceAccountFilterBase})";

            //If searchTerm contains a '*', then include search against extra AD attributes
            var filter = searchTerm?.Contains('*') ?? false
                ? $"(&{userSearchQuery}(|(userPrincipalName={searchTerm})({this.UserNamePropertyName}={searchTerm})({this.DisplayNamePropertyName}={searchTerm})))"
                : $"(&{userSearchQuery}({this.UserNamePropertyName}={searchTerm}))";
            this.Log(MessageLevel.Debug, "User Filter string: " + filter, "AD User Directory V5");
            foreach (var user in this.SearchDomains(client, filter, true))
                yield return user;
        }
        if (searchType.HasFlag(PrincipalSearchType.Groups))
        {
            //If searchTerm contains a '*', then include search against extra AD attributes
            var filter = searchTerm?.Contains('*') ?? false
                ? $"(&{this.GroupsFilterBase}(|(samAccountName={searchTerm})({this.GroupNamePropertyName}={searchTerm})))" 
                : $"(&{this.GroupsFilterBase}({this.GroupNamePropertyName}={searchTerm}))";
            this.Log(MessageLevel.Debug, "Group Filter string: " + filter, "AD User Directory V5");
            foreach (var group in this.SearchDomains(client, filter, false))
                yield return group;
        }

    }

    /// <summary>
    /// Handles searching the configured domains or current server's domain trusts for users and groups
    /// </summary>
    /// <param name="client"><see cref="LdapClient"/></param>
    /// <param name="ldapFilter">LDAP Query</param>
    /// <param name="isUser">True for User and False for group</param>
    /// <param name="scope">LDAP Scope</param>
    /// <returns></returns>
    private IEnumerable<IUserDirectoryPrincipal> SearchDomains(LdapClient client, string ldapFilter, bool isUser)
    {
        string[] attributes = isUser 
            ? ["distinguishedName", "objectCategory", "objectClass", "userAccountControl", this.UserNamePropertyName, this.DisplayNamePropertyName, this.EmailAddressPropertyName]
            : ["distinguishedName", "objectCategory", "objectClass", "userAccountControl", this.GroupNamePropertyName];

        foreach (var domain in this.localTrusts.Value)
        {
            this.Log(MessageLevel.Debug, $"Searching domain {domain}...", "AD User Directory V5");

            var baseDn = string.IsNullOrWhiteSpace(this.SearchRootPath) ? "DC=" + domain.Replace(".", ",DC=") : this.SearchRootPath;

            this.Log(MessageLevel.Debug, $"Using base dn: \"{baseDn}\"...", "AD User Directory V5");
            foreach (var result in client.SearchV2(baseDn, ldapFilter, LdapClientSearchScope.Subtree, attributes))
                yield return CreatePrincipal(result, isUser);
        }
    }

    /// <summary>
    /// Get's group names for a user or group principal
    /// </summary>
    /// <param name="principal"><see cref="ActiveDirectoryV5Principal"/> Principal object</param>
    /// <returns>A list of group names</returns>
    private ISet<string> GetGroupNames(ActiveDirectoryV5Principal principal)
    {

        this.Log(MessageLevel.Debug, "Begin ActiveDirectoryV5 GetGroupNames", "AD User Directory V5");
        using var ldapClient = this.GetClientAndConnect(true);
        ISet<string> groups = new HashSet<string>();

        foreach(var group in this.SearchDomains(ldapClient, $"(&{this.GroupsFilterBase}(member:1.2.840.113556.1.4.1941:={principal.PrincipalId.DistinguishedName}))", false))
            groups.Add(group.DisplayName);

        this.Log(MessageLevel.Debug, "End ActiveDirectoryV5 GetGroupNames", "AD User Directory V5");
        return groups;
    }

    /// <summary>
    /// Get members of a group
    /// </summary>
    /// <param name="principalId">Group Principal</param>
    /// <returns>A list of LDAP Users</returns>
    private IEnumerable<IUserDirectoryUser> GetMembers(PrincipalId principalId)
    {
        this.Log(MessageLevel.Debug, "Begin ActiveDirectoryV5 GetMembers", "AD User Directory V5");
        using var client = this.GetClientAndConnect(true);

        foreach (var user in this.SearchDomains(client, $"(&{this.UsersFilterBase}(memberOf:1.2.840.113556.1.4.1941:={principalId.DistinguishedName}))", true))
            yield return user as IUserDirectoryUser;
        
        this.Log(MessageLevel.Debug, "End ActiveDirectoryV5 GetMembers", "AD User Directory V5");
    }

    /****************************************************************************************************
    * User and Group Classes
    ****************************************************************************************************/
    private sealed class ActiveDirectoryV5User(ADUserDirectoryV5 directory, UserId userId, string displayName, string emailAddress) : ActiveDirectoryV5Principal(directory, userId), IUserDirectoryUser
    {
        public string EmailAddress { get; } = emailAddress;

        public override string DisplayName { get; } = AH.CoalesceString(displayName, userId.Principal);

        public bool Equals(IUserDirectoryUser other) => this.Equals(other as ActiveDirectoryV5Principal);
    }

    private sealed class ActiveDirectoryV5Group(ADUserDirectoryV5 directory, GroupId groupId) : ActiveDirectoryV5Principal(directory, groupId), IUserDirectoryGroup
    {
        internal IEnumerable<IUserDirectoryUser> GetMembers() => this.directory.GetMembers(this.principalId);
    }

    private abstract class ActiveDirectoryV5Principal : IUserDirectoryPrincipal, IEquatable<ActiveDirectoryV5Principal>
    {
        protected readonly PrincipalId principalId;
        protected readonly ADUserDirectoryV5 directory;
        protected readonly HashSet<string> isMemberOfGroupCache = new(StringComparer.OrdinalIgnoreCase);
        protected readonly Lazy<ISet<string>> groups;

        public ActiveDirectoryV5Principal(ADUserDirectoryV5 directory, PrincipalId principalId)
        {
            this.directory = directory;
            this.principalId = principalId ?? throw new ArgumentNullException(nameof(principalId));
            this.groups = new Lazy<ISet<string>>(() => this.directory.GetGroupNames(this));
        }

        internal PrincipalId PrincipalId => this.principalId;
        public string Name => this.principalId.ToFullyQualifiedName();
        public virtual string DisplayName => this.principalId.Principal;
        public string DistinguishedName => this.principalId.DistinguishedName;

        public bool Equals(ActiveDirectoryV5Principal other) => this.principalId.Equals(other?.principalId);
        public bool Equals(IUserDirectoryGroup other) => this.Equals(other as ActiveDirectoryV5Principal);
        public bool Equals(IUserDirectoryPrincipal other) => this.Equals(other as ActiveDirectoryV5Principal);
        public override bool Equals(object obj) => this.Equals(obj as ActiveDirectoryV5Principal);
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
}

public enum LdapConnectionType
{
    [Description("Use LDAP")]
    Ldap = 1,
    [Description("Use LDAPS")]
    Ldaps,
    [Description("Use LDAPS and bypass certificate errors")]
    LdapsWithBypass
}