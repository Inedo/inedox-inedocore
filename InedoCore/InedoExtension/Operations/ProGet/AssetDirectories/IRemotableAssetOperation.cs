using Inedo.Diagnostics;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    internal interface IRemotableAssetOperation : ILogSink
    {
        string Path { get; }
        string ApiUrl { get; }
        string ApiKey { get; }
        string UserName { get; }
        string Password { get; }
    }
}
