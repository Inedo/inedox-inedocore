using System.ComponentModel;

namespace Inedo.Extensions.UserDirectories
{
    public enum ADSearchMode
    {
        [Description("Current domain only")]
        // Searches only within the domain of the current user credentials in effect for the security context under which the application is running.
        CurrentDomain,

        [Description("All trusted domains")]
        // Searches all domains with an Inbound or Bidirectional Trust relationship of the current user credentials in effect for the security context under which the application is running.
        TrustedDomains,

        [Description("Specific list...")]
        // Searches an explicit list of domains
        SpecificDomains
    }
}
