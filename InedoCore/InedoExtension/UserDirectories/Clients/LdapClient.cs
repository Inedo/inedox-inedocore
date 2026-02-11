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

        public abstract Task ConnectAsync(string server, int? port, bool ldaps, bool bypassSslCertificate);
        public abstract Task BindAsync(NetworkCredential credentials);
        public abstract IAsyncEnumerable<LdapClientEntry> SearchAsync(string distinguishedName, string filter, LdapClientSearchScope scope);
        
        // these are used by OpenLdap for now; when refactoring, should be combined
        public abstract Task BindUsingDnAsync(string bindDn, string password);
        public abstract IAsyncEnumerable<LdapClientEntry> SearchV2Async(string distinguishedName, string filter, LdapClientSearchScope scope, params string[] attributes);

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
