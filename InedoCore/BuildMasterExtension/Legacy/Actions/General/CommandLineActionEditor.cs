using System;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.PromotionRequirements;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.General
{
    internal sealed class CommandLineActionEditor : ActionEditorBase
    {
        private SourceControlFileFolderPicker txtWorkingDirectory;
        private SourceControlFileFolderPicker txtExePath;
        private ValidatingTextBox txtArguments;
        private CheckBox chkFailOnStandardError;
        private CheckBox chkUseExitCode;
        private DropDownList ddlExitCode;
        private CheckBox chkImportBuildMasterVariables;

        public override string ServerLabel { get { return "Run on server:"; } }

        protected override void CreateChildControls()
        {
            this.txtWorkingDirectory = new SourceControlFileFolderPicker() { DefaultText = "$CurrentDirectory" };
            this.txtExePath = new SourceControlFileFolderPicker { Required = true, };
            this.txtArguments = new ValidatingTextBox();
            this.chkFailOnStandardError = new CheckBox { Text = "Fail if any text is written to standard error" };
            this.chkUseExitCode = new CheckBox { Text = "Succeed only when exit code is..." };
            this.chkImportBuildMasterVariables = new CheckBox { Text = "Import BuildMaster variables to environment" };

            this.ddlExitCode = new DropDownList()
            {
                Items =
                {
                    new ListItem("0", CommandLineSuccessExitCode.Zero.ToString()),
                    new ListItem("Positive", CommandLineSuccessExitCode.Positive.ToString()),
                    new ListItem("Nonnegative", CommandLineSuccessExitCode.NonNegative.ToString()),
                    new ListItem("Nonzero", CommandLineSuccessExitCode.NonZero.ToString()),
                    new ListItem("Negative", CommandLineSuccessExitCode.Negative.ToString())
                }
            };

            var ctlExitCode = new Div(this.ddlExitCode);

            this.Controls.Add(
                new SlimFormField("Executable file:", this.txtExePath),
                new SlimFormField("Process working directory:", this.txtWorkingDirectory),
                new SlimFormField("Arguments:", this.txtArguments),
                new SlimFormField(
                    "Error conditions:",
                    new Div(this.chkFailOnStandardError),
                    new Div(this.chkUseExitCode),
                    ctlExitCode
                ),
                new SlimFormField("Additional options:", this.chkImportBuildMasterVariables)
            );

            this.Controls.BindVisibility(this.chkUseExitCode, ctlExitCode);
        }

        public override void BindToForm(ActionBase action)
        {
            var cmd = (CommandLineAction)action;

            this.txtExePath.Text = cmd.ExePath;
            this.txtArguments.Text = cmd.Arguments;
            this.txtWorkingDirectory.Text = cmd.OverriddenSourceDirectory;
            this.chkFailOnStandardError.Checked = !cmd.DoNotFailOnStandardError;
            this.chkUseExitCode.Checked = cmd.SuccessExitCode != CommandLineSuccessExitCode.Ignore;
            this.chkImportBuildMasterVariables.Checked = cmd.ImportVariables;
            if (cmd.SuccessExitCode != CommandLineSuccessExitCode.Ignore)
                this.ddlExitCode.SelectedValue = cmd.SuccessExitCode.ToString();
        }
        public override ActionBase CreateFromForm()
        {
            return new CommandLineAction
            {
                ExePath = txtExePath.Text,
                Arguments = txtArguments.Text,
                OverriddenSourceDirectory = txtWorkingDirectory.Text,
                DoNotFailOnStandardError = !this.chkFailOnStandardError.Checked,
                ImportVariables = this.chkImportBuildMasterVariables.Checked,
                SuccessExitCode = this.chkUseExitCode.Checked
                                ? (CommandLineSuccessExitCode)Enum.Parse(typeof(CommandLineSuccessExitCode), this.ddlExitCode.SelectedValue)
                                : CommandLineSuccessExitCode.Ignore
            };
        }
    }
}
