namespace Inedo.Extensions.UserDirectories.Clients;

internal abstract class LdapClientEntry()
{
    public abstract string DistinguishedName { get; }
    public abstract string GetPropertyValue(string propertyName);
    public abstract ISet<string> ExtractGroupNames(string memberOfPropertyName = null);

    public string GetDomainPath() => LdapHelperV4.GetDomainPath(this.DistinguishedName);
}
