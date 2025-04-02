using System;
using System.Collections.Generic;
using System.Net;

namespace Inedo.Extensions.UserDirectories
{
    internal abstract class LdapClient : IDisposable
    {
        protected LdapClient()
        {
        }

        public abstract void Connect(string server, int? port, bool ldaps, bool bypassSslCertificate);
        public abstract void Bind(NetworkCredential credentials);
        public abstract IEnumerable<LdapClientEntry> Search(string distinguishedName, string filter, LdapClientSearchScope scope);
        
        // these are used by OpenLdap for now; when refactoring, should be combined
        public abstract void BindUsingDn(string bindDn, string password);
        public abstract IEnumerable<LdapClientEntry> SearchV2(string distinguishedName, string filter, LdapClientSearchScope scope, params string[] attributes);

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
