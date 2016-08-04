using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMaster.Extensibility.Actions.HTTP
{
    internal sealed class HttpGetActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtUrl;
        private CheckBox chkLogResponseBody;
        private CheckBox chkSaveResponseBodyInVariable;
        private ValidatingTextBox txtErrorStatusCodes;
        private DropDownList ddlHttpMethod;

        public override string ServerLabel
        {
            get { return "Request from server:"; }
        }

        public override void BindToForm(ActionBase extension)
        {
            var action = (HttpGetAction)extension;

            this.ddlHttpMethod.SelectedValue = action.HttpMethod;
            this.txtUrl.Text = action.Url ?? string.Empty;
            this.chkLogResponseBody.Checked = action.LogResponseBody;
            this.chkSaveResponseBodyInVariable.Checked = action.SaveResponseBodyAsVariable;
            this.txtErrorStatusCodes.Text = action.ErrorStatusCodes;
        }
        public override ActionBase CreateFromForm()
        {
            return new HttpGetAction
            {
                HttpMethod = this.ddlHttpMethod.SelectedValue,
                Url = this.txtUrl.Text,
                LogResponseBody = this.chkLogResponseBody.Checked,
                SaveResponseBodyAsVariable = this.chkSaveResponseBodyInVariable.Checked,
                ErrorStatusCodes = StatusCodeRangeList.Parse(this.txtErrorStatusCodes.Text).ToString(),
            };
        }

        protected override void CreateChildControls()
        {
            this.ddlHttpMethod = new DropDownList
            {
                Items = 
                {
                    new ListItem("GET"), 
                    new ListItem("DELETE"),
                    new ListItem("HEAD")
                }
            };

            this.txtUrl = new ValidatingTextBox
            {
                Required = true
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

            this.chkSaveResponseBodyInVariable = new CheckBox
            {
                Text = "Save response body as $" + HttpActionBase.HttpResponseBodyVariableName + " execution variable"
            };

            this.Controls.Add(
                new SlimFormField("HTTP method:", this.ddlHttpMethod),
                new SlimFormField("From URL:", this.txtUrl),
                new SlimFormField("Error status code ranges:", this.txtErrorStatusCodes)
                {
                    HelpText = "Enter comma-separated status codes (or ranges in the form of start:end) that should indicate this action has failed. "
                    + "For example, a value of \"401,500:599\" will fail on all server errors and also when \"HTTP Unauthorized\" is returned."
                },
                new SlimFormField("Additional options:", new Div(this.chkLogResponseBody), new Div(this.chkSaveResponseBodyInVariable))
            );
        }
    }
}
