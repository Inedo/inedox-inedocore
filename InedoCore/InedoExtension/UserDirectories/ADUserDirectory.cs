using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.UserDirectories;
using Inedo.Serialization;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

#pragma warning disable CA1416 // Validate platform compatibility

namespace Inedo.Extensions.UserDirectories
{
    [DisplayName("Active Directory (LDAP)")]
    [Description("Queries the current domain, global catalog for trusted domains, or a specific list of domains for users and group membership.")]
    public sealed class ADUserDirectory : UserDirectory
    {
        private readonly Lazy<HashSet<CredentialedDomain>> domainsToSearch;
        private readonly Lazy<IDictionary<string, string>> netBiosNameMaps;

        public ADUserDirectory()
        {
            this.domainsToSearch = new Lazy<HashSet<CredentialedDomain>>(this.BuildDomainsToSearch);
            this.netBiosNameMaps = new Lazy<IDictionary<string, string>>(this.BuildNetBiosNameMaps);
        }

        [Persistent]
        [DisplayName("Search mode")]
        public ADSearchMode SearchMode { get; set; }

        [Persistent]
        [DisplayName("Domains to search")]
        [PlaceholderText("Only used when Search mode is Specific List")]
        [Description("Only used when Search mode is Specific List. With Specific List selected, domains entered (one per line) may contain the name of AD username/password credentials after a comma; "
            + "e.g. us.kramerica.local,KramericaCredentials")]
        public string[] DomainsToSearch { get; set; }

        [Persistent]
        [DisplayName("Domain controller host")]
        [PlaceholderText("Server is on the domain")]
        [Description("If the product server is not on the domain, specify the host name or IP address of the domain controller here, e.g. 192.168.1.1")]
        public string DomainControllerAddress { get; set; }

        [Persistent]
        [DisplayName("NETBIOS name mapping")]
        [PlaceholderText("Automatically discover")]
        [Description("A list of key/value pairs that map NETBIOS names to domain names (one per line); e.g. KRAMUS=us.kramerica.local")]
        [Category("Advanced")]
        public string[] NetBiosNameMaps { get; set; }

        [Persistent]
        [Category("Advanced")]
        [DisplayName("Search recursively")]
        [Description("Check group memberships recursively instead of only checking the groups that a user is directly a member of. This may cause reduced performance.")]
        public bool SearchGroupsRecursively { get; set; }

        [Persistent]
        [Category("Advanced")]
        [DisplayName("Include gMSA")]
        [Description("When locating users in the directory, include Group Managed Service Accounts.")]
        public bool IncludeGroupManagedServiceAccounts { get; set; }

        [Persistent]
        [Category("Advanced")]
        [DisplayName("LDAP Port Override")]
        [Description("This will override the port used to connect to LDAP or LDAPS.  If this is not set, then port 389 is used for LDAP and 636 is used for LDAPS.")]
        [PlaceholderText("Use default")]
        public string Port { get; set; }

        [Persistent]
        [Category("Advanced")]
        [DisplayName("Use LDAPS")]
        [Description("When connecting to your local Active Directory, connect via LDAP over SSL.")]
        public bool UseLdaps { get; set; }

        [Persistent]
        [Category("Advanced")]
        [DisplayName("Bypass LDAPS Certificate Validation")]
        [Description("EXPIREMENTAL. This setting will bypass all LDAPS certificate validation and should only be used when troubleshooting AD connections using LDAPS.")]
        public bool BypassLdapsCertificateValidation { get; set; }

        public override IEnumerable<IUserDirectoryPrincipal> FindPrincipals(string searchTerm) => this.FindPrincipals(PrincipalSearchType.UsersAndGroups, searchTerm);
        public override IEnumerable<IUserDirectoryUser> GetGroupMembers(string groupName)
        {
            var group = (ActiveDirectoryGroup)this.TryGetGroup(groupName);
            return group?.GetMembers() ?? new List<IUserDirectoryUser>();
        }
        public override IUserDirectoryUser TryGetAndValidateUser(string userName, string password)
        {
            if (userName.Contains("\\"))
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
                conn.Connect(AH.NullIf(this.DomainControllerAddress, string.Empty), int.TryParse(this.Port, out var port) ? port : null, this.UseLdaps, this.BypassLdapsCertificateValidation);
                if(userName?.Contains("@") ?? false)
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
        [Obsolete("Please use TryGetUser and TryGetGroup instead", false)]
        public override IUserDirectoryPrincipal TryGetPrincipal(string principalName) => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.UsersAndGroups, principalName));
        public override IUserDirectoryUser TryParseLogonUser(string logonUser)
        {
            if (string.IsNullOrEmpty(logonUser))
                throw new ArgumentNullException(nameof(logonUser));

            var domainLogin = this.TryParseLoginUserName(logonUser);
            if (domainLogin == null)
                return null;
            return this.TryGetUser(domainLogin);
        }

        private string TryParseLoginUserName(string logonUser)
        {
            if (logonUser.Contains("\\"))
            {
                var parts = logonUser.Split(new[] { '\\' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    return null;

                var domain = LDAP.GetDomainNameFromNetbiosName(parts[0], this.netBiosNameMaps.Value, this.UseLdaps);
                return $"{parts[1]}@{domain}";
            }
            return null;
        }

        private HashSet<CredentialedDomain> BuildDomainsToSearch()
        {
            this.LogDebug($"Building search root paths for search mode {this.SearchMode}...");

            if (this.SearchMode == ADSearchMode.SpecificDomains)
            {
                return new HashSet<CredentialedDomain>(
                    this.DomainsToSearch?.Select(d => CredentialedDomain.Create(d)).Where(d => d != null) ?? new CredentialedDomain[0]
                );
            }

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (InedoLib.IsWindows)
                this.AddCurrentDomainAndTrusts(paths);

            return paths.Select(p => new CredentialedDomain(p)).ToHashSet();
        }
        private void AddCurrentDomainAndTrusts(HashSet<string> paths)
        {
            using var domain = Domain.GetCurrentDomain();
            paths.Add(domain.Name);
            this.LogDebug($"Domain \"{domain.Name}\" added.");

            if (this.SearchMode == ADSearchMode.TrustedDomains)
            {
                this.LogDebug("Adding domain trust relationships...");
                addTrusts(domain.GetAllTrustRelationships());

                this.LogDebug("Getting current forest...");

                using var forest = Forest.GetCurrentForest();
                this.LogDebug($"Adding trust relationships from \"{forest.Name}\"...");
                addTrusts(forest.GetAllTrustRelationships());
            }

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
        private LdapClientEntry TryGetPrincipal(PrincipalSearchType searchType, string principalName)
        {
            if (string.IsNullOrEmpty(principalName))
                return null;

            this.LogDebug($"Trying to a {searchType} search for principal \"{principalName}\"...");

            var userSearchQuery = "objectCategory=user";
            if (this.IncludeGroupManagedServiceAccounts)
                userSearchQuery = "|(objectCategory=user)(objectCategory=msDS-GroupManagedServiceAccount)";

            var categoryFilter = AH.Switch<PrincipalSearchType, string>(searchType)
                .Case(PrincipalSearchType.UsersAndGroups, $"(|({userSearchQuery})(objectCategory=group))")
                .Case(PrincipalSearchType.Groups, "(objectCategory=group)")
                .Case(PrincipalSearchType.Users, $"({userSearchQuery})")
                .End();

            PrincipalId principalId = null;
            var searchString = new StringBuilder();
            if (searchType == PrincipalSearchType.Users)
            {
                principalId = UserId.Parse(principalName);
                searchString.Append($"(&{categoryFilter}(sAMAccountName={LDAP.Escape(principalId?.Principal ?? principalName)}))");
            }
            else if (searchType.HasFlag(PrincipalSearchType.Groups))
            {
                principalId = GroupId.Parse(principalName);
                searchString.Append($"(&{categoryFilter}(|");
                searchString.Append($"(sAMAccountName={LDAP.Escape(principalId?.Principal ?? principalName)})");
                searchString.Append($"(name={LDAP.Escape(principalId?.Principal ?? principalName)})");
                searchString.Append("))");
            }
            else if (searchType == PrincipalSearchType.UsersAndGroups)
            {
                throw new ArgumentOutOfRangeException(nameof(searchType));
            }

            this.LogDebug($"Search string is \"{searchString}\"...");

            HashSet<CredentialedDomain> domains;
            if (principalId == null)
            {
                this.LogDebug("No domain specified, searching through aliases.");
                domains = this.domainsToSearch.Value;
            }
            else
            {
                this.LogDebug($"Domain alias \"{principalId.DomainAlias}\" will be used.");
                domains = new HashSet<CredentialedDomain>
                {
                    this.domainsToSearch.Value.FirstOrDefault(x => x.Name.Equals(principalId.DomainAlias, StringComparison.OrdinalIgnoreCase)) ?? new CredentialedDomain(principalId.DomainAlias)
                };
            }

            foreach (var domain in domains)
            {
                this.LogDebug($"Searching domain {domain}...");
                //BASE DN
                var result = this.Search("DC=" + domain.Name.Replace(".", ",DC="), searchString.ToString(), userName: domain.DomainQualifiedName, password: domain.Password).FirstOrDefault();
                if (result != null)
                    return result;
            }

            this.LogDebug("Principal not found.");
            return null;
        }
        private IEnumerable<IUserDirectoryPrincipal> FindPrincipals(PrincipalSearchType searchType, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                yield break;

            var st = LDAP.Escape(searchTerm);
            var filter = $"(|(userPrincipalName={st}*)(sAMAccountName={st}*)(name={st}*)(displayName={st}*))";
            this.LogDebug("Search term: " + searchTerm);

            foreach (var principal in this.FindPrincipalsUsingLdap(searchType, filter))
            {
                yield return principal;
            }
        }
        private IEnumerable<IUserDirectoryPrincipal> FindPrincipalsUsingLdap(PrincipalSearchType searchType, string ldapSearch = null, LdapClientSearchScope scope = LdapClientSearchScope.Subtree)//, ReferralChasingOption? referralChasingOption = null)
        {
          
            var userSearchQuery = "objectCategory=user";
            if (this.IncludeGroupManagedServiceAccounts)
                userSearchQuery = "|(objectCategory=user)(objectCategory=msDS-GroupManagedServiceAccount)";

            var categoryFilter = AH.Switch<PrincipalSearchType, string>(searchType)
                .Case(PrincipalSearchType.UsersAndGroups, $"(|({userSearchQuery})(objectCategory=group))")
                .Case(PrincipalSearchType.Groups, "(objectCategory=group)")
                .Case(PrincipalSearchType.Users, $"({userSearchQuery})")
                .End();

            var filter = $"(&{categoryFilter}{ldapSearch ?? string.Empty})";
            this.LogDebug("Filter string: " + filter);

            foreach (var domain in this.domainsToSearch.Value)
            {
                this.LogDebug("Searching domain: " + domain);
                //BASE DN
                foreach (var result in this.Search("DC=" + domain.Name.Replace(".", ",DC="), filter, scope: scope, userName: domain.DomainQualifiedName, password: domain.Password))
                {
                    var principal = this.CreatePrincipal(result);
                    if (principal == null)
                        continue;

                    yield return principal;
                }
            }
        }
        private IUserDirectoryPrincipal CreatePrincipal(LdapClientEntry result)
        {
            var principalId = PrincipalId.FromSearchResult(result);
            if (principalId == null)
                return null;

            if (principalId is UserId userId)
            {
                return new ActiveDirectoryUser(
                    this,
                    userId,
                    result.GetPropertyValue("displayName"),
                    result.GetPropertyValue("mail")
                );
            }
            else
            {
                return new ActiveDirectoryGroup(this, (GroupId)principalId);
            }
        }
        private IEnumerable<LdapClientEntry> Search(string dn, string filter, LdapClientSearchScope scope = LdapClientSearchScope.Subtree, string userName = null, SecureString password = null)
        {
            using var conn = GetClient();
            conn.Connect(AH.NullIf(this.DomainControllerAddress, string.Empty), int.TryParse(this.Port, out var port) ? port : null, this.UseLdaps, this.BypassLdapsCertificateValidation);
            if (userName?.Contains("@") ?? false)
            {
                var userNameSplit = userName.Split('@');
                conn.Bind(new NetworkCredential(userNameSplit[0], password, userNameSplit[1]));
            }
            else if(userName?.Contains("\\") ?? false)
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
        private static LdapClient GetClient()
        {
#if NET452
            return new DirectoryServicesLdapClient();
#else
            if (InedoLib.NetCore && InedoLib.IsLinux)
                return new NovellLdapClient();
            else
                return new DirectoryServicesLdapClient();
#endif
        }

        [Flags]
        private enum PrincipalSearchType
        {
            None = 0,
            Users = 1,
            Groups = 2,
            UsersAndGroups = Users | Groups
        }

        private sealed class ActiveDirectoryUser : IUserDirectoryUser, IEquatable<ActiveDirectoryUser>
        {
            private readonly ADUserDirectory directory;
            private readonly UserId userId;
            private readonly HashSet<string> isMemberOfGroupCache = new(StringComparer.OrdinalIgnoreCase);
            private readonly Lazy<ISet<string>> groups; 


            public ActiveDirectoryUser(ADUserDirectory directory, UserId userId, string displayName, string emailAddress, ISet<string> groupNames = null)
            {
                this.directory = directory;
                this.userId = userId ?? throw new ArgumentNullException(nameof(userId));
                this.DisplayName = AH.CoalesceString(displayName, userId.Principal);
                this.EmailAddress = emailAddress;

                this.groups = new Lazy<ISet<string>>(() =>
                {
                    ISet<string> groups;
                    if (groupNames == null)
                    {
                        var userSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Users, this.userId.ToFullyQualifiedName());
                        groups = userSearchResult == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : userSearchResult.ExtractGroupNames();
                    }
                    else
                    {
                        groups = groupNames;
                    }
                    if (this.directory.SearchGroupsRecursively)
                    {
                        var groupsToSearch = new Queue<string>(groups);
                        while (groupsToSearch.Count > 0)
                        {
                            var nextGroup = groupsToSearch.Dequeue();
                            var groupSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, nextGroup);
                            if (groupSearchResult != null)
                            {
                                var groupNames = groupSearchResult.ExtractGroupNames();
                                foreach (var groupName in groupNames)
                                {
                                    if (groups.Add(groupName))
                                        groupsToSearch.Enqueue(groupName);
                                }
                            }

                        }

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
            private readonly ADUserDirectory directory;
            private readonly HashSet<string> isMemberOfGroupCache = new(StringComparer.OrdinalIgnoreCase);
            private readonly Lazy<ISet<string>> groups;

            public ActiveDirectoryGroup(ADUserDirectory directory, GroupId groupId, ISet<string> groupNames = null)
            {
                this.directory = directory;
                this.groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));

                this.groups = new Lazy<ISet<string>>(() =>
                {
                    ISet<string> groups;
                    if (groupNames == null)
                    {
                        var rootGroupSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, this.groupId.ToFullyQualifiedName());
                        groups = rootGroupSearchResult == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : rootGroupSearchResult.ExtractGroupNames();
                    }
                    else
                    {
                        groups = groupNames;
                    }
                    if (this.directory.SearchGroupsRecursively)
                    {
                        var groupsToSearch = new Queue<string>(groups);
                        while (groupsToSearch.Count > 0)
                        {
                            var nextGroup = groupsToSearch.Dequeue();
                            var groupSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, nextGroup);
                            if (groupSearchResult != null)
                            {
                                var groupNames = groupSearchResult.ExtractGroupNames();
                                foreach (var groupName in groupNames)
                                {
                                    if (groups.Add(groupName))
                                        groupsToSearch.Enqueue(groupName);
                                }
                            }

                        }

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
                var groupSearch = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, this.groupId.ToFullyQualifiedName());
                var users = this.directory.FindPrincipalsUsingLdap(PrincipalSearchType.UsersAndGroups, $"(memberOf={groupSearch.GetPropertyValue("distinguishedName")})", LdapClientSearchScope.Subtree);

                foreach (var user in users)
                {
                    if (user is IUserDirectoryUser userId)
                        yield return userId;
                    continue;
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

        private sealed class CredentialedDomain : IEquatable<CredentialedDomain>
        {
            public CredentialedDomain(string name, string userName = null, SecureString password = null)
            {
                this.Name = name ?? throw new ArgumentNullException(nameof(name));
                this.UserName = userName;
                this.Password = password;
                this.DomainQualifiedName = userName != null ? GetDomainQualifiedName(name, userName) : null;
            }

            public string Name { get; }
            public string UserName { get; }
            public SecureString Password { get; }
            public string DomainQualifiedName { get; }

            public static CredentialedDomain Create(string input)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return null;

                var split = input.Split(',');

                if (split.Length < 2)
                    return new CredentialedDomain(split[0], null);

                var cred = SecureCredentials.Create(split[1], CredentialResolutionContext.None);

                if (cred is UsernamePasswordCredentials userPassCred)
                    return new CredentialedDomain(split[0], userPassCred.UserName, userPassCred.Password);

#pragma warning disable CS0618 // Type or member is obsolete
                if (cred is Extensibility.Credentials.UsernamePasswordCredentials legacyPassCred)
                    return new CredentialedDomain(split[0], legacyPassCred.UserName, legacyPassCred.Password);

                var unexpectedType = cred?.GetType()?.AssemblyQualifiedName ?? "null";
                throw new InvalidOperationException(
                    $"Credential {split[1]} has an unexpected type ({unexpectedType}); " +
                    $"expected {typeof(UsernamePasswordCredentials).AssemblyQualifiedName}" +
                    $" or {typeof(Extensibility.Credentials.UsernamePasswordCredentials).AssemblyQualifiedName}."
                );
#pragma warning restore CS0618 // Type or member is obsolete
            }

            public bool Equals(CredentialedDomain other) => Equals(this, other);
            public override bool Equals(object obj) => Equals(this, obj as CredentialedDomain);
            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name);

            public override string ToString()
            {
                if (this.UserName == null)
                    return this.Name;
                else
                    return $"{this.UserName}@{this.Name}";
            }

            private static bool Equals(CredentialedDomain x, CredentialedDomain y)
            {
                if (ReferenceEquals(x, y))
                    return true;
                if (x is null || y is null)
                    return false;

                return StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);
            }

            private static string GetDomainQualifiedName(string domainName, string userName)
            {
                if (userName.IndexOfAny(new[] { '\\', '@' }) >= 0)
                    return userName;
                else
                    return userName + "@" + domainName;
            }
        }
    }
}
