using System;
using System.Collections.Generic;
using System.Net;

namespace Inedo.Extensions.UserDirectories.Clients
{
    internal abstract class LdapClient : IDisposable
    {
        protected LdapClient()
        {
        }

        public abstract void Connect(string server, int? port, bool ldaps, bool bypassSslCertificate);
        public abstract void Bind(NetworkCredential credentials);
        public abstract void Bind(string bindDn, string password);
        public abstract IEnumerable<LdapClientEntry> Search(string distinguishedName, string filter, LdapClientSearchScope scope);
        public abstract IEnumerable<LdapClientEntry> SearchV2(string distinguishedName, string filter, LdapClientSearchScope scope, params string[] attributes);

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
