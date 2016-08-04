using System;
using System.Collections.Generic;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class SetFileAttributesActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtFiles;
        private CheckBox chkRecursive;
        private DropDownList ddlReadOnly;
        private DropDownList ddlSystem;
        private DropDownList ddlHidden;

        public SetFileAttributesActionEditor()
        {
            this.ValidateBeforeSave += SetFileAttributesActionEditor_ValidateBeforeSave;
        }

        public override bool DisplaySourceDirectory { get { return true; } }
        public override string SourceDirectoryLabel { get { return "In directory:"; } }
        public override string ServerLabel { get { return "On server:"; } }

        public override void BindToForm(ActionBase action)
        {
            var changeAction = (SetFileAttributesAction)action;

            this.txtFiles.Text = String.Join(Environment.NewLine, changeAction.FileMasks);
            this.chkRecursive.Checked = changeAction.Recursive;
            if (changeAction.ReadOnly.HasValue) this.ddlReadOnly.SelectedValue = changeAction.ReadOnly.ToString();
            if (changeAction.Hidden.HasValue) this.ddlHidden.SelectedValue = changeAction.Hidden.ToString();
            if (changeAction.System.HasValue) this.ddlSystem.SelectedValue = changeAction.System.ToString();
        }

        public override ActionBase CreateFromForm()
        {
            return new SetFileAttributesAction()
            {
                FileMasks = txtFiles.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries),
                Recursive = chkRecursive.Checked,
                ReadOnly = Util.Bool.ParseN(ddlReadOnly.SelectedValue),
                Hidden = Util.Bool.ParseN(ddlHidden.SelectedValue),
                System = Util.Bool.ParseN(ddlSystem.SelectedValue)
            };
        }

        protected override void CreateChildControls()
        {
            this.txtFiles = new ValidatingTextBox { TextMode = TextBoxMode.MultiLine, Rows = 3, Text = "*", Required = true };
            this.chkRecursive = new CheckBox { Text = "Set attributes on files in all subdirectories", Checked = true };

            this.ddlReadOnly = new DropDownList();
            this.ddlReadOnly.Items.AddRange(GetAttribListItems("Read Only"));

            this.ddlSystem = new DropDownList();
            this.ddlSystem.Items.AddRange(GetAttribListItems("System"));

            this.ddlHidden = new DropDownList();
            this.ddlHidden.Items.AddRange(GetAttribListItems("Hidden"));

            this.Controls.Add(
                new SlimFormField("File masks:",
                    new Div(new Div(this.txtFiles), new Div(this.chkRecursive))),
                new SlimFormField("Change attributes:",
                    new Div(new Div("Read Only:"), new Div(this.ddlReadOnly)),
                    new Div(new Div("Hidden:"), new Div(this.ddlHidden)),
                    new Div(new Div("System:"), new Div(this.ddlSystem)))
            );
        }

        private void SetFileAttributesActionEditor_ValidateBeforeSave(object sender, ValidationEventArgs<ActionBase> e)
        {
            var action = (SetFileAttributesAction)e.Extension;
            if (!action.ReadOnly.HasValue && !action.Hidden.HasValue && !action.System.HasValue)
            {
                e.Message = "At least one attribute must be set or cleared.";
                e.ValidLevel = ValidationLevel.Error;
                return;
            }
        }

        private static IEnumerable<ListItem> GetAttribListItems(string text)
        {
            return new[]
            {
                new ListItem("Do not change", ""),
                new ListItem("Clear " + text, bool.FalseString),
                new ListItem("Set " + text, bool.TrueString)
            };
        }
    }
}
