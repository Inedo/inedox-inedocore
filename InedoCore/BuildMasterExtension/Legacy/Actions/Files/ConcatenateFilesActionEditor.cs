using System;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.BuildMaster.Web.WebApplication.Controls;
using Inedo.IO;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class ConcatenateFilesActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtMasks, txtContentSeparationText;
        private CheckBox chkRecursive, chkForceLinuxNewlines;
        private EncodingPicker ddlEncoding;
        private FileBrowserTextBox txtOutputFile;

        public override bool DisplaySourceDirectory => true;
        public override string ServerLabel => "On server:";
        public override string SourceDirectoryLabel => "From directory:";

        protected override void CreateChildControls()
        {
            this.txtMasks = new ValidatingTextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Rows = 2,
                Text = "*"
            };

            this.txtOutputFile = new FileBrowserTextBox
            {
                IncludeFiles = true,
                Required = true
            };

            this.txtContentSeparationText = new ValidatingTextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Rows = 5,
                DefaultText = "no separator"
            };

            this.chkRecursive = new CheckBox
            {
                Checked = true,
                Text = "Recurse subdirectories"
            };

            this.chkForceLinuxNewlines = new CheckBox
            {
                Checked = false,
                Text = "Use \\n for newlines in separator"
            };

            this.ddlEncoding = new EncodingPicker();

            this.Controls.Add(
                new SlimFormField("Files to concatenate:",
                    new Div(this.txtMasks),
                    new Div(this.chkRecursive)
                    ) { HelpText = "Files matching the specified masks in the \"From directory\" (entered one per line) will be concatenated." },
                new SlimFormField("Content separator:",
                    new Div(this.txtContentSeparationText),
                    new Div(this.chkForceLinuxNewlines)),
                new SlimFormField("To file:", this.txtOutputFile) { HelpText = "This is a path relative to the \"To directory\", e.g. \"combined.css\" or \"~\\db\\objects.sql\"" },
                new SlimFormField("File encoding:", this.ddlEncoding)
            );
        }

        public override void BindToForm(ActionBase extension)
        {
            var action = (ConcatenateFilesAction)extension;
            this.txtMasks.Text = string.Join(Environment.NewLine, action.FileMasks);
            this.txtOutputFile.Text = PathEx.Combine(action.OverriddenTargetDirectory ?? string.Empty, action.OutputFile ?? string.Empty);
            this.chkRecursive.Checked = action.Recursive;
            this.ddlEncoding.SelectedValue = action.OutputFileEncoding;
            this.txtContentSeparationText.Text = action.ContentSeparationText;
            this.chkForceLinuxNewlines.Checked = action.ForceLinuxNewlines;
        }

        public override ActionBase CreateFromForm()
        {
            return new ConcatenateFilesAction
            {
                FileMasks = this.txtMasks.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries),
                OverriddenTargetDirectory = PathEx.GetDirectoryName(this.txtOutputFile.Text),
                OutputFile = PathEx.GetFileName(this.txtOutputFile.Text),
                Recursive = this.chkRecursive.Checked,
                OutputFileEncoding = this.ddlEncoding.SelectedValue,
                ContentSeparationText = this.txtContentSeparationText.Text,
                ForceLinuxNewlines = this.chkForceLinuxNewlines.Checked
            };
        }
    }
}
