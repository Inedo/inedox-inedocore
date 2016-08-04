using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class RenameFilesActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtSourceMask;
        private ValidatingTextBox txtTargetMask;
        private CheckBox chkLogVerbose;
        private CheckBox chkOverwriteExisting;

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

        public override void BindToForm(ActionBase action)
        {
            this.EnsureChildControls();

            var rename = (RenameFilesAction)action;
            this.txtSourceMask.Text = rename.SourceMask;
            this.txtTargetMask.Text = rename.TargetMask;
            this.chkLogVerbose.Checked = rename.LogVerbose;
            this.chkOverwriteExisting.Checked = rename.OverwriteExisting;
        }

        public override ActionBase CreateFromForm()
        {
            this.EnsureChildControls();

            return new RenameFilesAction()
            {
                SourceMask = this.txtSourceMask.Text,
                TargetMask = this.txtTargetMask.Text,
                LogVerbose = this.chkLogVerbose.Checked,
                OverwriteExisting = this.chkOverwriteExisting.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtSourceMask = new ValidatingTextBox { Required = true };
            this.txtTargetMask = new ValidatingTextBox { Required = true };
            this.chkLogVerbose = new CheckBox() { Text = "Log each file rename"  };
            this.chkOverwriteExisting = new CheckBox() { Text = "Overwrite Existing Files" };

            this.Controls.Add(
                new SlimFormField("Rename files matching:", this.txtSourceMask) { HelpText = "This can be a mask that utilizes wildcards, such as \"*.sql\"" },
                new SlimFormField("To files matching:",
                    new Div(this.txtTargetMask),
                    new Div(this.chkOverwriteExisting)) { HelpText = "When using a masks, the matched wildcards will remain the same; e.g. \"*.asp\" to \"*.asp.old\" will rename each file with the .asp extension to have an .asp.old extension instead." },
                new SlimFormField("Additional options:", this.chkLogVerbose)
            );
        }
    }
}
