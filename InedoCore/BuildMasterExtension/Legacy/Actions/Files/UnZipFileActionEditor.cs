using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.IO;
using Inedo.Web.Controls;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class UnZipFileActionEditor : ActionEditorBase
    {
        private SourceControlFileFolderPicker txtFileName;

        public override bool DisplayTargetDirectory { get { return true; } }
        public override string ServerLabel { get { return "On server:"; } }
        public override string TargetDirectoryLabel { get { return "To directory:"; } }

        public override ActionBase CreateFromForm()
        {
            return new UnZipFileAction
            {
                OverriddenSourceDirectory = PathEx.GetDirectoryName(this.txtFileName.Text),
                FileName = PathEx.GetFileName(this.txtFileName.Text)
            };
        }

        public override void BindToForm(ActionBase action)
        {
            var zipAct = (UnZipFileAction)action;
            this.txtFileName.Text = PathEx.Combine(zipAct.OverriddenSourceDirectory, zipAct.FileName);
        }

        protected override void CreateChildControls()
        {
            this.txtFileName = new SourceControlFileFolderPicker { Required = true };
            
            this.Controls.Add(
                new SlimFormField("Zip file path:", this.txtFileName)
            );
        }
    }
}
