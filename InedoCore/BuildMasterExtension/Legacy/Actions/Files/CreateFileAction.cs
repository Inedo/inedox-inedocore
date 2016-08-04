using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using Inedo.Agents;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.CreateFileAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.Files.CreateFile))]
    [DisplayName("Create File")]
    [Description("Creates a text file.")]
    [CustomEditor(typeof(CreateFileActionEditor))]
    [RequiresInterface(typeof(IFileOperationsExecuter))]
    [Tag(Tags.Files)]
    public sealed class CreateFileAction : AgentBasedActionBase
    {
        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Create File ",
                    new Hilite(this.FileName)
                ),
                new RichDescription(
                    "in ",
                    new DirectoryHilite(this.OverriddenSourceDirectory),
                    " starting with ",
                    new Hilite(this.Contents)
                )
            );
        }

        [Persistent]
        public string FileName { get; set; }
        [Persistent]
        public string Contents { get; set; }

        protected override void Execute()
        {
            this.LogDebug("Creating file {0}...", this.FileName);

            var op = this.Context.Agent.GetService<IFileOperationsExecuter>();
            var text = this.Contents.Replace(Environment.NewLine, op.NewLine);
            var filePath = op.CombinePath(this.Context.SourceDirectory, this.FileName);

            using (var stream = op.OpenFile(filePath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(text);
            }

            this.LogInformation("File created.");
        }
    }
}
