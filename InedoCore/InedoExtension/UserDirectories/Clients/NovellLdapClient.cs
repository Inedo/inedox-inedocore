using System.Net;
using System.Text.Json;
using Novell.Directory.Ldap;
using Logger = Inedo.Diagnostics.Logger;

namespace Inedo.Extensions.UserDirectories;

internal sealed class NovellLdapClient : LdapClient
{
    private LdapConnection connection;

    public override Task ConnectAsync(string server, int? port, bool ldaps, bool bypassSslCertificate)
    {
        this.connection = new LdapConnection();
        if (ldaps)
        {
            this.connection.SecureSocketLayer = true;
            if (bypassSslCertificate)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                this.connection.UserDefinedServerCertValidationDelegate += (sender, certificate, chain, sslPolicyErrors) => true;
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }
        
        return this.connection.ConnectAsync(server, port ?? (ldaps ? 636 : 389));            
    }

    public override Task BindAsync(NetworkCredential credentials)
    {
        return this.connection.BindAsync($"{credentials.UserName}{(string.IsNullOrWhiteSpace(credentials.Domain) ? string.Empty : "@" + credentials.Domain)}", credentials.Password);
    }
    public override async IAsyncEnumerable<LdapClientEntry> SearchAsync(string distinguishedName, string filter, LdapClientSearchScope scope)
    {
        var results = await this.connection.SearchAsync(distinguishedName, (int)scope, filter, null, false, this.connection.SearchConstraints);

        Logger.Log(MessageLevel.Debug, "Begin LDAP Get Search Results", "AD User Directory");
        while (await results.HasMoreAsync())
        {
            LdapEntry entry;
            try
            {
                entry = await results.NextAsync();
            }
            catch (LdapReferralException lrex)
            {
                //Logger.Log(MessageLevel.Debug, $"Referral chasing enabled: {connection.SearchConstraints.ReferralFollowing}", "AD User Directory");
                Logger.Log(MessageLevel.Debug, "LdapReferralException", "AD User Directory", lrex.ToString(), lrex);
                entry = null;
            }
            catch (LdapException lex)
            {
                Logger.Log(MessageLevel.Debug, "LdapException", "AD User Directory", lex.ToString(), lex);
                try
                {
                    Logger.Log(MessageLevel.Debug, "LdapException", "AD User Directory", JsonSerializer.Serialize(lex));
                }
                catch
                {
                    Logger.Log(MessageLevel.Debug, "Couldn't serialize LdapException", "AD User Directory");
                }

                throw;
            }
            catch (Exception ex)
            {
                Logger.Log(MessageLevel.Debug, ex.GetType().Name, "AD User Directory", ex.ToString(), ex);
                throw;
            }

            if (entry != null)
                yield return new Entry(entry);
        }

        Logger.Log(MessageLevel.Debug, "End LDAP Get Search Results", "AD User Directory");
    }

    public override Task BindUsingDnAsync(string bindDn, string password)
    {
        return this.connection.BindAsync(bindDn, password);
    }
    public override async IAsyncEnumerable<LdapClientEntry> SearchV2Async(string distinguishedName, string filter, LdapClientSearchScope scope, params string[] attributes)
    {
        var results = await this.connection.SearchAsync(distinguishedName, (int)scope, filter, attributes, false, this.connection.SearchConstraints);

        Logger.Log(MessageLevel.Debug, "Begin LDAP Get Search Results", "AD User Directory");
        while (await results.HasMoreAsync())
        {
            LdapEntry entry;
            try
            {
                entry = await results.NextAsync();
            }
            catch (LdapReferralException lrex)
            {
                //Logger.Log(MessageLevel.Debug, $"Referral chasing enabled: {connection.SearchConstraints.ReferralFollowing}", "AD User Directory");
                Logger.Log(MessageLevel.Debug, "LdapReferralException", "AD User Directory", lrex.ToString(), lrex);
                entry = null;
            }
            catch (LdapException lex)
            {
                Logger.Log(MessageLevel.Debug, "LdapException", "AD User Directory", lex.ToString(), lex);
                try
                {
                    Logger.Log(MessageLevel.Debug, "LdapException", "AD User Directory", JsonSerializer.Serialize(lex));
                }
                catch
                {
                    Logger.Log(MessageLevel.Debug, "Couldn't serialize LdapException", "AD User Directory");
                }

                throw;
            }
            catch (Exception ex)
            {
                Logger.Log(MessageLevel.Debug, ex.GetType().Name, "AD User Directory", ex.ToString(), ex);
                throw;
            }

            if (entry != null)
                yield return new Entry(entry);
        }

        Logger.Log(MessageLevel.Debug, "End LDAP Get Search Results", "AD User Directory");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            this.connection?.Dispose();

        base.Dispose(disposing);
    }

    private sealed class Entry : LdapClientEntry
    {
        private readonly LdapEntry entry;

        public Entry(LdapEntry entry) => this.entry = entry;

        public override string DistinguishedName => this.entry.Dn;

        public override string GetPropertyValue(string propertyName)
        {
            try
            {
                return this.entry.GetAttributeSet(propertyName)?.FirstOrDefault().Value?.StringValue;
            }
            catch
            {
                return null;
            }
        }

        public override HashSet<string> ExtractGroupNames(string memberOfPropertyName = null)
        {
            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var memberOf in this.entry.GetAttributeSet(AH.NullIf(memberOfPropertyName, string.Empty) ?? "memberof")?.FirstOrDefault().Value?.StringValueArray ?? [])
                {
                    var groupNames = from part in memberOf.Split(',')
                                     where part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
                                     let name = part["CN=".Length..]
                                     where !string.IsNullOrWhiteSpace(name)
                                     select name;

                    groups.UnionWith(groupNames);
                }
            }
            catch
            {
            }

            return groups;
        }
    }
}
