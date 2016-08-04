using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Mail;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.General
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.General.SendEmailAction,BuildMasterExtensions")]
    [DisplayName("Send Email")]
    [Description("Sends an email message.")]
    [CustomEditor(typeof(SendEmailActionEditor))]
    [Tag(Tags.Email)]
    [Tag(Tags.General)]
    public sealed class SendEmailAction : ActionBase
    {
        /// <summary>
        /// Gets or sets a semicolon-separated list of email addresses
        /// </summary>
        [Persistent]
        public string To { get; set; }
        /// <summary>
        /// Gets or sets the email's subject line
        /// </summary>
        [Persistent]
        public string Subject { get; set; }
        /// <summary>
        /// Gets or sets the plaintext message body
        /// </summary>
        [Persistent]
        public string Message { get; set; }
        /// <summary>
        /// Gets or sets a file to attach to the message.
        /// </summary>
        [Persistent]
        public string Attachment { get; set; }
        /// <summary>
        /// Gets or sets the server ID where the email attachment is located.
        /// </summary>
        [Persistent]
        public int? ServerId { get; set; }
        [Persistent]
        public bool IsBodyHtml { get; set; }

        /// <summary>
        /// Returns a description of the current configuration of the action.
        /// </summary>
        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription("Send Email to ", new Hilite(this.GetToAddresses().FirstOrDefault() ?? "{unknown}")),
                new RichDescription("with subject ", new Hilite(this.Subject))
            );
        }

        protected override void Execute()
        {
            var addresses = this.GetToAddresses().ToList();

            if (!addresses.Any())
            {
                this.LogWarning("The \"To address(es)\" field is empty, therefore no emails will be sent.");
                return;
            }

            this.LogInformation("Preparing to send email message to {0}...", addresses.Count > 1 ? addresses.Count + " addresses" : addresses.First());

            using (var smtp = new BuildMasterSmtpClient())
            using (var message = BuildMasterSmtpClient.CreateMailMessage(addresses))
            {
                message.IsBodyHtml = this.IsBodyHtml;
                message.Subject = this.Subject;
                message.Body = this.Message;

                if (this.Timeout > 0)
                    smtp.Timeout = this.Timeout;

                if (!string.IsNullOrEmpty(this.Attachment))
                {
                    using (var agent = Util.Agents.CreateAgentFromId(this.ServerId))
                    {
                        var fileOps = agent.GetService<IFileOperationsExecuter>();
                        var sourceDir = fileOps.GetLegacyWorkingDirectory((IGenericBuildMasterContext)this.Context, PathEx.GetDirectoryName(this.Attachment));
                        var attachmentPath = fileOps.CombinePath(sourceDir, PathEx.GetFileName(this.Attachment));
                        this.LogDebug("Adding attachment at file path \"{0}\"...", attachmentPath);

                        if (!fileOps.FileExists(attachmentPath))
                        {
                            this.LogWarning("Could not attach \"{0}\" to the email message because the file was not found on the selected server.", this.Attachment);
                            smtp.Send(message);
                        }
                        else
                        {
                            using (var fileStream = fileOps.OpenFile(attachmentPath, FileMode.Open, FileAccess.Read))
                            {
                                message.Attachments.Add(new Attachment(fileStream, PathEx.GetFileName(attachmentPath)));
                                smtp.Send(message);
                            }
                        }
                    }
                }
                else
                {
                    smtp.Send(message);
                }
            }

            this.LogInformation("Email message{0} sent.", addresses.Count > 1 ? "s" : "");
        }

        private IEnumerable<string> GetToAddresses()
        {
            return this.To.Split(';').Where(address => !string.IsNullOrWhiteSpace(address));
        }
    }
}
