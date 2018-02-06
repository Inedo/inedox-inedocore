using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.IO;
using Inedo.Web.Controls;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class CreateFileActionEditor : ActionEditorBase
    {
        private Inedo.Web.FileBrowserTextBox txtFileName;
        private TextBox txtFileContents;

        public override string ServerLabel => "On server:";

        protected override void CreateChildControls()
        {
            this.txtFileName = new Inedo.Web.FileBrowserTextBox
            {
                IncludeFiles = true,
                Required = true
            };

            this.txtFileContents = new TextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Wrap = false,
                Rows = 20
            };

            this.Controls.Add(
                new SlimFormField("File path:", this.txtFileName),
                new SlimFormField("File Contents:", this.txtFileContents)
            );
        }

        public override void BindToForm(ActionBase action)
        {
            var file = (CreateFileAction)action;

            this.txtFileName.Text = PathEx.Combine(file.OverriddenSourceDirectory ?? string.Empty, file.FileName);
            this.txtFileContents.Text = file.Contents;
        }

        public override ActionBase CreateFromForm()
        {
            return new CreateFileAction
            {
                OverriddenSourceDirectory = PathEx.GetDirectoryName(this.txtFileName.Text),
                FileName = PathEx.GetFileName(this.txtFileName.Text),
                Contents = this.txtFileContents.Text
            };
        }
    }
}
