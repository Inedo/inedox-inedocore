using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Inedo.Extensions.Operations.ProGet
{
    internal sealed class ProGetPackageVersionSpecifier
    {
        private static readonly LazyRegex VersionRegex = new LazyRegex(@"^(?<1>[0-9]+)(\.(?<2>[0-9]+)(?<3>\.([0-9]+(-.+)?)?)?)?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public ProGetPackageVersionSpecifier(string versionText)
        {
            this.Value = AH.CoalesceString(versionText, "latest");
            var match = VersionRegex.Match(versionText);
            if (!match.Groups[3].Success)
            {
                if (match.Groups[1].Success)
                    this.Major = BigInteger.Parse(match.Groups[1].Value);
                if (match.Groups[2].Success)
                    this.Minor = BigInteger.Parse(match.Groups[2].Value);
            }
        }
        
        public string Value { get; }
        public BigInteger Major { get; }
        public BigInteger Minor { get; }

        public string GetBestMatch(IEnumerable<string> packageVersions)
        {
            var versions = packageVersions.Select(UniversalPackageVersion.Parse).OrderByDescending(v => v).ToList();

            if (string.Equals(this.Value, "latest", StringComparison.OrdinalIgnoreCase))
            {
                return versions.FirstOrDefault()?.ToString();
            }
            else if (this.Major != null && this.Minor != null)
            {
                return versions.FirstOrDefault(v => v.Major == this.Major && v.Minor == this.Minor)?.ToString();
            }
            else if (this.Major != null && this.Minor == null)
            {
                return versions.FirstOrDefault(v => v.Major == this.Major)?.ToString();
            }
            else
            {
                var semver = UniversalPackageVersion.Parse(this.Value);
                return versions.FirstOrDefault(v => v == semver)?.ToString();
            }
        }
    }
}
