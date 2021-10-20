using System.Collections.Generic;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    internal interface IUploadAssets : IRemotableAssetOperation
    {
        IEnumerable<string> Includes { get; }
        IEnumerable<string> Excludes { get; }
        string SourceDirectory { get; }
    }
}
