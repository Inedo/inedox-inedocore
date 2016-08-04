using System.ComponentModel;
using Inedo.Agents;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.CreateZipFileAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.Files.CreateZip))]
    [DisplayName("Create Zip File")]
    [Description("Creates a .zip archive of a folder.")]
    [CustomEditor(typeof(CreateZipFileActionEditor))]
    [RequiresInterface(typeof(IFileOperationsExecuter))]
    [Tag(Tags.Files)]
    public sealed class CreateZipFileAction : AgentBasedActionBase
    {
        [Persistent]
        public string FileName { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Zip ",
                    new Hilite("*"),
                    " to ",
                    new Hilite(this.FileName)
                ),
                new RichDescription(
                    "from all files in ",
                    new DirectoryHilite(this.OverriddenSourceDirectory),
                    " to ",
                    new DirectoryHilite(this.OverriddenTargetDirectory, this.FileName)
                )
            );
        }

        protected override void Execute()
        {
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();

            var filePath = fileOps.CombinePath(this.Context.TargetDirectory, this.FileName);
            this.LogInformation("Creating file {0}...", filePath);

            fileOps.CreateZipFile(this.Context.SourceDirectory, filePath);
            this.LogDebug("Zip file created.");
        }
    }
}
