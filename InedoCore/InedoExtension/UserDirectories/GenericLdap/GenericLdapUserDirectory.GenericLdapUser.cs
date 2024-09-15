using Inedo.Extensibility.UserDirectories;

namespace Inedo.Extensions.UserDirectories.GenericLdap;

public partial class GenericLdapUserDirectory
{
    internal sealed class GenericLdapUser : IUserDirectoryUser, IEquatable<GenericLdapUser>
    {
        private readonly GenericLdapUserDirectory directory;
        private readonly UserId userId;
        private readonly HashSet<string> isMemberOfGroupCache = new(StringComparer.OrdinalIgnoreCase);

        public Lazy<ISet<string>> Groups { get; }

        public GenericLdapUser(GenericLdapUserDirectory directory, LdapClientEntry entry)
        {
            this.directory = directory;
            userId = new UserId(entry.GetPropertyValue(directory.UserNameAttributeName), entry.GetDomainPath());
            userId.DistinguishedName = entry.DistinguishedName;
            DisplayName = AH.CoalesceString(entry.GetPropertyValue(directory.UserDisplayNameAttributeName), userId.Principal);
            EmailAddress = entry.GetPropertyValue(directory.EmailAddressAttributeName);
            DistinguishedName = entry.DistinguishedName;
            Groups = new Lazy<ISet<string>>(() => GetGroups(directory, entry), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        string IUserDirectoryPrincipal.Name => userId.ToFullyQualifiedName();
        public string EmailAddress { get; }
        public string DisplayName { get; }
        public string DistinguishedName { get; }

        public bool IsMemberOfGroup(string groupName)
        {
            Logger.Log(MessageLevel.Debug, "Begin GenericLdapUser IsMemberOfGroup", "Generic LDAP User Directory");
            ArgumentNullException.ThrowIfNull(groupName);
            if (isMemberOfGroupCache.Contains(groupName))
            {
                Logger.Log(MessageLevel.Debug, "End GenericLdapUser IsMemberOfGroup", "Generic LDAP User Directory");
                return true;
            }
            
            if (Groups.Value.Contains(groupName))
            {
                Logger.Log(MessageLevel.Debug, "End GenericLdapUser IsMemberOfGroup", "Generic LDAP User Directory");
                isMemberOfGroupCache.Add(groupName);
                return true;
            }

            Logger.Log(MessageLevel.Debug, "End GenericLdapUser IsMemberOfGroup", "Generic LDAP User Directory");
            return false;
        }

        public bool Equals(GenericLdapUser other) => userId.Equals(other?.userId);
        public bool Equals(IUserDirectoryUser other) => Equals(other as GenericLdapUser);
        public bool Equals(IUserDirectoryPrincipal other) => Equals(other as GenericLdapUser);
        public override bool Equals(object obj) => Equals(obj as GenericLdapUser);
        public override int GetHashCode() => userId.GetHashCode();
    }
}