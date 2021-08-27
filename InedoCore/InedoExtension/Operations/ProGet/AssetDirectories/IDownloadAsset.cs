namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    internal interface IDownloadAsset : IRemotableAssetOperation
    {
        string TargetDirectory { get; }
    }
}
