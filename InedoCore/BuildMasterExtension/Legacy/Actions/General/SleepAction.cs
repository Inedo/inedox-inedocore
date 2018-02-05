using System.ComponentModel;
using Inedo.Documentation;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.General
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.General.SleepAction,BuildMasterExtensions")]
    [ConvertibleToOperation(typeof(Inedo.Extensions.Legacy.ActionImporters.General.Sleep))]
    [DisplayName("Sleep")]
    [Description("Halts the execution for a specified number of seconds.")]
    [Inedo.Web.CustomEditor(typeof(SleepActionEditor))]
    [Tag(Tags.General)]
    public sealed class SleepAction : ActionBase
    {
        [Persistent]
        public string SecondsToSleep { get; set; }

        [ScriptAlias("Seconds")]
        public int Seconds
        {
            get { return AH.ParseInt(this.SecondsToSleep) ?? 0; }
            set { this.SecondsToSleep = value.ToString(); }
        }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription("Sleep for ", new Hilite(this.SecondsToSleep + " Second" + (AH.ParseInt(this.SecondsToSleep) == 1 ? "" : "s")))
            );
        }

        protected override void Execute()
        {
            int? seconds = AH.ParseInt(this.SecondsToSleep);
            if (seconds == null)
            {
                this.LogError("The specified number of seconds to sleep \"{0}\", does not evaluate to a valid number of seconds.", this.SecondsToSleep);
                return;
            }

            this.LogInformation("Execution halted for {0} second{1}...", this.SecondsToSleep, seconds == 1 ? "" : "s");

            for (int i = (int)seconds; i > 0; i--)
            {
                this.ThrowIfCanceledOrTimeoutExpired();

                if (i <= 15)
                {
                    this.LogDebug("Resume in {0}s...", i);
                }
                else if (i < 60 && i % 10 == 0)
                {
                    this.LogDebug("Resume in {0}s...", i);
                }
                else if (i % 60 == 0)
                {
                    this.LogDebug("Resume in {0}m...", i / 60);
                }

                this.Context.CancellationToken.WaitHandle.WaitOne(1000);
            }

            this.LogInformation("Execution resumed.");
        }
    }
}
