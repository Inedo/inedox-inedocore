using System;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class DeleteFilesActionEditor : ActionEditorBase
    {
        private TextBox txtDeleteFiles;
        private CheckBox chkRecursive;
        private CheckBox chkLogVerbose;

        public override string ServerLabel 
        {
            get { return "On server:"; }
        }
        public override string SourceDirectoryLabel
        {
            get { return "In directory:"; }
        }
        public override bool DisplaySourceDirectory
        {
            get { return true; }
        }

        public override void BindToForm(ActionBase extension)
        {
            var deleteAction = (DeleteFilesAction)extension;
            this.txtDeleteFiles.Text = string.Join(Environment.NewLine, deleteAction.FileMasks ?? new string[0]);
            this.chkRecursive.Checked = deleteAction.Recursive;
            this.chkLogVerbose.Checked = deleteAction.LogVerbose;
        }
        public override ActionBase CreateFromForm()
        {
            return new DeleteFilesAction
            {
                FileMasks = this.txtDeleteFiles.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries),
                Recursive = this.chkRecursive.Checked,
                LogVerbose = this.chkLogVerbose.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtDeleteFiles = new TextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Rows = 3,
                Text = "*"
            };

            this.chkRecursive = new CheckBox 
            {
                Text = "Delete from all subdirectories",
                Checked = true
            };

            this.chkLogVerbose = new CheckBox { Text = "Log individual file/directory deletions" };

            this.Controls.Add(
                new SlimFormField("File masks:",
                    new Div(this.txtDeleteFiles),
                    new Div(this.chkRecursive)
                ) { HelpText = "Files and directories matching the specified masks in the \"In Directory\" (entered one per line) will be deleted." },
                new SlimFormField("Additional options:", this.chkLogVerbose)
            );
        }
    }
}
