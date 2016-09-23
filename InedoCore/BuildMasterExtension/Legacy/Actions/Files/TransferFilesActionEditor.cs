using System;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    internal sealed class TransferFilesActionEditor : ActionEditorBase, IDualAgentActionEditor
    {
        private ActionServerPicker ddlSourceServer;
        private ActionServerPicker ddlTargetServer;
        private FileBrowserTextBox ctlSourcePath;
        private FileBrowserTextBox ctlTargetPath;
        private CheckBox chkDelete;
        private ValidatingTextBox txtIncludeFileMasks;

        int? IDualAgentActionEditor.SourceServerId
        {
            get
            {
                this.EnsureChildControls();
                return this.ddlSourceServer.ServerId;
            }
            set
            {
                this.EnsureChildControls();
                this.ddlSourceServer.ServerId = value;
            }
        }
        string IDualAgentActionEditor.SourceServerVariableName
        {
            get
            {
                this.EnsureChildControls();
                return this.ddlSourceServer.ServerVariableName;
            }
            set
            {
                this.EnsureChildControls();
                if (!string.IsNullOrEmpty(value))
                    this.ddlSourceServer.ServerVariableName = value;
            }
        }
        int? IDualAgentActionEditor.TargetServerId
        {
            get
            {
                this.EnsureChildControls();
                return this.ddlTargetServer.ServerId;
            }
            set
            {
                this.EnsureChildControls();
                this.ddlTargetServer.ServerId = value;
            }
        }
        string IDualAgentActionEditor.TargetServerVariableName
        {
            get
            {
                this.EnsureChildControls();
                return this.ddlTargetServer.ServerVariableName;
            }
            set
            {
                this.EnsureChildControls();
                if (!string.IsNullOrEmpty(value))
                    this.ddlTargetServer.ServerVariableName = value;
            }
        }

        public override void BindToForm(ActionBase extension)
        {
            var action = (TransferFilesAction)extension;

            this.ctlSourcePath.Text = action.SourceDirectory;
            this.ctlTargetPath.Text = action.TargetDirectory;
            this.chkDelete.Checked = action.DeleteTarget;
            this.txtIncludeFileMasks.Text = string.Join(Environment.NewLine, action.IncludeFileMasks);
        }

        public override ActionBase CreateFromForm()
        {
            return new TransferFilesAction
            {
                SourceDirectory = this.ctlSourcePath.Text,
                TargetDirectory = this.ctlTargetPath.Text,
                DeleteTarget = this.chkDelete.Checked,
                IncludeFileMasks = this.txtIncludeFileMasks.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            };
        }

        protected override void CreateChildControls()
        {
            this.ddlSourceServer = new ActionServerPicker
            {
                ID = "ddlSourceServer",
                ShowGroups = false,
                EnvironmentId = this.EnvironmentId
            };

            this.ddlTargetServer = new ActionServerPicker
            {
                ID = "ddlTargetServer",
                EnvironmentId = this.EnvironmentId
            };

            this.ctlSourcePath = new FileBrowserTextBox
            {
                IncludeFiles = false,
                BindToControlId = this.ddlSourceServer.ID,
                DefaultText = "$CurrentDirectory"
            };

            this.ctlTargetPath = new FileBrowserTextBox
            {
                IncludeFiles = false,
                BindToControlId = this.ddlTargetServer.ID,
                DefaultText = "$CurrentDirectory"
            };

            this.chkDelete = new CheckBox
            {
                ID = "chkDelete",
                Checked = true,
                Text = "Delete files/directories not present in source"
            };

            this.txtIncludeFileMasks = new ValidatingTextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Rows = 4,
                Text = "*"
            };

            this.Controls.Add(
                new SlimFormField("From server:", this.ddlSourceServer),
                new SlimFormField("From directory:", this.ctlSourcePath),
                new SlimFormField("To server:", this.ddlTargetServer),
                new SlimFormField("To directory:", this.ctlTargetPath),
                new SlimFormField("File/directory mask:", this.txtIncludeFileMasks)
                {
                    HelpText = "Files and folders matching the specified masks (entered one per line) will be transferred. " 
                    + "For example, if you want to transfer all files except *.src files, enter the following lines "
                    + "(without quotes): \"*\" and \"!*.src\""
                },
                new SlimFormField("Additional options:", this.chkDelete)
            );
        }
    }
}
