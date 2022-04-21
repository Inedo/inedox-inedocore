using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using Inedo.Web;

namespace Inedo.Extensions.Operations.General
{
    [DisplayName("Send Email")]
    [Description("Sends an email message.")]
    [ScriptAlias("Send-Email")]
    [Tag("email")]
    [Example(@"
Send-Email (
    To: @(someone@example.org, someone-else@example.org),
    Subject: Howdy!,
    Text: >>Hello there!

This email was sent from BuildMaster on $Date.>>
);
")]
    [Note("If the Html property is specified, then the Text property will be ignored.")]
    public sealed class SendEmailOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("To")]
        [DisplayName("To address(es)")]
        [Description("A single email address may be used, or a list variable containing multiple email addresses.")]
        public IEnumerable<string> To { get; set; }
        [ScriptAlias("Subject")]
        public string Subject { get; set; }
        [ScriptAlias("Text")]
        [DisplayName("Plain-text body")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string BodyText { get; set; }
        [ScriptAlias("Html")]
        [DisplayName("HTML body")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string BodyHtml { get; set; }
        [Category("Attachments")]
        [ScriptAlias("Attachments")]
        public IEnumerable<string> Attachments { get; set; }
        [Category("Attachments")]
        [ScriptAlias("AttachmentDirectory")]
        [DisplayName("From directory")]
        [PlaceholderText("$WorkingDirectory")]
        public string SourceDirectory { get; set; }
        [ScriptAlias("CC")]
        [DisplayName("CC address(es)")]
        [Description("A single email address may be used, or a list variable containing multiple email addresses.")]
        [Category("Advanced")]
        public IEnumerable<string> CC { get; set; }
        [ScriptAlias("Bcc")]
        [DisplayName("BCC address(es)")]
        [Description("A single email address may be used, or a list variable containing multiple email addresses.")]
        [Category("Advanced")]
        public IEnumerable<string> Bcc { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var addresses = this.To?.ToList() ?? new List<string>();
            if (addresses.Count == 0)
            {
                this.LogWarning("No \"To\" addresses were specified.");
                return;
            }

            var ccAddresses = this.CC?.ToList() ?? new List<string>();
            var bccAddresses = this.Bcc?.ToList() ?? new List<string>();

            var attachments = new List<Attachment>();
            try
            {
                var attachmentNames = this.Attachments?.ToList() ?? new List<string>();
                if (attachmentNames.Count > 0)
                {
                    var path = context.ResolvePath(this.SourceDirectory);
                    this.LogDebug($"Looking for file attachments in {path}...");
                    var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

                    foreach (var attachmentName in attachmentNames)
                    {
                        var fullPath = fileOps.CombinePath(path, attachmentName);
                        try
                        {
                            this.LogDebug($"Attaching {fullPath}...");
                            attachments.Add(new Attachment(fileOps.OpenFile(fullPath, FileMode.Open, FileAccess.Read), PathEx.GetFileName(attachmentName)));
                        }
                        catch (Exception ex)
                        {
                            this.LogError("Unable to attach " + attachmentName + ": " + ex.Message);
                            return;
                        }
                    }
                }
                else
                {
                    this.LogDebug("No file attachments specified.");
                }

                this.LogInformation($"Preparing to send email to {string.Join("; ", addresses)}...");

                using var smtp = this.CreateSmtpClient();
                if (smtp == null)
                {
                    this.LogError("SMTP client configuration is invalid.");
                    return;
                }

                using var message = this.CreateMailMessage();
                if (message == null)
                {
                    this.LogError("SMTP client FromAddress configuration is invalid.");
                    return;
                }

                foreach (var address in addresses)
                    message.To.Add(address);
                foreach (var address in ccAddresses)
                    message.CC.Add(address);
                foreach (var address in bccAddresses)
                    message.Bcc.Add(address);

                message.IsBodyHtml = !string.IsNullOrWhiteSpace(this.BodyHtml);
                message.Body = AH.CoalesceString(this.BodyHtml, this.BodyText) ?? string.Empty;
                message.Subject = this.Subject ?? string.Empty;

                foreach (var attachment in attachments)
                    message.Attachments.Add(attachment);

                context.CancellationToken.ThrowIfCancellationRequested();
                context.CancellationToken.Register(
                    () =>
                    {
                        try
                        {
                            smtp.SendAsyncCancel();
                        }
                        catch
                        {
                        }
                    }
                );

                this.LogInformation("Sending email...");
                await smtp.SendMailAsync(message).ConfigureAwait(false);
                this.LogInformation("Email sent.");
            }
            finally
            {
                foreach (var attachment in attachments)
                {
                    try
                    {
                        attachment.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private SmtpClient CreateSmtpClient()
        {
            string host = SDK.GetConfigValue("Smtp.Host");
            int port = AH.ParseInt(SDK.GetConfigValue("Smtp.Port")) ?? 25;
            bool ssl = bool.TryParse(AH.CoalesceString(SDK.GetConfigValue("Smtp.SslEnabled"), SDK.GetConfigValue("Smtp.EnableSSL")), out bool s) && s;
            string username = SDK.GetConfigValue("Smtp.UserName");
            string password = SDK.GetConfigValue("Smtp.Password");

            if (string.IsNullOrEmpty(host))
            {
                this.LogError("SMTP host not specified. Please set the \"Smtp.Host\" value in Advanced Settings.");
                return null;
            }

            SmtpClient client = null;
            try
            {
                client = new SmtpClient(host, port)
                {
                    EnableSsl = ssl,
                    UseDefaultCredentials = false, // login to mail server anonymously if no username/password specified
                    Timeout = 15 * 1000
                };

                if (!string.IsNullOrEmpty(username))
                    client.Credentials = new NetworkCredential(username, password);

                return client;
            }
            catch
            {
                client?.Dispose();
                throw;
            }
        }

        private MailMessage CreateMailMessage()
        {
            string fromAddress = SDK.GetConfigValue("Smtp.FromAddress");
            string fromName = SDK.GetConfigValue("Smtp.FromName");

            if (string.IsNullOrEmpty(fromAddress))
            {
                this.LogError("From Address not specified. Please set the \"Smtp.FromAddress\" value in Advanced Settings.");
                return null;
            }

            if (string.IsNullOrEmpty(fromName))
            {
                return new MailMessage
                {
                    From = new MailAddress(fromAddress)
                };
            }
            else
            {
                return new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName)
                };
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var longDesc = new RichDescription();
            var attachments = config[nameof(Attachments)].AsEnumerable()?.ToList();
            if (attachments != null && attachments.Count > 0)
            {
                longDesc.AppendContent(
                    "with attachments ",
                    new ListHilite(attachments),
                    " "
                );

                if (!string.IsNullOrEmpty(config[nameof(SourceDirectory)]))
                {
                    longDesc.AppendContent(
                        "in ",
                        new DirectoryHilite(config[nameof(SourceDirectory)])
                    );
                }
            }

            var text = AH.CoalesceString((string)config[nameof(BodyHtml)], (string)config[nameof(BodyText)]);
            if (!string.IsNullOrWhiteSpace(text))
            {
                longDesc.AppendContent(
                    "starting with ",
                    new Hilite(text)
                );
            }

            return new ExtendedRichDescription(
                new RichDescription(
                    "Send email to ",
                    new ListHilite(config[nameof(To)]),
                    " with subject ",
                    new Hilite(AH.CoalesceString(config[nameof(Subject)], "(no subject)"))
                ),
                longDesc
            );
        }
    }
}
