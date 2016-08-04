using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMaster.Extensibility.Actions.General
{
    internal sealed class SleepActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtSecondsToSleep;

        protected override void CreateChildControls()
        {
            this.txtSecondsToSleep = new ValidatingTextBox { Required = true };

            this.Controls.Add(new SlimFormField("Seconds to sleep:", txtSecondsToSleep));
        }

        public override ActionBase CreateFromForm()
        {
            return new SleepAction
            {
                SecondsToSleep = txtSecondsToSleep.Text
            };
        }

        public override void BindToForm(ActionBase action)
        {
            txtSecondsToSleep.Text = ((SleepAction)action).SecondsToSleep;
        }
    }
}
