using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.ClientResources;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.HTTP
{
    internal sealed class HttpPostActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtUrl;
        private ValidatingTextBox txtContentType;
        private TextBox txtBody;
        private CheckBox chkLogResponseBody;
        private ValidatingTextBox txtErrorStatusCodes;
        private CheckBox chkRawInput;
        private CheckBox chkLogContent;
        private CheckBox chkSaveResponseBodyInVariable;
        private HiddenField ctlFormData;
        private DropDownList ddlHttpMethod;

        public override string ServerLabel
        {
            get { return "From server:"; }
        }

        public override void BindToForm(ActionBase extension)
        {
            var action = (HttpPostAction)extension;

            this.ddlHttpMethod.SelectedValue = action.HttpMethod;
            this.txtUrl.Text = action.Url ?? string.Empty;
            this.txtBody.Text = action.PostData ?? string.Empty;
            this.chkLogResponseBody.Checked = action.LogResponseBody;
            this.chkSaveResponseBodyInVariable.Checked = action.SaveResponseBodyAsVariable;
            this.txtErrorStatusCodes.Text = action.ErrorStatusCodes;
            this.chkLogContent.Checked = action.LogRequestData;

            if (!string.IsNullOrEmpty(action.ContentType) || !string.IsNullOrEmpty(action.PostData))
            {
                this.txtContentType.Text = action.ContentType;
                this.chkRawInput.Checked = true;
            }
            else
            {
                this.chkRawInput.Checked = false;
                if (action.FormData != null)
                {
                    this.ctlFormData.Value = string.Join(
                        "&",
                        from f in action.FormData
                        select Uri.EscapeDataString(f.Key) + "=" + Uri.EscapeDataString(f.Value)
                    );
                }
            }
        }
        public override ActionBase CreateFromForm()
        {
            var action = new HttpPostAction
            {
                HttpMethod = this.ddlHttpMethod.SelectedValue,
                Url = this.txtUrl.Text,
                LogResponseBody = this.chkLogResponseBody.Checked,
                SaveResponseBodyAsVariable = this.chkSaveResponseBodyInVariable.Checked,
                ErrorStatusCodes = StatusCodeRangeList.Parse(this.txtErrorStatusCodes.Text).ToString(),
                LogRequestData = this.chkLogContent.Checked
            };

            if (this.chkRawInput.Checked)
            {
                action.PostData = this.txtBody.Text;
                action.ContentType = AH.NullIf(this.txtContentType.Text, string.Empty);
            }
            else
            {
                action.FormData = (from f in this.ctlFormData.Value.Split('&')
                                   where !string.IsNullOrEmpty(f)
                                   let p = f.Split(new[] { '=' }, 2)
                                   where p.Length == 2
                                   let k = Uri.UnescapeDataString(p[0]).Trim()
                                   let v = Uri.UnescapeDataString(p[1]).Trim()
                                   where !string.IsNullOrEmpty(k)
                                   select new KeyValuePair<string, string>(k, v)).ToList();
            }

            return action;
        }

        protected override void CreateChildControls()
        {
            this.ddlHttpMethod = new DropDownList 
            {
                Items = 
                {
                    new ListItem("POST"), 
                    new ListItem("PUT"),
                    new ListItem("PATCH")
                }
            };

            this.txtUrl = new ValidatingTextBox
            {
                Required = true
            };

            this.txtContentType = new ValidatingTextBox
            {
                ID = "txtContentType",
                CssClass = "raw-input-mode",
                DefaultText = "application/x-www-form-urlencoded"
            };

            this.txtBody = new TextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Rows = 5,
                CssClass = "raw-input-mode"
            };

            this.txtErrorStatusCodes = new ValidatingTextBox 
            { 
                Text = "400:599",
                DefaultText = "Succeed regardless of response code" 
            };

            this.chkLogResponseBody = new CheckBox
            {
                Text = "Log Content-Body of response"
            };

            this.chkRawInput = new CheckBox
            {
                ID = "chkRawInputMode",
                Text = "Raw input mode"
            };

            this.chkSaveResponseBodyInVariable = new CheckBox 
            { 
                Text = "Save response body as $" + HttpActionBase.HttpResponseBodyVariableName  + " execution variable" 
            };

            this.ctlFormData = new HiddenField
            {
                ID = "ctlFormData"
            };

            this.chkLogContent = new CheckBox
            {
                Text = "Log actual request content"
            };

            string dpTemplate;
            using (var stream = typeof(HttpPostActionEditor).Assembly.GetManifestResourceStream(typeof(HttpPostActionEditor).Namespace + ".FormDataInput.html"))
            using (var reader = new StreamReader(stream))
            {
                dpTemplate = reader.ReadToEnd();
            }

            var dpContainer = new Div
            {
                ID = "ctlFormInputMode",
                Class = "form-input-mode",
                InnerHtml = dpTemplate
            };

            this.Controls.Add(
                new SlimFormField("HTTP method:", this.ddlHttpMethod),
                new SlimFormField("To URL:", this.txtUrl),
                new SlimFormField(
                    "Content type:",
                    this.txtContentType,
                    new P("application/x-www-form-urlencoded") { Class = "form-input-mode" }
                ),
                new SlimFormField(
                    "Data:",
                    new Div(chkRawInput),
                    this.txtBody,
                    dpContainer,
                    ctlFormData
                )
                {
                    HelpText = "If raw input mode is used, BuildMaster will post exactly the data you supply here with no additional encoding using the Content-Type you supply. " +
                        "Otherwise, each key/value pair will be URI-encoded and posted as application/x-www-form-urlencoded data."
                },
                new SlimFormField("Error status code ranges:", this.txtErrorStatusCodes)
                {
                    HelpText = "Enter comma-separated status codes (or ranges in the form of start:end) that should indicate this action has failed. "
                    + "For example, a value of \"401,500:599\" will fail on all server errors and also when \"HTTP Unauthorized\" is returned."
                },
                new SlimFormField(
                    "Additional options:",
                    new Div(this.chkLogResponseBody),
                    new Div(this.chkSaveResponseBodyInVariable),
                    new Div(this.chkLogContent)
                ),
                new RenderJQueryDocReadyDelegator(
                    w =>
                    {
                        w.Write("var handleChange = function() {");
                        w.Write("  if($('#{0}').is(':checked')) {{", chkRawInput.ClientID);
                        w.Write("    $('.form-input-mode').hide();");
                        w.Write("    $('.raw-input-mode').show();");
                        w.Write("  } else {");
                        w.Write("    $('.form-input-mode').show();");
                        w.Write("    $('.raw-input-mode').hide();");
                        w.Write("  }");
                        w.Write("};");
                        w.Write("handleChange();");
                        w.Write("$('#{0}').change(function() {{ handleChange(); }});", chkRawInput.ClientID);

                        w.Write("FormDataInputControl.Init(");
                        InedoLib.Util.JavaScript.WriteJson(
                            w,
                            new
                            {
                                ctlFieldId = ctlFormData.ClientID,
                                ctlContainerId = dpContainer.ClientID
                            }
                        );
                        w.Write(");");
                    }
                )
            );
        }

        protected override void OnPreRender(EventArgs e)
        {
            this.IncludeClientResourceInPage(
                new JavascriptResource("/Resources/jQuery/FormDataInput.js?" + typeof(HttpActionBase).Assembly.GetName().Version)//, InedoLibWebCR.knockout)
            );

            base.OnPreRender(e);
        }
    }
}
