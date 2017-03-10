using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using Inedo.BuildMaster.Extensibility.UserDirectories;

namespace Inedo.Extensions.UserDirectories
{
    /// <summary>
    /// A way to uniquely identify a user or group principal in a forest
    /// </summary>
    /// <remarks>
    /// This is similar to the [userPrincipalName] property (user@domain.local), except that the prefix is
    /// the name ([sAMAccountName] or [name]) and the suffix is a period-separated concatenation of all of the 
    /// domain components (fully-qualified)
    ///  
    /// In most cases this will be identical to the [userPrincipalName], but in some cases its not. 
    /// Also, [userPrincipalName] is an *optional* field on a user, where as [sAMAccountName] is not
    /// 
    /// See: https://msdn.microsoft.com/en-us/library/ms677605(v=vs.85).aspx
    /// </remarks>
    internal abstract class PrincipalId : IEquatable<PrincipalId>
    {
        protected PrincipalId(string principal, string domainAlias)
        {
            if (string.IsNullOrEmpty(principal)) throw new ArgumentNullException(nameof(principal));
            if (string.IsNullOrEmpty(domainAlias)) throw new ArgumentNullException(nameof(domainAlias));

            this.DomainAlias = domainAlias;
            this.Principal = principal;
        }

        public string DomainAlias { get; }
        public string Principal { get; }

        public string ToFullyQualifiedName() => $"{this.Principal}@{this.DomainAlias}";
        public string GetDomainSearchPath() => "DC=" + this.DomainAlias.Replace(".", ",DC=");

        public static PrincipalId FromSearchResult(SearchResult result)
        {
            var objectCategory = result.GetPropertyValue("objectCategory");
            if (objectCategory == null)
                return null;

            var isUser = objectCategory.IndexOf("CN=Person", StringComparison.OrdinalIgnoreCase) >= 0;

            var principalName = result.GetPropertyValue("sAMAccountName");

            if (!isUser && string.IsNullOrWhiteSpace(principalName))
                principalName = result.GetPropertyValue("name");

            if (string.IsNullOrWhiteSpace(principalName))
                return null;

            var domain = result.GetDomainPath();
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            if (isUser)
                return new UserId(domain, principalName);
            else
                return new GroupId(domain, principalName);
        }       

        public bool Equals(PrincipalId other)
        {
            if (object.ReferenceEquals(this, other))
                return true;
            if (object.ReferenceEquals(other, null))
                return false;

            return string.Equals(this.DomainAlias, other.DomainAlias, StringComparison.OrdinalIgnoreCase)
                && string.Equals(this.Principal, other.Principal, StringComparison.OrdinalIgnoreCase);
        }
        public override bool Equals(object obj) => this.Equals(obj as PrincipalId);
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(this.DomainAlias)
                ^ StringComparer.OrdinalIgnoreCase.GetHashCode(this.Principal);
        }
    }
    internal sealed class GroupId : PrincipalId
    {
        public GroupId(string principal, string domain)
            : base(principal, domain)
        {
        }
        public static GroupId Parse(string value)
        {
            var parts = value.Split(new[] { '@' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
                return new GroupId(parts[0], parts[1]);

            return null;
        }
    }
    internal sealed class UserId : PrincipalId
    {
        public UserId(string principal, string domain)
            : base(principal, domain)
        {
        }
        public static UserId Parse(string value)
        {
            var parts = value.Split(new[] { '@' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
                return new UserId(parts[0], parts[1]);

            return null;
        }
    }
}
