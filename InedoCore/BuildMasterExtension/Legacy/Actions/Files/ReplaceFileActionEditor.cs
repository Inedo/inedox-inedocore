using System;
using System.IO;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    public sealed class ReplaceFileActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtFileName, txtSearchText, txtReplaceText;
        private CheckBox chkUseRegex, chkRecursive;

        public override string ServerLabel { get { return "On server:"; } }
        public override string SourceDirectoryLabel { get { return "In directory:"; } }
        public override bool DisplaySourceDirectory { get { return true; } }

        protected override void CreateChildControls()
        {
            this.txtFileName = new ValidatingTextBox() { TextMode = TextBoxMode.MultiLine, Rows = 3, Required = true };
            this.txtSearchText = new ValidatingTextBox { TextMode = TextBoxMode.MultiLine, Rows = 3, Required = true  };
            this.txtReplaceText = new ValidatingTextBox { TextMode = TextBoxMode.MultiLine, Rows = 3 };
            this.chkUseRegex = new CheckBox { Text = "Use Regular Expression for Search and Replace" };
            this.chkRecursive = new CheckBox { Text = "Also search in subdirectories" };

            this.Controls.Add(
                new SlimFormField("Matching files:",
                    new Div(new Div(this.txtFileName), new Div(this.chkRecursive)))
                { HelpText =  "This can be a mask that utilizes wildcards, such as \"*.sql\" or \"\\subpath\\myclass.php\" " },
                new SlimFormField("Search text:", 
                    new Div(new Div(this.txtSearchText),new Div(this.chkUseRegex))),
                new SlimFormField("Replace text:", this.txtReplaceText)
            );

        }

        public override void BindToForm(ActionBase action)
        {
            this.EnsureChildControls();

            var file = (ReplaceFileAction)action;

            this.txtFileName.Text = string.Join(Environment.NewLine, file.FileNameMasks);
            this.txtSearchText.Text = file.SearchText;
            this.txtReplaceText.Text = file.ReplaceText;
            this.chkUseRegex.Checked = file.UseRegex;
            this.chkRecursive.Checked = file.Recursive;
        }

        public override ActionBase CreateFromForm()
        {
            this.EnsureChildControls();

            return new ReplaceFileAction()
            {
                FileNameMasks = txtFileName.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries),
                SearchText = txtSearchText.Text,
                ReplaceText = txtReplaceText.Text,
                UseRegex = chkUseRegex.Checked,
                Recursive = chkRecursive.Checked
            };
        }
    }
}
