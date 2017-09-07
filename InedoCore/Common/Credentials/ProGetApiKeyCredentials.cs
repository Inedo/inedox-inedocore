using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Serialization;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions;
using Inedo.Otter.Extensions.Credentials;
#elif Hedgehog
using Inedo.Extensibility;
#endif

namespace Inedo.Extensions.Credentials
{
    //[ScriptAlias("ProGetApi")]
    //[DisplayName("ProGet API")]
    //[Description("Credentials that represent a connection to the ProGet API.")]
    //public sealed class ProGetApiKeyCredentials : ResourceCredentials, IInedoProductCredentials
    //{
    //    public ProGetApiKeyCredentials()
    //    {
    //    }

    //    [Persistent]
    //    [DisplayName("ProGet hostname")]
    //    public string Host { get; set; }
    //    [Persistent(Encrypted = true)]
    //    [DisplayName("API key")]
    //    public SecureString ApiKey { get; set; }

    //    [Description("If the feed requires authentication in addition to the API key, this user name will be used.")]
    //    [DisplayName("ProGet user name")]
    //    [Persistent]
    //    public string UserName { get; set; }

    //    [Description("If the feed requires authentication in addition to the API key, this password will be used.")]
    //    [DisplayName("ProGet password")]
    //    [FieldEditMode(FieldEditMode.Password)]
    //    [Persistent(Encrypted = true)]
    //    public SecureString Password { get; set; }

    //    public override RichDescription GetDescription()
    //    {
    //        return new RichDescription(this.Host);
    //    }
    //}
}
