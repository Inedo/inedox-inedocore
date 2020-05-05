using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.UserDirectories;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.UserDirectories
{
    public enum ADSearchMode
    {
        [Description("Current domain only")]
        // Searches only within the domain of the current user credentials in effect for the security context under which the application is running.
        CurrentDomain,

        [Description("All trusted domains")]
        // Searches all domains with an Inbound or Bidirectional Trust relationship of the current user credentials in effect for the security context under which the application is running.
        TrustedDomains,

        [Description("Specific list...")]
        // Searches an explicit list of domains
        SpecificDomains
    }

    [DisplayName("Active Directory (LDAP)")]
    [Description("Queries the current domain, global catalog for trusted domains, or a specific list of domains for users and group membership.")]
    [System.Runtime.InteropServices.Guid("8767A20B-A4F1-4614-B688-538F9E6BD195")]
    public sealed class ADUserDirectory : UserDirectory
    {
        private Lazy<HashSet<CredentialedDomain>> domainsToSearch;
        private Lazy<IDictionary<string, string>> netBiosNameMaps;

        [Persistent]
        [DisplayName("Search mode")]
        public ADSearchMode SearchMode { get; set; }

        [Persistent]
        [DisplayName("Domains to search")]
        [PlaceholderText("Only used when Search mode is Specific List")]
        [Description("With Specific List selected, domains entered (one per line) may contain the name of AD username/password credentials after a comma; "
            + "e.g. us.kramerica.local,KramericaCredentials")]
        [Category("Advanced")]
        public string[] DomainsToSearch { get; set; }

        [Persistent]
        [DisplayName("Domain controller host")]
        [PlaceholderText("Server is on the domain")]
        [Description("If the product server is not on the domain, specify the host name or IP address of the domain controller here, e.g. 192.168.1.1")]
        [Category("Advanced")]
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
        public bool IncludeGroupManagedServiceAccounts{ get; set; }

        [Persistent]
        [Category("Advanced")]
        [DisplayName("Use LDAPS")]
        [Description("When connecting to your local Active Directory, connect via LDAP over SSL.")]
        public bool UseLdaps { get; set; }

        public ADUserDirectory()
        {
            this.domainsToSearch = new Lazy<HashSet<CredentialedDomain>>(this.BuildDomainsToSearch);
            this.netBiosNameMaps = new Lazy<IDictionary<string, string>>(this.BuildNetBiosNameMaps);
        }

        public override IEnumerable<IUserDirectoryPrincipal> FindPrincipals(string searchTerm) => this.FindPrincipals(PrincipalSearchType.UsersAndGroups, searchTerm);

        private HashSet<CredentialedDomain> BuildDomainsToSearch()
        {
            this.LogDebug($"Building Search Root Paths for SearchMode \"{this.SearchMode}\"...");

            if (this.SearchMode == ADSearchMode.SpecificDomains)
            {
                return new HashSet<CredentialedDomain>(
                    this.DomainsToSearch?.Select(d => CredentialedDomain.Create(d)).Where(d => d != null) ?? new CredentialedDomain[0]
                );
            }

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var domain = Domain.GetCurrentDomain())
            {
                paths.Add(domain.Name);
                this.LogDebug($"Domain \"{domain.Name}\" added.");

                if (this.SearchMode == ADSearchMode.TrustedDomains)
                {
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

                    this.LogDebug($"Adding domain trust relationships...");
                    addTrusts(domain.GetAllTrustRelationships());

                    this.LogDebug($"Getting current forest...");
                    using (var forest = Forest.GetCurrentForest())
                    {
                        this.LogDebug($"Adding trust relationships from \"{forest.Name}\"...");
                        addTrusts(forest.GetAllTrustRelationships());
                    }
                }
            }

            return paths.Select(p => new CredentialedDomain(p)).ToHashSet();
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

        public override IEnumerable<IUserDirectoryUser> GetGroupMembers(string groupName)
        {
            throw new NotImplementedException();
        }

        public override IUserDirectoryUser TryGetAndValidateUser(string userName, string password)
        {
            var result = this.TryGetPrincipal(PrincipalSearchType.Users, userName);
            if (result == null)
                return null;

            using (var entry = new DirectoryEntry(result.Path, userName, password))
            using (var searcher = new DirectorySearcher(entry))
            {
                try
                {
                    if (searcher.FindOne() == null)
                        return null;

                    return this.CreatePrincipal(result) as IUserDirectoryUser;
                }
                catch (Exception ex)
                {
                    this.LogDebug($"Searcher could not find user \"{userName}\", error was: {ex.ToString()}");
                    return null;
                }
            }
        }

        public override IUserDirectoryUser TryGetUser(string userName) => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.Users, userName)) as IUserDirectoryUser;

        public override IUserDirectoryGroup TryGetGroup(string groupName) => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.Groups, groupName)) as IUserDirectoryGroup;

        public override IUserDirectoryPrincipal TryGetPrincipal(string principalName) => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.UsersAndGroups, principalName));

        public override IUserDirectoryUser TryParseLogonUser(string logonUser)
        {
            if (string.IsNullOrEmpty(logonUser))
                throw new ArgumentNullException(nameof(logonUser));
            
            var parts = logonUser.Split(new[] { '\\' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return null;
            var ldapsPort = ":636";
            if(this.UseLdaps && !string.IsNullOrWhiteSpace(this.DomainControllerAddress) && this.DomainControllerAddress.Contains(":"))
            {
                var hostParts = this.DomainControllerAddress.TrimEnd('/').Split(':');
                ldapsPort = ":"+hostParts[hostParts.Length-1];
            }

            var domain = LDAP.GetDomainNameFromNetbiosName(parts[0], this.netBiosNameMaps.Value, this.UseLdaps, ldapsPort);
            return this.TryGetUser($"{parts[1]}@{domain}");
        }

        private SearchResult TryGetPrincipal(PrincipalSearchType searchType, string principalName)
        {
            if (string.IsNullOrEmpty(principalName))
                return null;

            this.LogDebug($"Trying to a {searchType} search for principal \"{principalName}\"...");

            PrincipalId principalId = null;
            var searchString = new StringBuilder();
            if (searchType == PrincipalSearchType.Users)
            {
                principalId = UserId.Parse(principalName);
                searchString.Append($"(sAMAccountName={LDAP.Escape(principalId?.Principal ?? principalName)})");
            }
            else if (searchType.HasFlag(PrincipalSearchType.Groups))
            {
                principalId = GroupId.Parse(principalName);
                searchString.Append("(|");
                searchString.Append($"(sAMAccountName={LDAP.Escape(principalId?.Principal ?? principalName)})");
                searchString.Append($"(name={LDAP.Escape(principalId?.Principal ?? principalName)})");
                searchString.Append(")");
            }
            else if (searchType == PrincipalSearchType.UsersAndGroups)
            {
                throw new ArgumentOutOfRangeException(nameof(searchType));
            }
            this.LogDebug($"Search string is \"{searchString}\"...");

            HashSet<CredentialedDomain> domains;
            if (principalId == null)
            {
                this.LogDebug($"No domain specified, searching through aliases.");
                domains = this.domainsToSearch.Value;
            }
            else
            {
                this.LogDebug($"Domain alias \"{principalId.DomainAlias}\" will be used.");
                domains = new HashSet<CredentialedDomain>();
                domains.Add(this.domainsToSearch.Value.FirstOrDefault(x=>x.Name.Equals(principalId.DomainAlias)) 
                    ?? new CredentialedDomain(principalId.DomainAlias));
            }
            foreach (var domain in domains)
            {
                this.LogDebug($"Searching domain {domain}...");
                using (var entry = new DirectoryEntry(this.GetLdapRoot() + "DC=" + domain.Name.Replace(".", ",DC="), domain.UserName, domain.Password, AuthenticationTypes.Secure))
                using (var searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = searchString.ToString();
                    var result = searcher.FindOne();
                    if (result != null)
                        return result;
                }
            }
            this.LogDebug($"Principal not found.");
            return null;
        }
        private IEnumerable<IUserDirectoryPrincipal> FindPrincipals(PrincipalSearchType searchType, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                yield break;

            var userSearchQuery = "objectCategory=user";
            if (this.IncludeGroupManagedServiceAccounts)
                userSearchQuery = "|(objectCategory=user)(objectCategory=msDS-GroupManagedServiceAccount)";

            var categoryFilter = AH.Switch<PrincipalSearchType, string>(searchType)
                .Case(PrincipalSearchType.UsersAndGroups, $"(|({userSearchQuery})(objectCategory=group))")
                .Case(PrincipalSearchType.Groups, "(objectCategory=group)")
                .Case(PrincipalSearchType.Users, $"({userSearchQuery})")
                .End();

            var st = LDAP.Escape(searchTerm);
            var filter = $"(&{categoryFilter}(|(userPrincipalName={st}*)(sAMAccountName={st}*)(name={st}*)(displayName={st}*)))";

            this.LogDebug("Search term: " + searchTerm);
            this.LogDebug("Filter string: " + filter);

            foreach (var domain in this.domainsToSearch.Value)
            {
                this.LogDebug("Searching domain: " + domain);

                using (var entry = new DirectoryEntry(this.GetLdapRoot() + "DC=" + domain.Name.Replace(".", ",DC="), domain.UserName, domain.Password, AuthenticationTypes.Secure))
                using (var searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = filter;

                    using (var results = searcher.FindAll())
                    {
                        foreach (SearchResult result in results)
                        {
                            var principal = this.CreatePrincipal(result);
                            if (principal == null)
                                continue;

                            yield return principal;
                        }
                    }
                }
            }
        }
        private IUserDirectoryPrincipal CreatePrincipal(SearchResult result)
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
                return new ActiveDirectoryGroup((GroupId)principalId);
            }
        }

        [Flags]
        private enum PrincipalSearchType
        {
            None = 0,
            Users = 1,
            Groups = 2,
            UsersAndGroups = Users | Groups
        }

        private string GetLdapRoot() => string.IsNullOrEmpty(this.DomainControllerAddress) 
                                                                ? ("LDAP://" + (this.UseLdaps ? ":636/" : string.Empty))
                                                                  : ($"LDAP://{this.DomainControllerAddress + (this.UseLdaps ? (this.DomainControllerAddress.Contains(":") ? string.Empty : ":636") : string.Empty)}/");

        private sealed class ActiveDirectoryUser : IUserDirectoryUser, IEquatable<ActiveDirectoryUser>
        {
            private readonly ADUserDirectory directory;
            private readonly UserId userId;

            public ActiveDirectoryUser(ADUserDirectory directory, UserId userId, string displayName, string emailAddress)
            {
                this.directory = directory;
                this.userId = userId ?? throw new ArgumentNullException(nameof(userId));
                this.DisplayName =  AH.CoalesceString(displayName, userId.Principal);
                this.EmailAddress = emailAddress;
            }

            string IUserDirectoryPrincipal.Name => this.userId.ToFullyQualifiedName();
            public string EmailAddress { get; }
            public string DisplayName { get; }

            public bool IsMemberOfGroup(string groupName)
            {
                if (groupName == null)
                    throw new ArgumentNullException(nameof(groupName));

                var userSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Users, this.userId.ToFullyQualifiedName());
                if (userSearchResult == null)
                    return false;

                var groupSet = LDAP.ExtractGroupNames(userSearchResult);
                var compareName = GroupId.Parse(groupName)?.Principal ?? groupName;
                if (groupSet.Contains(compareName))
                    return true;

                if (this.directory.SearchGroupsRecursively)
                {
                    var groupsToSearch = new Queue<string>(groupSet);
                    var groupsSearched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    while (groupsToSearch.Count > 0)
                    {
                        var nextGroup = groupsToSearch.Dequeue();
                        if (StringComparer.OrdinalIgnoreCase.Equals(nextGroup, compareName))
                            return true;

                        if (groupsSearched.Add(nextGroup))
                        {
                            var groupSearchResult = this.directory.TryGetPrincipal(PrincipalSearchType.Groups, nextGroup);
                            if (groupSearchResult != null)
                            {
                                var groupGroups = LDAP.ExtractGroupNames(groupSearchResult);
                                foreach (var g in groupGroups)
                                    groupsToSearch.Enqueue(g);
                            }
                        }
                    }
                }

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

            public ActiveDirectoryGroup(GroupId groupId)
            {
                this.groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            }

            string IUserDirectoryPrincipal.Name => this.groupId.ToFullyQualifiedName();
            string IUserDirectoryPrincipal.DisplayName => this.groupId.Principal;
            
            public bool IsMemberOfGroup(string groupName)
            {
                throw new NotSupportedException();
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
            public static CredentialedDomain Create(string input)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return null;

                var split = input.Split(',');

                if (split.Length < 2)
                    return new CredentialedDomain(split[0], null);

                var cred = SecureCredentials.Create(split[1], CredentialResolutionContext.None);
                var usernameCred = (cred ?? (cred as ResourceCredentials)?.ToSecureCredentials()) as UsernamePasswordCredentials;
                if (usernameCred == null)
                {
                    var typ = (usernameCred?.GetType() ?? cred?.GetType())?.AssemblyQualifiedName ?? "null";
                    throw new InvalidOperationException(
                        $"Credential {split[1]} has an unexpected type ({typ}); " +
                        $"expected {typeof(UsernamePasswordCredentials).AssemblyQualifiedName}" +
#pragma warning disable CS0618 // Type or member is obsolete
                        $" or {typeof(Inedo.Extensibility.Credentials.UsernamePasswordCredentials).AssemblyQualifiedName}" +
#pragma warning restore CS0618 // Type or member is obsolete
                        $".");
                }
                return new CredentialedDomain(split[0], usernameCred.UserName, AH.Unprotect(usernameCred.Password));
            }

            public CredentialedDomain(string name, string userName = null, string password = null)
            {
                this.Name = name ?? throw new ArgumentNullException(nameof(name));
                this.UserName = userName;
                this.Password = password;
            }

            public string Name { get; }
            public string UserName { get; }
            public string Password { get; }

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
                if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                    return false;

                return StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);
            }
        }
    }
}
