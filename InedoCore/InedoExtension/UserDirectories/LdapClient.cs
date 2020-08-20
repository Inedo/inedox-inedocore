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

        public abstract void Connect(string server, int? port, bool ldaps);
        public abstract void Bind(NetworkCredential credentials);
        public abstract IEnumerable<LdapClientEntry> Search(string distinguishedName, string filter, LdapClientSearchScope scope);

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
