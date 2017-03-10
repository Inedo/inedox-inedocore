using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.UserDirectories;
using Inedo.BuildMaster.Web.Controls;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Serialization;

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

        [Description("Specify list...")]
        // Searches an explicit list of domains
        SpecificDomains
    }

    [DisplayName("(New) Active Directory Domain")]
    [Description("BuildMaster will use the global catalog for the current forest, and also all domains which have an inbound trust. If you only have one domain, the LDAP directory should be used instead.")]
    public sealed class ADUserDirectory : UserDirectoryBase
    {
        private Lazy<HashSet<string>> domainsToSearch;

        [Persistent]
        [DisplayName("Search mode")]
        public ADSearchMode SearchMode { get; set; }

        [Persistent]
        [DisplayName("Domains to search")]
        [PlaceholderText("Only used when Search mode is Specific List")]
        [Category("Advanced")]
        public string[] DomainsToSearch { get; set; }

        [Persistent]
        [DisplayName("NETBIOS name mapping")]
        [PlaceholderText("Automatically discover")]
        [Description("A list of key/value pairs that map NETBIOS names to domain names (one per line); e.g. KRAMUS=us.kramerica.local")]
        [Category("Advanced")]
        public string[] NetBiosNameMaps { get; set; }

        public override string GetDescription() => "ADUserDirectory";

        public ADUserDirectory()
        {
            this.domainsToSearch = new Lazy<HashSet<string>>(() => buildDomainsToSearch(this));
        }

        public override IEnumerable<IUserDirectoryPrincipal> FindPrincipals(string searchTerm)
            => this.FindPrincipals(PrincipalSearchType.UsersAndGroups, searchTerm);

        private static HashSet<string> buildDomainsToSearch(ADUserDirectory instance)
        {
            instance.LogDebug($"Building Search Root Paths for SearchMode \"{ADSearchMode.CurrentDomain}\"...");

            if (instance.SearchMode == ADSearchMode.SpecificDomains)
                return new HashSet<string>(instance.DomainsToSearch ?? new string[0], StringComparer.OrdinalIgnoreCase);

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var domain = Domain.GetCurrentDomain())
            {
                paths.Add(domain.Name);
                instance.LogDebug($"Domain  \"{domain.Name}\" added.");

                if (instance.SearchMode == ADSearchMode.TrustedDomains)
                {
                    Action<TrustRelationshipInformationCollection> addTrusts = trusts =>
                    {
                        foreach (TrustRelationshipInformation trust in trusts)
                        {
                            instance.LogDebug($"Trust relationship found, source: {trust.SourceName}, target: {trust.TargetName}, type: {trust.TrustType}, direction: {trust.TrustDirection} ");
                            if (trust.TrustDirection == TrustDirection.Outbound)
                            {
                                instance.LogDebug("Trust direction was Outbound, ignoring.");
                            }
                            else
                            {
                                paths.Add(trust.TargetName);
                            }
                        }
                        if (trusts.Count == 0)
                            instance.LogDebug("No trust relationships found.");
                    };

                    instance.LogDebug($"Adding domain trust relationships...");
                    addTrusts(domain.GetAllTrustRelationships());

                    instance.LogDebug($"Getting current forest...");
                    using (var forest = Forest.GetCurrentForest())
                    {
                        instance.LogDebug($"Adding trust relationships from \"{forest.Name}\"...");
                        addTrusts(forest.GetAllTrustRelationships());
                    }
                }
            }

            return paths;
        }

        public override IEnumerable<IUserDirectoryUser> GetGroupMembers(string groupName)
        {
            throw new NotImplementedException();
        }

        public override IUserDirectoryUser TryGetAndValidateUser(string userName, string password)
        {
            var result = this.TryGetPrincipal(PrincipalSearchType.Users, userName);

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
                    this.LogDebug("Searcher could not find user \"{0}\", error was: {1}", userName, ex.ToString());
                    return null;
                }
            }

        }

        public override IUserDirectoryUser TryGetUser(string userName)
            => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.Users, userName)) as IUserDirectoryUser;

        public override IUserDirectoryGroup TryGetGroup(string groupName)
            => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.Groups, groupName)) as IUserDirectoryGroup;

        public override IUserDirectoryPrincipal TryGetPrincipal(string principalName)
            => this.CreatePrincipal(this.TryGetPrincipal(PrincipalSearchType.UsersAndGroups, principalName));

        public override IUserDirectoryUser TryParseLogonUser(string logonUser)
        {
            if (string.IsNullOrEmpty(logonUser))
                throw new ArgumentNullException(nameof(logonUser));
            
            var parts = logonUser.Split(new[] { '\\' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return null;

            var domain = LDAP.GetDomainNameFromNetbiosName(parts[0]);
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

            HashSet<string> domains;
            if (principalId == null)
            {
                this.LogDebug($"No domain specified, searching through aliases.");
                domains = this.domainsToSearch.Value;
            }
            else
            {
                this.LogDebug($"Domain alias \"{principalId.DomainAlias}\" will be used.");
                domains = new HashSet<string>();
                domains.Add(principalId.DomainAlias);
            }
            foreach (var domain in domains)
            {
                this.LogDebug($"Searching domain {domain}...");
                using (var entry = new DirectoryEntry("LDAP://DC=" + domain.Replace(".", ",DC=")))
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

            var categoryFilter = AH.Switch<PrincipalSearchType, string>(searchType)
                .Case(PrincipalSearchType.UsersAndGroups, "(|(objectCategory=user)(objectCategory=group))")
                .Case(PrincipalSearchType.Groups, "(objectCategory=group)")
                .Case(PrincipalSearchType.Users, "(objectCategory=user)")
                .End();

            var st = LDAP.Escape(searchTerm);
            var filter = $"(&{categoryFilter}(|(userPrincipalName={st}*)(sAMAccountName={st}*)(name={st}*)(displayName={st}*)))";

            foreach (var domainName in this.domainsToSearch.Value)
            {
                using (var entry = new DirectoryEntry("LDAP://DC=" + domainName.Replace(".", ",DC=")))
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

            if (principalId is UserId)
                return new ActiveDirectoryUser((UserId)principalId,
                    result.GetPropertyValue("displayName"),
                    result.GetPropertyValue("mail"),
                    this);
            else
                return new ActiveDirectoryGroup((GroupId)principalId);
        }
        private void GetParentGroups(PrincipalId principalId, HashSet<GroupId> groupList, bool recurse)
        {
            var escapedUserPrincipalName = LDAP.Escape(principalId.ToString());

            var filter = string.Format(
                "(&(|(objectCategory=user)(objectCategory=group))(|(userPrincipalName={0})(sAMAccountName={1})(name={1})))",
                LDAP.Escape(principalId.ToString()),
                LDAP.Escape(principalId.Principal)
            );

            try
            {
                using (var entry = new DirectoryEntry($"LDAP://" + principalId.GetDomainSearchPath()))
                using (var searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = filter;
                    var result = searcher.FindOne();
                    if (result == null)
                        return;

                    foreach (var group in result.ExtractGroups())
                    {
                        if (groupList.Add(group) && recurse)
                            this.GetParentGroups(group, groupList, true);
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogWarning("Failed to get active directory groups: {0}", ex.Message);
            }
        }
        
        [Flags]
        private enum PrincipalSearchType
        {
            Users = 1,
            Groups = 2,
            UsersAndGroups = Users + Groups
        }
        private sealed class ActiveDirectoryUser : IUserDirectoryUser, IEquatable<ActiveDirectoryUser>
        {
            private HashSet<GroupId> groups = new HashSet<GroupId>();
            private ADUserDirectory directory;
            private string principalName;
            private Lazy<HashSet<GroupId>> groupsLazy;

            public ActiveDirectoryUser(UserId userId, string displayName, string emailAddress,  ADUserDirectory directory)
            {
                this.UserName = userId.Principal;
                this.Domain = userId.DomainAlias;
                this.DisplayName =  AH.CoalesceString(displayName, this.UserName);
                this.EmailAddress = emailAddress;
                this.principalName = $"{userId.Principal}@{userId.DomainAlias}";

                this.groupsLazy = new Lazy<HashSet<GroupId>>(() => {
                    var groups = new HashSet<GroupId>();
                    directory.GetParentGroups(userId, groups, true);
                    return groups;
                });
                this.directory = directory;
            }

            public string UserName { get; }
            public string Domain { get;  }
            string IUserDirectoryPrincipal.Name => this.principalName;
            public string EmailAddress { get; }
            public string DisplayName { get; }

            public bool IsMemberOfGroup(GroupId group) => this.groupsLazy.Value.Contains(group);
            public bool IsMemberOfGroup(string groupName) => this.IsMemberOfGroup(GroupId.Parse(groupName) ?? new GroupId(groupName, this.Domain));
            
            public bool Equals(ActiveDirectoryUser other)
            {
                if (object.ReferenceEquals(this, other))
                    return true;
                if (object.ReferenceEquals(other, null))
                    return false;

                return string.Equals(this.Domain, other.Domain, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(this.UserName, other.UserName, StringComparison.OrdinalIgnoreCase);
            }
            public bool Equals(IUserDirectoryUser other) => this.Equals(other as ActiveDirectoryUser);
            public bool Equals(IUserDirectoryPrincipal other) => this.Equals(other as ActiveDirectoryUser);
            public override bool Equals(object obj) => this.Equals(obj as ActiveDirectoryUser);
            public override int GetHashCode()
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(this.Domain)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(this.UserName);
            }
        }

        private sealed class ActiveDirectoryGroup : IUserDirectoryGroup, IEquatable<ActiveDirectoryGroup>
        {
            private string principalName;

            public ActiveDirectoryGroup(GroupId groupId)
            {
                this.GroupName = groupId.Principal;
                this.Domain = groupId.DomainAlias;
                this.principalName = $"{groupId.Principal}@{groupId.DomainAlias}";
            }

            public string GroupName { get; }
            public string Domain { get; }            
            string IUserDirectoryPrincipal.Name => this.principalName;
            string IUserDirectoryPrincipal.DisplayName => this.GroupName;
            
            public bool IsMemberOfGroup(string groupName)
            {
                throw new NotSupportedException();
            }

            public bool Equals(ActiveDirectoryGroup other)
            {
                if (object.ReferenceEquals(this, other))
                    return true;
                if (object.ReferenceEquals(other, null))
                    return false;

                return string.Equals(this.Domain, other.Domain, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(this.GroupName, other.GroupName, StringComparison.OrdinalIgnoreCase);
            }
            public bool Equals(IUserDirectoryGroup other) => this.Equals(other as ActiveDirectoryGroup);
            public bool Equals(IUserDirectoryPrincipal other) => this.Equals(other as ActiveDirectoryGroup);
            public override bool Equals(object obj) => this.Equals(obj as ActiveDirectoryGroup);
            public override int GetHashCode()
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(this.Domain)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(this.GroupName);
            }
            public override string ToString() => this.GroupName;
            
        }
    }
}
