using System;
using System.Linq;
using System.Text;
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

    internal sealed class EncodingPicker : SelectList
    {
        public EncodingPicker()
        {
            this.Items.AddRange(
                new[]
                {
                    new SelectListItem("Auto Detect", string.Empty, false, "Standard"),
                    new SelectListItem("UTF-8", Encoding.UTF8.WebName, false, "Standard"),
                    new SelectListItem("UTF-16", Encoding.Unicode.WebName, false, "Standard"),
                    new SelectListItem("ASCII", Encoding.ASCII.WebName, false, "Standard"),
                    new SelectListItem("ANSI", "ansi", false, "Standard")
                }
            );

            this.Items.AddRange(
                Encoding.GetEncodings()
                    .Where(e => e.Name != Encoding.UTF8.WebName && e.Name != Encoding.Unicode.WebName && e.Name != Encoding.ASCII.WebName && e.Name != "ansi")
                    .Select(e => new SelectListItem(e.DisplayName, e.Name, false, "Extended"))
            );
        }
        public Encoding SelectedEncoding
        {
            get
            {
                var value = this.SelectedValue;
                if (string.IsNullOrEmpty(value))
                    return null;

                if (value == "ansi")
                    return Encoding.Default;

                return Encoding.GetEncoding(value);
            }
            set
            {
                if (value == null)
                    this.SelectedValue = string.Empty;
                else if (value == Encoding.Default)
                    this.SelectedValue = "ansi";
                else
                    this.SelectedValue = value.WebName;
            }
        }
    }
}
