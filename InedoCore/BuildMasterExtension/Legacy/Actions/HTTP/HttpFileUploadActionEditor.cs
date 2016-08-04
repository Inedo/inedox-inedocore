using System.Web.UI.WebControls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.IO;
using Inedo.Web.Controls;

namespace Inedo.BuildMaster.Extensibility.Actions.HTTP
{
    internal sealed class HttpFileUploadActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtFileName;
        private ValidatingTextBox txtUrl;
        private CheckBox chkLogResponseBody;

        public override string ServerLabel
        {
            get { return "From server:"; }
        }

        protected override void CreateChildControls()
        {
            this.txtFileName = new ValidatingTextBox { Required = true };

            this.txtUrl = new ValidatingTextBox { Required = true };

            this.chkLogResponseBody = new CheckBox { Text = "Log Content-Body of response" };

            this.Controls.Add(
                new SlimFormField("To URL:", this.txtUrl),
                new SlimFormField("File name:", this.txtFileName),
                new SlimFormField("Additional options:", this.chkLogResponseBody)
            );
        }

        public override void BindToForm(ActionBase extension)
        {
            var httpFileUploadAction = (HttpFileUploadAction)extension;

            this.txtFileName.Text = PathEx.Combine(httpFileUploadAction.OverriddenSourceDirectory, PathEx.GetFileName(httpFileUploadAction.FileName));
            this.txtUrl.Text = httpFileUploadAction.Url;
            this.chkLogResponseBody.Checked = httpFileUploadAction.LogResponseBody;
        }

        public override ActionBase CreateFromForm()
        {
            return new HttpFileUploadAction()
            {
                FileName = PathEx.GetFileName(this.txtFileName.Text.Trim()),
                OverriddenSourceDirectory = PathEx.GetDirectoryName(this.txtFileName.Text.Trim()),
                Url = this.txtUrl.Text.Trim(),
                LogResponseBody = this.chkLogResponseBody.Checked
            };
        }
    }
}
