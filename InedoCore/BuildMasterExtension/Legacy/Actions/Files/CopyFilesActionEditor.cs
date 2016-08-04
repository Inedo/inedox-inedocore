using System;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class CopyFilesActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtIncludeFileMasks;
        private CheckBox chkOverwrite;
        private CheckBox chkRecursive;
        private CheckBox chkVerbose;

        public override bool DisplaySourceDirectory => true;
        public override bool DisplayTargetDirectory => true;
        public override string SourceDirectoryLabel => "From:";
        public override string TargetDirectoryLabel => "To:";

        public override void BindToForm(ActionBase extension)
        {
            var action = (CopyFilesAction)extension;

            this.txtIncludeFileMasks.Text = string.Join(Environment.NewLine, action.IncludeFileMasks ?? new string[0]);
            this.chkOverwrite.Checked = action.Overwrite;
            this.chkRecursive.Checked = action.Recursive;
            this.chkVerbose.Checked = action.VerboseLogging;
        }
        public override ActionBase CreateFromForm()
        {
            return new CopyFilesAction
            {
                IncludeFileMasks = txtIncludeFileMasks.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries),
                Overwrite = this.chkOverwrite.Checked,
                Recursive = this.chkRecursive.Checked,
                VerboseLogging = this.chkVerbose.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtIncludeFileMasks = new ValidatingTextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Rows = 4,
                Text = "*"
            };

            this.chkOverwrite = new CheckBox
            {
                Text = "Overwrite files",
                Checked = true
            };

            this.chkRecursive = new CheckBox
            {
                Text = "Recursively copy directories"
            };

            this.chkVerbose = new CheckBox
            {
                Text = "Verbose logging"
            };

            this.Controls.Add(
                new SlimFormField("File mask:", this.txtIncludeFileMasks)
                {
                    HelpText = "Files and folders matching the specified masks (entered one per line) will be transferred. "
                             + "For example, if you want to transfer all files except *.src files, enter the following lines "
                             + "(without quotes): \"*\" and \"!*.src\""
                },
                new SlimFormField(
                    "Options:",
                    new Div(this.chkOverwrite),
                    new Div(this.chkRecursive),
                    new Div(this.chkVerbose)
                )
            );
        }
    }
}
