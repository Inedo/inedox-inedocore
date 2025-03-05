using System.DirectoryServices.Protocols;
using System.Net;
using System.Security;
using Inedo.Extensibility.UserDirectories;
using Inedo.Serialization;

namespace Inedo.Extensions.UserDirectories.GenericLdap
{
    [DisplayName("Generic LDAP Directory")]
    [Description("Queries an LDAP directory for users and group membership.")]
    public partial class GenericLdapUserDirectory : UserDirectory
    {
        #region Configuration Properties

        #region Connection

        [Persistent]
        [Category("Connection")]
        [DisplayName("LDAP hostname")]
        [PlaceholderText("Use Domain")]
        [Description("Specify the host name or IP address of the domain controller.")]
        public string Hostname { get; set; }

        [Persistent]
        [Category("Connection")]
        [DisplayName("LDAP Port Override")]
        [Description("This will override the port used to connect to LDAP or LDAPS.  If this is not set, then port 389 is used for LDAP and 636 is used for LDAPS.")]
        [PlaceholderText("Use default port")]
        public string Port { get; set; }

        [Persistent]
        [Category("Connection")]
        [DisplayName("LDAP Connection")]
        [DefaultValue(LdapConnectionType.Ldap)]
        [Description("Connect via LDAP, LDAP over SSL, or LDAP over SSL and bypass certificate errors.")]
        public LdapConnectionType LdapConnection { get; set; }

        [Persistent]
        [Category("Connection")]
        [DisplayName("Bind Username")]
        [Description("User name for LDAP credentials that have READ access to the domain")]
        public string BindUsername { get; set; }

        [Persistent(Encrypted = true)]
        [Category("Connection")]
        [DisplayName("Bind Password")]
        [Description("BindPassword for LDAP credentials that have READ access to the domain")]
        public SecureString BindPassword { get; set; }

        [Persistent]
        [Category("Connection")]
        [DisplayName("Base DN")]
        [PlaceholderText("Leave blank to search the root of the directory.")]
        [Description("Base DN to use when searcing for users and groups.")]
        public string BaseDN { get; set; }

        #endregion

        #region User Search

        [Persistent]
        [Category("User Search")]
        [DisplayName("Users LDAP Filter")]
        [DefaultValue("(objectCategory=user)")]
        [PlaceholderText("(objectCategory=user)")]
        public string UsersFilterBase { get; set; } = "(objectCategory=user)";

        [Persistent]
        [Category("User Search")]
        [DisplayName("User Name Attribute")]
        [DefaultValue("sAMAccountName")]
        [PlaceholderText("sAMAccountName")]
        public string UserNameAttributeName { get; set; } = "sAMAccountName";

        [Persistent]
        [Category("User Search")]
        [DisplayName("User Display Name Attribute")]
        [DefaultValue("displayName")]
        [PlaceholderText("displayName")]
        public string UserDisplayNameAttributeName { get; set; } = "displayName";

        [Persistent]
        [Category("User Search")]
        [DisplayName("Email Attribute")]
        [DefaultValue("mail")]
        [PlaceholderText("mail")]
        public string EmailAddressAttributeName { get; set; } = "mail";

        #endregion

        #region Group Search

        [Persistent]
        [Category("Group Search")]
        [DisplayName("Groups LDAP Filter")]
        [DefaultValue("(objectCategory=group)")]
        [PlaceholderText("(objectCategory=group)")]
        public string GroupsFilterBase { get; set; } = "(objectCategory=group)";

        [Persistent]
        [Category("Group Search")]
        [DisplayName("Group Name Attribute")]
        [DefaultValue("name")]
        [PlaceholderText("name")]
        public string GroupNameAttributeName { get; set; } = "name";

        [Persistent]
        [Category("Group Search")]
        [DisplayName("Search Group Method")]
        [DefaultValue(GroupSearchType.NoRecursion)]
        [Description("Choose to recursively check group memberships or only check for the groups that a user is directly a member of. This may cause reduced performance.")]
        public GroupSearchType GroupSearchType { get; set; }

        [Persistent]
        [Category("Group Search")]
        [DisplayName("Group Membership Property Value")]
        [DefaultValue("memberof")]
        [PlaceholderText("memberof")]
        [Description("This property will only be used when \"No Recursion\" or \"Recursive Search (LDAP/Non-Active Directory)\" is set for the group search type.  When the group search type is \"Recursive Search (Active Directory Only)\" a special Active Directory query is used to find groups.")]
        public string GroupMemberOfAttributeName { get; set; } = "memberof";

        private string[] RequiredAttributes
        {
            get
            {
                return new[]
                {
                    UserNameAttributeName, UserDisplayNameAttributeName, EmailAddressAttributeName,
                    GroupNameAttributeName, GroupMemberOfAttributeName
                };
            }
        }

        #endregion

        #endregion

        #region Overrides of UserDirectory

        /// <inheritdoc />
        public override IUserDirectoryUser TryGetUser(string userName)
        {
            // Username might be fully qualified:  user@domain
            try
            {
                if (TryParseFullyQualifiedPrincipalName(userName, out string user, out _))
                {
                    userName = user;
                }

                var result = FindUsers(userName).FirstOrDefault();
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc />
        public override IUserDirectoryUser TryGetAndValidateUser(string userName, string password)
        {
            var user = TryGetUser(userName) as GenericLdapUser;
            if (user == null)
            {
                return null;
            }

            try
            {
                using var connection = CreateConnectionAndBindUser(user.DistinguishedName, password);
            }
            catch
            {
                return null;
            }

            return user;
        }

        /// <inheritdoc />
        public override IUserDirectoryGroup TryGetGroup(string groupName)
        {
            try
            {
                if (TryParseFullyQualifiedPrincipalName(groupName, out string group, out _))
                {
                    groupName = group;
                }

                var result = FindGroups(groupName).FirstOrDefault();
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc />
        public override IEnumerable<IUserDirectoryUser> GetGroupMembers(string groupName)
        {
            var group = (GenericLdapGroup)TryGetGroup(groupName);
            return group?.GetMemberUsers()?.ToList() ?? [];
        }

        /// <inheritdoc />
        public override IEnumerable<IUserDirectoryPrincipal> FindPrincipals(string searchTerm)
        {
            // We want any groups that also match the search terms plus any users that match the search terms
            return FindUsers(searchTerm).Cast<IUserDirectoryPrincipal>().Union(FindGroups(searchTerm));
        }

        /// <inheritdoc />
        public override IEnumerable<IUserDirectoryUser> FindUsers(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                return [];
            }

            string filter = UsersFilterBase;
            // If the passed-in search term contains an = sign, assume it is already a filter (ex: the AD recursive group member search) and append it as-is
            if (searchTerm.Contains("="))
            {
                filter = CombineFiltersAnd(filter, searchTerm);
            }
            else
            {
                filter = CombineFiltersAnd(filter, $"{UserNameAttributeName}={searchTerm}*");
            }

            var results = Search(BaseDN, filter);
            return results.Select(LdapClientEntryAsUser);
        }

        /// <inheritdoc />
        public override IEnumerable<IUserDirectoryGroup> FindGroups(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                return [];
            }

            string filter = GroupsFilterBase;

            // If the passed-in search term contains an = sign, assume it is already a filter (ex: the AD recursive group member search) and append it as-is
            if (searchTerm.Contains("="))
            {
                filter = CombineFiltersAnd(filter, searchTerm);
            }
            else
            {
                filter = CombineFiltersAnd(filter, $"{GroupNameAttributeName}={searchTerm}*");
            }

            var results = Search(BaseDN, filter);
            return results.Select(LdapClientEntryAsGroup);
        }

        #endregion

        #region Private Methods

        private LdapClient GetClient()
        {
            return OperatingSystem.IsWindows()
                ? new DirectoryServicesLdapClient(AuthType.Basic, RequiredAttributes)
                : new NovellLdapClient(RequiredAttributes);
        }

        private static bool TryParseFullyQualifiedPrincipalName(string fullyQualifiedName, out string principalName, out string domainName)
        {
            // Username here may be fully qualified:  user@domain
            string[] parts = fullyQualifiedName.Split('@');
            if (parts.Length < 2)
            {
                principalName = null;
                domainName = null;
                return false;
            }

            // Everything after the last @ is considered the domain, everything before that is the name
            // Allow for multiple @ signs (allowing for cases where the user itself is an email address - a@gmail.com@domain.com)
            domainName = parts[parts.Length - 1];
            principalName = string.Join('@', parts.SkipLast(1));
            return true;

        }

        private static string CombineFiltersAnd(string baseFilter, string additionalFilter)
        {
            string newFilter = baseFilter;
            if (additionalFilter != null)
            {
                if (baseFilter.First() != '(')
                {
                    baseFilter = $"({baseFilter})";
                }

                if (additionalFilter.First() != '(')
                {
                    additionalFilter = $"({additionalFilter})";
                }
                newFilter = $"(&{baseFilter}{additionalFilter})";
            }

            return newFilter;
        }

        private IEnumerable<LdapClientEntry> Search(string baseDN, string filter)
        {
            this.LogInformation($"Generic LDAP search for filter \"{filter}\" in base DN \"baseDN\"...");
            using var connection = CreateConnectionAndBindUser();
            var entries = connection.Search(baseDN, filter, LdapClientSearchScope.Subtree);
            return entries.ToList();
        }

        private LdapClient CreateConnectionAndBindUser(string username = null, string password = null)
        {
            NetworkCredential credential = null;
            if (username != null && password != null)
            {
                credential = new NetworkCredential(username, password);
            }
            else if (BindUsername != null && BindPassword != null)
            {
                credential = new NetworkCredential(BindUsername, BindPassword);
            }

            return CreateConnectionAndBindUser(credential);
        }

        private LdapClient CreateConnectionAndBindUser(NetworkCredential credential)
        {
            LdapClient conn = null;
            
            try
            {
                conn = GetClient();
                conn.Connect(AH.NullIf(Hostname, string.Empty), int.TryParse(Port, out var port) ? port : null, LdapConnection != LdapConnectionType.Ldap, LdapConnection == LdapConnectionType.LdapsWithBypass);
                if (credential != null)
                {
                    conn.Bind(credential);
                }

                return conn;
            }
            catch
            {
                conn?.Dispose();
                throw;
            }
        }

        private GenericLdapUser LdapClientEntryAsUser(LdapClientEntry entry)
        {
            var user = new GenericLdapUser(this, entry);
            return user;
        }

        private GenericLdapGroup LdapClientEntryAsGroup(LdapClientEntry entry)
        {
            var group = new GenericLdapGroup(this, entry);
            return group;
        }

        private static ISet<string> GetGroups(GenericLdapUserDirectory directory, LdapClientEntry entry)
        {
            var groups = new List<string>();
            //Old Group searching way
            if (directory.GroupSearchType != GroupSearchType.RecursiveSearchActiveDirectory)
            {
                var parentGroupNames = entry.ExtractGroupNames(directory.GroupMemberOfAttributeName, directory.GroupNameAttributeName, true);
                foreach (var childGroupName in parentGroupNames)
                {
                    groups.Add(childGroupName);
                }

                groups.Sort();
                if (directory.GroupSearchType == GroupSearchType.RecursiveSearch)
                {
                    foreach (var parentGroupName in parentGroupNames)
                    {
                        var parentGroup = (GenericLdapGroup)directory.TryGetGroup(parentGroupName);
                        if (parentGroup != null)
                        {
                            foreach (string childMemberGroup in parentGroup.Groups.Value)
                            {
                                groups.Add(childMemberGroup);
                            }
                        }
                    }
                }
            }
            // New AD-only way
            else
            {
                foreach (var group in directory.FindGroups($"member:1.2.840.113556.1.4.1941:={entry.DistinguishedName}"))
                {
                    groups.Add(group.Name);
                }
            }

            return new HashSet<string>(groups);
        }

        #endregion
    }
}
