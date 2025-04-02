#if !NET452
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using Inedo.Diagnostics;
using Novell.Directory.Ldap;
using Logger = Inedo.Diagnostics.Logger;

namespace Inedo.Extensions.UserDirectories
{
    internal sealed class NovellLdapClient : LdapClient
    {
        private LdapConnection connection;

        public override void Connect(string server, int? port, bool ldaps, bool bypassSslCertificate)
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
            this.connection.Connect(server, port ?? (ldaps ? 636 : 389));            
        }

        public override void Bind(NetworkCredential credentials)
        {
            this.connection.Bind($"{credentials.UserName}{(string.IsNullOrWhiteSpace(credentials.Domain) ? string.Empty : "@" + credentials.Domain)}", credentials.Password);
        }
        public override IEnumerable<LdapClientEntry> Search(string distinguishedName, string filter, LdapClientSearchScope scope)
        {
            return getResults(this.connection.Search(distinguishedName, (int)scope, filter, null, false, this.connection.SearchConstraints));

            static IEnumerable<LdapClientEntry> getResults(ILdapSearchResults results)
            {
                Logger.Log(MessageLevel.Debug, "Begin LDAP Get Search Results", "AD User Directory");
                while (results.HasMore())
                {
                    LdapEntry entry;
                    try
                    {
                        entry = results.Next();
                    }
                    catch (LdapReferralException lrex)
                    {
                        //Logger.Log(MessageLevel.Debug, $"Referral chasing enabled: {connection.SearchConstraints.ReferralFollowing}", "AD User Directory");
                        Logger.Log(MessageLevel.Debug, "LdapReferralException", "AD User Directory", lrex.ToString(), lrex);
                        entry = null;
                    }
                    catch(LdapException lex)
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
                    catch(Exception ex)
                    {
                        Logger.Log(MessageLevel.Debug, ex.GetType().Name, "AD User Directory", ex.ToString(), ex);
                        throw;
                    }

                    if (entry != null)
                        yield return new Entry(entry);
                }
                Logger.Log(MessageLevel.Debug, "End LDAP Get Search Results", "AD User Directory");
            }
        }

        public override void BindV2(string bindDn, string password)
        {
            this.connection.Bind(bindDn, password);
        }
        public override IEnumerable<LdapClientEntry> SearchV2(string distinguishedName, string filter, LdapClientSearchScope scope, params string[] attributes)
        {
            return getResults(this.connection.Search(distinguishedName, (int)scope, filter, attributes, false, this.connection.SearchConstraints)).ToList();

            static IEnumerable<LdapClientEntry> getResults(ILdapSearchResults results)
            {
                Logger.Log(MessageLevel.Debug, "Begin LDAP Get Search Results", "AD User Directory");
                while (results.HasMore())
                {
                    LdapEntry entry;
                    try
                    {
                        entry = results.Next();
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
                    return this.entry.GetAttribute(propertyName)?.StringValue;
                }
                catch
                {
                    return null;
                }
            }

            public override ISet<string> ExtractGroupNames(string memberOfPropertyName = null)
            {
                var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var memberOf in this.entry.GetAttribute(AH.NullIf(memberOfPropertyName, string.Empty) ?? "memberof")?.StringValueArray ?? Array.Empty<string>())
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
}
#endif
