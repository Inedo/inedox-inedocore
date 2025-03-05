namespace Inedo.Extensions.UserDirectories.Clients
{
    public sealed class LdapDomains
    {

        internal enum LdapClientSearchScope
        {
            Base,
            OneLevel,
            Subtree
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
    }
}
