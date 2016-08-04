using System.ComponentModel;
using Inedo.Agents;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.UnZipFileAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.Files.ExtractZip))]
    [DisplayName("Unzip File")]
    [Description("Extracts a .zip archive to a directory.")]
    [CustomEditor(typeof(UnZipFileActionEditor))]
    [RequiresInterface(typeof(IFileOperationsExecuter))]
    [Tag(Tags.Files)]
    public sealed class UnZipFileAction : AgentBasedActionBase
    {
        [Persistent]
        public string FileName { get ; set ; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription("Unzip ", new Hilite(this.FileName)),
                new RichDescription(
                    "from ", new DirectoryHilite(this.OverriddenSourceDirectory),
                    " to ", new DirectoryHilite(this.OverriddenTargetDirectory)
                )
            );
        }

        protected override void Execute()
        {            
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();

            var filePath = fileOps.CombinePath(this.Context.SourceDirectory, this.FileName);
            this.LogInformation("Extracting zip file \"{0}\"...", this.FileName);
            this.LogDebug("Extracting contents of zip file from \"{0}\" to \"{1}\"...", filePath, this.Context.TargetDirectory);

            if (!fileOps.FileExists(filePath))
            {
                this.LogError("The zip file was not found on the selected server.");
                return;
            }

            fileOps.ExtractZipFile(filePath, this.Context.TargetDirectory, true);
            this.LogInformation("Zip file extracted.");
        }
    }
}
