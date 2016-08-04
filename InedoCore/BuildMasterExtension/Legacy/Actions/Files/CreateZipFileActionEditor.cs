using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.IO;
using Inedo.Web.Controls;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class CreateZipFileActionEditor : ActionEditorBase
    {
        private SourceControlFileFolderPicker txtFileName;

        public override bool DisplaySourceDirectory
        {
            get { return true; }
        }
        public override string SourceDirectoryLabel
        {
            get { return "From directory:"; }
        }
        public override string ServerLabel
        {
            get { return "From server:"; }
        }

        protected override void CreateChildControls()
        {
            this.txtFileName = new SourceControlFileFolderPicker
            {
                DisplayMode = SourceControlBrowser.DisplayModes.FoldersAndFiles,
                DefaultText = "$CurrentDirectory\\archive.zip"
            };

            this.Controls.Add(
                new SlimFormField("To file:", this.txtFileName)
            );
        }

        public override ActionBase CreateFromForm()
        {
            return new CreateZipFileAction
            {
                OverriddenTargetDirectory = PathEx.GetDirectoryName(this.txtFileName.Text),
                FileName = AH.CoalesceStr(PathEx.GetFileName(this.txtFileName.Text), "archive.zip")
            };
        }
        public override void BindToForm(ActionBase action)
        {
            var zipAction = (CreateZipFileAction)action;
            this.txtFileName.Text = PathEx.Combine(zipAction.OverriddenTargetDirectory ?? string.Empty, zipAction.FileName);
        }
    }
}
