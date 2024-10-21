using Inedo.Extensibility.UserDirectories;

namespace Inedo.Extensions.UserDirectories.GenericLdap;

public partial class GenericLdapUserDirectory
{
    private sealed class GenericLdapGroup : IUserDirectoryGroup, IEquatable<GenericLdapGroup>
    {
        private readonly GroupId groupId;
        private readonly GenericLdapUserDirectory directory;
        private readonly HashSet<string> isMemberOfGroupCache = new(StringComparer.OrdinalIgnoreCase);

        public Lazy<ISet<string>> Groups { get; }

        public GenericLdapGroup(GenericLdapUserDirectory directory, LdapClientEntry entry)
        {
            this.directory = directory;
            groupId = new GroupId(entry.GetPropertyValue(directory.GroupNameAttributeName), entry.GetDomainPath());
            groupId.DistinguishedName = entry.DistinguishedName;

            Groups = new Lazy<ISet<string>>(() => GetGroups(directory, entry), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public string Name => groupId.ToFullyQualifiedName();
        public string DisplayName => groupId.Principal;

        public bool IsMemberOfGroup(string groupName)
        {
            Logger.Log(MessageLevel.Debug, "Begin GenericLdapGroup IsMemberOfGroup", "Generic LDAP User Directory");
            ArgumentNullException.ThrowIfNull(groupName);
            if (isMemberOfGroupCache.Contains(groupName))
            {
                Logger.Log(MessageLevel.Debug, "End GenericLdapGroup IsMemberOfGroup", "Generic LDAP User Directory");
                return true;
            }

            if (Groups.Value.Contains(groupName))
            {
                Logger.Log(MessageLevel.Debug, "End GenericLdapGroup IsMemberOfGroup", "Generic LDAP User Directory");
                isMemberOfGroupCache.Add(groupName);
                return true;
            }

            Logger.Log(MessageLevel.Debug, "End GenericLdapGroup IsMemberOfGroup", "Generic LDAP User Directory");
            return false;
        }

        public IEnumerable<IUserDirectoryUser> GetMemberUsers()
        {
            Logger.Log(MessageLevel.Debug, "Begin GenericLdapGroup GetMembers", "Generic LDAP User Directory");
            if (directory.GroupSearchType != GroupSearchType.RecursiveSearchActiveDirectory)
            {
                var memberUsers = directory.FindUsers($"({directory.GroupMemberOfAttributeName}={groupId.DistinguishedName})");
                foreach (var memberUser in memberUsers)
                {
                    yield return memberUser;
                }

                if (directory.GroupSearchType == GroupSearchType.RecursiveSearch)
                {
                    var memberGroups = directory.FindGroups($"({directory.GroupMemberOfAttributeName}={groupId.DistinguishedName})").Cast<GenericLdapGroup>();
                    foreach (var memberGroup in memberGroups)
                    {
                        foreach (var memberUser in memberGroup.GetMemberUsers())
                        {
                            yield return memberUser;
                        }
                    }
                }
            }
            else
            {
                foreach (var userEntry in directory.FindUsers($"memberOf:1.2.840.113556.1.4.1941:={groupId.DistinguishedName}"))
                {
                    yield return userEntry;
                }
            }
            Logger.Log(MessageLevel.Debug, "End GenericLdapGroup GetMembers", "Generic LDAP User Directory");
        }

        public bool Equals(GenericLdapGroup other) => groupId.Equals(other?.groupId);
        public bool Equals(IUserDirectoryGroup other) => Equals(other as GenericLdapGroup);
        public bool Equals(IUserDirectoryPrincipal other) => Equals(other as GenericLdapGroup);
        public override bool Equals(object obj) => Equals(obj as GenericLdapGroup);
        public override int GetHashCode() => groupId.GetHashCode();
        public override string ToString() => groupId.Principal;
    }
}