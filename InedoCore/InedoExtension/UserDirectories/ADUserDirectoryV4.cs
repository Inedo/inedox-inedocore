using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Inedo.Extensibility.UserDirectories;
using Inedo.Serialization;
using ActiveDirectory = System.DirectoryServices.ActiveDirectory;
using LDAP = Inedo.Extensions.UserDirectories.LdapHelperV4;

namespace Inedo.Extensions.UserDirectories
{
    [DisplayName("V4: Active Directory/LDAP (Preview)")]
    [Description("Queries the current domain, global catalog for trusted domains, or a specific list of domains for users and group membership.")]

    public sealed class ADUserDirectoryV4 : UserDirectory
    {
        private readonly Lazy<HashSet<string>> localTrusts;
        private readonly Lazy<IDictionary<string, string>> NetBiosNameMapsDict;

        public ADUserDirectoryV4()
        {
            this.NetBiosNameMapsDict = new(this.BuildNetBiosNameMaps);
            this.localTrusts = OperatingSystem.IsWindows() ? new Lazy<HashSet<string>>(this.GetCurrentDomainAndTrusts) : new Lazy<HashSet<string>>(new HashSet<string>());
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
        public string SearchRootPath { get; set; }

        [Persistent]
        [Category("LDAP Overrides")]
        [DisplayName("User LDAP Filter")]
        [DefaultValue("(objectCategory=user)")]
        [PlaceholderText("(objectCategory=user)")]

        public string UsersFilterBase { get; set; } = "(objectCategory=user)";

        [Persistent]
        [Category("LDAP Overrides")]
        [DisplayName("gMSA LDAP Filter")]
        [DefaultValue("(objectCategory=msDS-GroupManagedServiceAccount)")]
        [PlaceholderText("(objectCategory=msDS-GroupManagedServiceAccount)")]

        public string GroupManagedServiceAccountFilterBase { get; set; } = "(objectCategory=msDS-GroupManagedServiceAccount)";

        [Persistent]
        [Category("LDAP Overrides")]
        [DisplayName("Group LDAP Filter")]
        [DefaultValue("(objectCategory=group)")]
        [PlaceholderText("(objectCategory=group)")]
        public string GroupsFilterBase { get; set; } = "(objectCategory=group)";

        [Persistent]
        [Category("LDAP Overrides")]
        [DisplayName("User name Property Value")]
        [DefaultValue("sAMAccountName")]
        [PlaceholderText("sAMAccountName")]
        public string UserNamePropertyName { get; set; } = "sAMAccountName";

        [Persistent]
        [Category("LDAP Overrides")]
        [DisplayName("Group Name Property Value")]
        [DefaultValue("name")]
        [PlaceholderText("name")]
        public string GroupNamePropertyName { get; set; } = "name";

        [Persistent]
        [Category("LDAP Overrides")]
        [DisplayName("Display Name Value")]
        [DefaultValue("displayName")]
        [PlaceholderText("displayName")]
        public string DisplayNamePropertyName { get; set; } = "displayName";

        [Persistent]
        [Category("LDAP Overrides")]
        [DisplayName("Email Property Value")]
        [DefaultValue("mail")]
        [PlaceholderText("mail")]
        public string EmailAddressPropertyName { get; set; } = "mail";

        [Persistent]
        [Category("LDAP Overrides")]
        [DisplayName("Group Membership Property Value")]
        [DefaultValue("memberof")]
        [PlaceholderText("memberof")]
        [Description("This property will only be used when \"No Recursion\" or \"Recursive Search (LDAP/Non-Active Directory)\" is set for the group search type.  When the group search type is \"Recursive Search (Active Directory Only)\" a special Active Directory query is used to find groups.")]
        public string GroupNamesPropertyName { get; set; } = "memberof";

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
        [DisplayName("Search Group Method")]
        [DefaultValue(GroupSearchType.NoRecursion)]
        [Description("Choose to recursively check group memberships or only check for the groups that a user is directly a member of. This may cause reduced performance.")]
        public GroupSearchType GroupSearchType { get; set; }

        [Persistent]
        [Category("Advanced")]
        [DisplayName("Include gMSA")]
        [Description("When locating users in the directory, include Group Managed Service Accounts.")]

        public bool IncludeGroupManagedServiceAccounts { get; set; }

        /****************************************************************************************************
        * UserDirectory
        ****************************************************************************************************/

        public override IEnumerable<IUserDirectoryPrincipal> FindPrincipals(string searchTerm) => this.FindPrincipals(PrincipalSearchType.UsersAndGroups, searchTerm);
        public override IEnumerable<IUserDirectoryUser> GetGroupMembers(string groupName)
        {
            var group = (ActiveDirectoryGroup)this.TryGetGroup(groupName);
            return group?.GetMembers()?.ToList() ?? new List<IUserDirectoryUser>();
        }
        public override IUserDirectoryUser TryGetAndValidateUser(string userName, string password)
        {
            if (userName.Contains('\\'))
            {
                userName = this.TryParseLoginUserName(userName);
                if (userName == null)
                    return null;
            }

            var result = this.TryGetPrincipal(PrincipalSearchType.Users, userName);
            if (result == null)
                return null;

            try
            {
                using var conn = GetClient();
                conn.Connect(AH.NullIf(this.DomainControllerAddress, string.Empty), int.TryParse(this.Port, out var port) ? port : null, this.LdapConnection != LdapConnectionType.Ldap, this.LdapConnection == LdapConnectionType.LdapsWithBypass);
                if (userName?.Contains('@') ?? false)
                {
                    var userNameSplit = userName.Split('@');
                    conn.Bind(new NetworkCredential(userNameSplit[0], password, userNameSplit[1]));

                }
                else
                {
                    var domain = result.GetDomainPath();
                    conn.Bind(new NetworkCredential(userName, password, domain));
                }
                return this.CreatePrincipal(result) as IUserDirectoryUser;
            }
            catch
            {
                return null;
            }
        }
        public override IUserDirectoryUser TryGetUser(string userName) => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.Users, userName)) as IUserDirectoryUser;
        public override IUserDirectoryGroup TryGetGroup(string groupName) => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.Groups, groupName)) as IUserDirectoryGroup;
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
        private string TryParseLoginUserName(string logonUser)
        {
            if (logonUser.Contains('\\'))
            {
                var parts = logonUser.Split(new[] { '\\' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    return null;

                var domain = GetDomainNameFromNetbiosName(parts[0]);
                if (string.IsNullOrWhiteSpace(domain))
                    return parts[1];
                return $"{parts[1]}@{domain}";
            }
            return null;
        }

        private static LdapClient GetClient()
        {
            return new DirectoryServicesLdapClient();
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
        private LdapClientEntry TryGetPrincipal(PrincipalSearchType searchType, string principalName)
        {
            if (string.IsNullOrEmpty(principalName))
                return null;

            this.LogDebug($"Trying to a {searchType} search for principal \"{principalName}\"...");

            var userSearchQuery = this.UsersFilterBase;
            if (this.IncludeGroupManagedServiceAccounts)
                userSearchQuery = $"(|{UsersFilterBase}{this.GroupManagedServiceAccountFilterBase})";

            var categoryFilter = AH.Switch<PrincipalSearchType, string>(searchType)
                .Case(PrincipalSearchType.UsersAndGroups, $"(|{userSearchQuery}{this.GroupsFilterBase})")
                .Case(PrincipalSearchType.Groups, this.GroupsFilterBase)
                .Case(PrincipalSearchType.Users, userSearchQuery)
                .End();

            PrincipalId principalId = searchType.HasFlag(PrincipalSearchType.Users) ? UserId.Parse(principalName) : GroupId.Parse(principalName);
            var searchString = new StringBuilder();


            searchString.Append($"(&{categoryFilter}");
            if (searchType.HasFlag(PrincipalSearchType.Groups))
                searchString.Append("(|");
            searchString.Append($"({this.UserNamePropertyName}={LDAP.Escape(principalId?.Principal ?? principalName)})");
            if (searchType.HasFlag(PrincipalSearchType.Groups))
            {
                searchString.Append($"({this.GroupNamePropertyName}={LDAP.Escape(principalId?.Principal ?? principalName)})");
                //Closes the |
                searchString.Append($")");
            }
            //Closes the &
            searchString.Append(")");

            var principal = this.SearchDomain(searchString.ToString()).FirstOrDefault();
            if(principal == null)
                this.LogDebug("Principal not found.");
            return principal;
        }
        private IEnumerable<IUserDirectoryPrincipal> FindPrincipals(PrincipalSearchType searchType, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                yield break;

            var st = LDAP.Escape(searchTerm);
            var filter = $"(|(userPrincipalName={st}*)({this.UserNamePropertyName}={st}*)({this.GroupNamePropertyName}={st}*)({this.DisplayNamePropertyName}={st}*))";
            this.LogDebug("Search term: " + searchTerm);

            foreach (var principal in this.FindPrincipalsUsingLdap(searchType, filter))
                yield return principal;
        }

        private IEnumerable<IUserDirectoryPrincipal> FindPrincipalsUsingLdap(PrincipalSearchType searchType, string ldapSearch = null, LdapClientSearchScope scope = LdapClientSearchScope.Subtree)
        {
            var userSearchQuery = this.UsersFilterBase;
            if (this.IncludeGroupManagedServiceAccounts)
                userSearchQuery = $"(|{UsersFilterBase}{this.GroupManagedServiceAccountFilterBase})";

            var categoryFilter = AH.Switch<PrincipalSearchType, string>(searchType)
                .Case(PrincipalSearchType.UsersAndGroups, $"(|{userSearchQuery}{this.GroupsFilterBase})")
                .Case(PrincipalSearchType.Groups, this.GroupsFilterBase)
                .Case(PrincipalSearchType.Users, userSearchQuery)
                .End();

            var filter = $"(&{categoryFilter}{ldapSearch ?? string.Empty})";
            this.LogDebug("Filter string: " + filter);

            foreach (var result in this.SearchDomain(filter, scope))
            {
                var principal = this.CreatePrincipal(result);
                if (principal == null)
                    continue;

                yield return principal;
            }
        }

        private IUserDirectoryPrincipal CreatePrincipal(LdapClientEntry result)
        {
            var principalId = CreatePrincipleId(result);
            if (principalId == null)
                return null;

            if (principalId is UserId userId)
                return new ActiveDirectoryUser(
                    this,
                    userId,
                    result.GetPropertyValue(this.DisplayNamePropertyName),
                    result.GetPropertyValue(this.EmailAddressPropertyName)
                );
            return new ActiveDirectoryGroup(this, (GroupId)principalId);
        }

        //Copy pasta from PrincipalId.cs, but uses configured LDAP overrides
        private PrincipalId CreatePrincipleId(LdapClientEntry result)
        {
            if (result == null)
                return null;

            var objectCategory = result.GetPropertyValue("objectCategory");
            if (objectCategory == null)
                return null;

            var isUser = objectCategory.IndexOf("CN=Person", StringComparison.OrdinalIgnoreCase) >= 0;
            var isGmsa = objectCategory.IndexOf("CN=ms-DS-Group-Managed-Service-Account", StringComparison.OrdinalIgnoreCase) >= 0;

            var principalName = result.GetPropertyValue(this.UserNamePropertyName);

            if (!isUser && string.IsNullOrWhiteSpace(principalName))
                principalName = result.GetPropertyValue(this.GroupNamesPropertyName);

            if (string.IsNullOrWhiteSpace(principalName))
                return null;

            var domain = result.GetDomainPath();
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            try
            {
                // do not return the account if it is disabled
                if (isUser && int.TryParse(result.GetPropertyValue("userAccountControl"), out int flags) && (flags & 0x02) != 0)
                    return null;
            }
            catch
            {
            }

            if (isUser || isGmsa)
                return new UserId(principalName, domain) { DistinguishedName = result.GetPropertyValue("distinguishedName") };
            else
                return new GroupId(principalName, domain) { DistinguishedName = result.GetPropertyValue("distinguishedName") };
        }

        /****************************************************************************************************
        * Internal Domain Search Methods
        ****************************************************************************************************/
        /// <summary>
        /// Parse NetBios Name Maps from Input
        /// </summary>
        /// <returns>IDictionary<string, string> of NetBios Maps</returns>
        private IDictionary<string, string> BuildNetBiosNameMaps()
        {
            if (this.NetBiosNameMaps == null || this.NetBiosNameMaps.Length == 0)
                return new Dictionary<string, string>(0);

            var maps = this.NetBiosNameMaps
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries))
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

            var port = AH.ParseInt(this.Port);
            
            using var conn = new LdapConnection(
                this.LdapConnection != LdapConnectionType.Ldap || port != null 
                    ? new LdapDirectoryIdentifier(AH.NullIf(this.Domain?.Trim(), string.Empty), port ?? 636) 
                    : new LdapDirectoryIdentifier(AH.NullIf(this.Domain?.Trim(), string.Empty))
                );

            if (!string.IsNullOrWhiteSpace(this.Username))
                conn.Bind(new NetworkCredential(this.Username, this.Password));

            if (this.LdapConnection != LdapConnectionType.Ldap)
            {
                conn.SessionOptions.SecureSocketLayer = true;
                if (this.LdapConnection != LdapConnectionType.LdapsWithBypass)
                    conn.SessionOptions.VerifyServerCertificate = new VerifyServerCertificateCallback((connection, certifacte) => true);
            }
            var response = conn.SendRequest(new SearchRequest("", "(&(objectClass=*))", SearchScope.Base));
            if (response is SearchResponse sr && sr.Entries.Count > 0)
            {
                var cfg = sr.Entries[0].GetValue("configurationNamingContext");

                var response2 = conn.SendRequest(new SearchRequest("cn=Partitions," + cfg, "nETBIOSName=" + netbiosName, SearchScope.Subtree));
                if (response2 is SearchResponse sr2 && sr2.Entries.Count > 0)
                {
                    var root = sr2.Entries[0].GetValue("dnsRoot");
                    this.NetBiosNameMapsDict.Value.Add(netbiosName, root);
                    return root;
                }
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
            HashSet<string> paths = new HashSet<string>();
            using var domain = ActiveDirectory.Domain.GetCurrentDomain();
            paths.Add(domain.Name);
            this.LogDebug($"Domain \"{domain.Name}\" added.");

            this.LogDebug("Adding domain trust relationships...");
            addTrusts(domain.GetAllTrustRelationships());

            this.LogDebug("Getting current forest...");

            using var forest = Forest.GetCurrentForest();
            this.LogDebug($"Adding trust relationships from \"{forest.Name}\"...");
            addTrusts(forest.GetAllTrustRelationships());
            return paths;


            void addTrusts(TrustRelationshipInformationCollection trusts)
            {
                foreach (TrustRelationshipInformation trust in trusts)
                {
                    this.LogDebug($"Trust relationship found, source: {trust.SourceName}, target: {trust.TargetName}, type: {trust.TrustType}, direction: {trust.TrustDirection} ");
                    if (trust.TrustDirection == TrustDirection.Outbound)
                    {
                        this.LogDebug("Trust direction was Outbound, ignoring.");
                    }
                    else
                    {
                        paths.Add(trust.TargetName);
                    }
                }

                if (trusts.Count == 0)
                    this.LogDebug("No trust relationships found.");
            };
        }

        /// <summary>
        /// Search configured domain or current server's domain trusts
        /// </summary>
        /// <param name="searchString">LDAP query to search for</param>
        /// <returns>IEnumerable of <see cref="LdapClientEntry"/></returns>
        /// <exception cref="InvalidOperationException">If CredentialName is not <see cref="UsernamePasswordCredentials"/></exception>
        private IEnumerable<LdapClientEntry> SearchDomain(string searchString, LdapClientSearchScope scope = LdapClientSearchScope.Subtree)
        {
            this.LogDebug($"Search string is \"{searchString}\"...");

            if (!string.IsNullOrWhiteSpace(this.Domain))
            {
                this.LogDebug($"Searching domain {this.Domain}...");

                var baseDn = string.IsNullOrWhiteSpace(this.SearchRootPath) ? "DC=" + this.Domain.Replace(".", ",DC=") : this.SearchRootPath;
                foreach (var result in this.Search(baseDn, searchString.ToString(), scope: scope, userName: this.Username?.GetDomainQualifiedName(this.Domain), password: this.Password))
                    yield return result;
            }
            else
            {
                foreach (var domain in this.localTrusts.Value)
                {
                    this.LogDebug($"Searching domain {domain}...");
                    
                    var baseDn = string.IsNullOrWhiteSpace(this.SearchRootPath) ? "DC=" + domain.Replace(".", ",DC=") : this.SearchRootPath;
                    foreach (var result in this.Search(baseDn, searchString.ToString()))
                        yield return result;
                }
            }

            yield break;
        }

        /// <summary>
        /// Connects to the Domain and finds LdapClinetEntries
        /// </summary>
        /// <param name="dn">Root Path of the LDAP server</param>
        /// <param name="filter">Query filter</param>
        /// <param name="scope"><see cref="LdapClientSearchScope"/></param>
        /// <param name="userName">Username to connect to the domain wtih</param>
        /// <param name="password">Password to connect to the domain with</param>
        /// <returns></returns>
        private IEnumerable<LdapClientEntry> Search(string dn, string filter, LdapClientSearchScope scope = LdapClientSearchScope.Subtree, string userName = null, SecureString password = null)
        {
            using var conn = GetClient();
            conn.Connect(AH.NullIf(this.DomainControllerAddress, string.Empty), int.TryParse(this.Port, out var port) ? port : null, this.LdapConnection != LdapConnectionType.Ldap, this.LdapConnection == LdapConnectionType.LdapsWithBypass);
            if (userName?.Contains('@') ?? false)
            {
                var userNameSplit = userName.Split('@');
                conn.Bind(new NetworkCredential(userNameSplit[0], password, userNameSplit[1]));
            }
            else if (userName?.Contains('\\') ?? false)
            {
                var userNameSplit = userName.Split('\\');
                conn.Bind(new NetworkCredential(userNameSplit[1], password, userNameSplit[0]));
            }
            else
            {
                conn.Bind(new NetworkCredential(userName, password));
            }
            return conn.Search(dn, filter, scope).ToList();
        }



        /****************************************************************************************************
        * User and Group Classes
        ****************************************************************************************************/
        private sealed class ActiveDirectoryUser : IUserDirectoryUser, IEquatable<ActiveDirectoryUser>
        {
            private readonly ADUserDirectoryV4 directory;
            private readonly UserId userId;
            private readonly HashSet<string> isMemberOfGroupCache = new(StringComparer.OrdinalIgnoreCase);
            private readonly Lazy<ISet<string>> groups;


            public ActiveDirectoryUser(ADUserDirectoryV4 directory, UserId userId, string displayName, string emailAddress, ISet<string> groupNames = null)
            {
                this.directory = directory;
                this.userId = userId ?? throw new ArgumentNullException(nameof(userId));
                this.DisplayName = AH.CoalesceString(displayName, userId.Principal);
                this.EmailAddress = emailAddress;

                this.groups = new Lazy<ISet<string>>(() =>
                {
                    ISet<string> groups;
                    // Old Group Search way
                    if (this.directory.GroupSearchType != GroupSearchType.RecursiveSearchActiveDirectory) {
                        if (groupNames == null)
                        {
                            var userSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Users, this.userId.ToFullyQualifiedName());
                            groups = userSearchResult == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : userSearchResult.ExtractGroupNames(this.directory.GroupNamesPropertyName);
                        }
                        else
                        {
                            groups = groupNames;
                        }
                        if (this.directory.GroupSearchType == GroupSearchType.RecursiveSearch)
                        {
                            var groupsToSearch = new Queue<string>(groups);
                            while (groupsToSearch.Count > 0)
                            {
                                var nextGroup = groupsToSearch.Dequeue();
                                var groupSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, nextGroup);
                                if (groupSearchResult != null)
                                {
                                    var groupNames = groupSearchResult.ExtractGroupNames(this.directory.GroupNamesPropertyName);
                                    foreach (var groupName in groupNames)
                                    {
                                        if (groups.Add(groupName))
                                            groupsToSearch.Enqueue(groupName);
                                    }
                                }

                            }

                        }
                    }
                    //New AD only way
                    else
                    {
                        groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach(var groupEntry in this.directory.SearchDomain($"(&{this.directory.GroupsFilterBase}(member:1.2.840.113556.1.4.1941:={this.userId.DistinguishedName}))"))
                            groups.Add(this.directory.CreatePrincipal(groupEntry).DisplayName);
                    }
                    return groups;
                });
            }

            string IUserDirectoryPrincipal.Name => this.userId.ToFullyQualifiedName();
            public string EmailAddress { get; }
            public string DisplayName { get; }

            public bool IsMemberOfGroup(string groupName)
            {
                if (this.isMemberOfGroupCache.Contains(groupName))
                    return true;

                Logger.Log(MessageLevel.Debug, "Begin ActiveDirectoryUser IsMemberOfGroup", "AD User Directory");
                if (groupName == null)
                    throw new ArgumentNullException(nameof(groupName));


                var compareName = GroupId.Parse(groupName)?.Principal ?? groupName;
                if (this.groups.Value.Contains(compareName))
                {
                    Logger.Log(MessageLevel.Debug, "End ActiveDirectoryUser IsMemberOfGroup", "AD User Directory");
                    this.isMemberOfGroupCache.Add(groupName);
                    return true;
                }
                Logger.Log(MessageLevel.Debug, "End ActiveDirectoryUser IsMemberOfGroup", "AD User Directory");

                return false;
            }

            public bool Equals(ActiveDirectoryUser other) => this.userId.Equals(other?.userId);
            public bool Equals(IUserDirectoryUser other) => this.Equals(other as ActiveDirectoryUser);
            public bool Equals(IUserDirectoryPrincipal other) => this.Equals(other as ActiveDirectoryUser);
            public override bool Equals(object obj) => this.Equals(obj as ActiveDirectoryUser);
            public override int GetHashCode() => this.userId.GetHashCode();
        }

        private sealed class ActiveDirectoryGroup : IUserDirectoryGroup, IEquatable<ActiveDirectoryGroup>
        {
            private readonly GroupId groupId;
            private readonly ADUserDirectoryV4 directory;
            private readonly HashSet<string> isMemberOfGroupCache = new(StringComparer.OrdinalIgnoreCase);
            private readonly Lazy<ISet<string>> groups;

            public ActiveDirectoryGroup(ADUserDirectoryV4 directory, GroupId groupId, ISet<string> groupNames = null)
            {
                this.directory = directory;
                this.groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));

                this.groups = new Lazy<ISet<string>>(() =>
                {
                    ISet<string> groups;
                    //Old Group searching way
                    if (this.directory.GroupSearchType != GroupSearchType.RecursiveSearchActiveDirectory)
                    {
                        if (groupNames == null)
                        {
                            var rootGroupSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, this.groupId.ToFullyQualifiedName());
                            groups = rootGroupSearchResult == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : rootGroupSearchResult.ExtractGroupNames(this.directory.GroupNamesPropertyName);
                        }
                        else
                        {
                            groups = groupNames;
                        }
                        if (this.directory.GroupSearchType == GroupSearchType.RecursiveSearch)
                        {
                            var groupsToSearch = new Queue<string>(groups);
                            while (groupsToSearch.Count > 0)
                            {
                                var nextGroup = groupsToSearch.Dequeue();
                                var groupSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, nextGroup);
                                if (groupSearchResult != null)
                                {
                                    var groupNames = groupSearchResult.ExtractGroupNames(this.directory.GroupNamesPropertyName);
                                    foreach (var groupName in groupNames)
                                    {
                                        if (groups.Add(groupName))
                                            groupsToSearch.Enqueue(groupName);
                                    }
                                }

                            }
                        }
                    }
                    // New AD-only way
                    else
                    {
                        groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var groupEntry in this.directory.SearchDomain($"(&{this.directory.GroupsFilterBase}(member:1.2.840.113556.1.4.1941:={this.groupId.DistinguishedName}))"))
                            groups.Add(this.directory.CreatePrincipal(groupEntry).DisplayName);
                    }
                    return groups;
                });
            }

            string IUserDirectoryPrincipal.Name => this.groupId.ToFullyQualifiedName();
            string IUserDirectoryPrincipal.DisplayName => this.groupId.Principal;

            public bool IsMemberOfGroup(string groupName)
            {
                if (this.isMemberOfGroupCache.Contains(groupName))
                    return true;

                Logger.Log(MessageLevel.Debug, "Begin ActiveDirectoryGroup IsMemberOfGroup", "AD User Directory");

                if (groupName == null)
                    throw new ArgumentNullException(nameof(groupName));

                var compareName = GroupId.Parse(groupName)?.Principal ?? groupName;
                if (this.groups.Value.Contains(compareName))
                {
                    Logger.Log(MessageLevel.Debug, "End ActiveDirectoryGroup IsMemberOfGroup", "AD User Directory");
                    this.isMemberOfGroupCache.Add(groupName);
                    return true;
                }

                Logger.Log(MessageLevel.Debug, "End ActiveDirectoryGroup IsMemberOfGroup", "AD User Directory");

                return false;
            }

            internal IEnumerable<IUserDirectoryUser> GetMembers()
            {
                Logger.Log(MessageLevel.Debug, "Begin ActiveDirectoryGroup GetMembers", "AD User Directory");
                if (this.directory.GroupSearchType != GroupSearchType.RecursiveSearchActiveDirectory) {
                    var groupSearch = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, this.groupId.ToFullyQualifiedName());
                    var users = this.directory.FindPrincipalsUsingLdap(PrincipalSearchType.UsersAndGroups, $"({this.directory.GroupNamesPropertyName}={groupSearch.GetPropertyValue("distinguishedName")})", LdapClientSearchScope.Subtree);

                    foreach (var user in users)
                    {
                        if (user is IUserDirectoryUser userId)
                            yield return userId;
                        continue;
                    }
                }
                else
                {
                    foreach (var userEntry in this.directory.SearchDomain($"(&{this.directory.UsersFilterBase}(memberOf:1.2.840.113556.1.4.1941:={this.groupId.DistinguishedName}))"))
                    {
                        if(this.directory.CreatePrincipal(userEntry) is IUserDirectoryUser user)
                            yield return user;
                        continue;
                    }
                }
                Logger.Log(MessageLevel.Debug, "End ActiveDirectoryGroup GetMembers", "AD User Directory");
            }

            public bool Equals(ActiveDirectoryGroup other) => this.groupId.Equals(other?.groupId);
            public bool Equals(IUserDirectoryGroup other) => this.Equals(other as ActiveDirectoryGroup);
            public bool Equals(IUserDirectoryPrincipal other) => this.Equals(other as ActiveDirectoryGroup);
            public override bool Equals(object obj) => this.Equals(obj as ActiveDirectoryGroup);
            public override int GetHashCode() => this.groupId.GetHashCode();
            public override string ToString() => this.groupId.Principal;
        }
    }

    public enum GroupSearchType
    {
        [Description("No Recursion")]
        NoRecursion = 1,
        [Description("Recursive Search (LDAP/Non-Active Directory)")]
        RecursiveSearch,
        [Description("Recursive Search (Active Directory Only)")]
        RecursiveSearchActiveDirectory
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

    /****************************************************************************************************
    * Helper
    ****************************************************************************************************/
#warning Move to own class file
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
    }
}
