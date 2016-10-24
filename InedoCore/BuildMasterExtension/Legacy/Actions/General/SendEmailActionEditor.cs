using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.BuildMaster.Web.Security;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.General
{
    internal sealed class SendEmailActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtTo;
        private ValidatingTextBox txtSubject;
        private TextBox txtBody;
        private ActionServerPicker ddlServer;
        private FileBrowserTextBox txtAttachment;
        private CheckBox chkAttachFile;
        private CheckBox chkIsBodyHtml;

        protected override void CreateChildControls()
        {
            this.txtTo = new ValidatingTextBox
            {
                Required = true,
                TextMode = TextBoxMode.MultiLine,
                Rows = 5
            };

            this.txtSubject = new ValidatingTextBox
            {
                MaxLength = 255,
                Required = true
            };

            this.txtBody = new TextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Rows = 10
            };

            this.txtAttachment = new FileBrowserTextBox
            {
                IncludeFiles = true,
                DefaultText = "no attachment"
            };

            this.ddlServer = new ActionServerPicker
            {
                ID = "bm-action-server-id",
                ClientIDMode = ClientIDMode.Static,
                EnvironmentId = this.EnvironmentId,
                ServerId = this.ServerId
            };

            this.chkAttachFile = new CheckBox { Text = "Attach file..." };

            this.chkIsBodyHtml = new CheckBox { Text = "Send as HTML" };

            var ctlAttachmentContainer = new Div(
                new Div("From server:"),
                this.ddlServer,
                new Div("From file:"),
                this.txtAttachment
            );

            this.Controls.Add(
                new SlimFormField("To address(es):", this.txtTo)
                {
                    HelpText = "Multiple recipients should be separated with a semicolon or newline."
                },
                new SlimFormField("Subject:", this.txtSubject),
                new SlimFormField("Body text:", new Div(this.txtBody), new Div(this.chkIsBodyHtml)),
                new SlimFormField("Attachment:", chkAttachFile, ctlAttachmentContainer)
            );

            this.Controls.BindVisibility(chkAttachFile, ctlAttachmentContainer);
        }

        public override void BindToForm(ActionBase action)
        {
            var act = (SendEmailAction)action;

            this.txtTo.Text = act.To;
            this.txtSubject.Text = act.Subject;
            this.txtBody.Text = act.Message;
            this.txtAttachment.Text = act.Attachment;
            this.ddlServer.ServerId = act.ServerId;
            this.chkAttachFile.Checked = !string.IsNullOrEmpty(act.Attachment);
            this.chkIsBodyHtml.Checked = act.IsBodyHtml;
        }

        public override ActionBase CreateFromForm()
        {
            return new SendEmailAction
            {
                To = JoinAddresses(this.txtTo.Text),
                Subject = this.txtSubject.Text,
                Message = this.txtBody.Text,
                Attachment = this.txtAttachment.Text,
                ServerId = this.ddlServer.ServerId,
                IsBodyHtml = this.chkIsBodyHtml.Checked
            };
        }

        private static string JoinAddresses(string addresses)
        {
            return string.Join(";", addresses.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
