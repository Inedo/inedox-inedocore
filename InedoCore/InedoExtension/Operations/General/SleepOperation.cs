using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.General
{
    [DisplayName("Sleep")]
    [Description("Halts the execution of operations for the specified number of seconds.")]
    [ScriptAlias("Sleep")]
    [ScriptAlias("Wait")]
    [DefaultProperty(nameof(Seconds))]
    public sealed class SleepOperation : ExecuteOperation
    {
        private long startTicks;
        private long endTicks;

        [Required]
        [ScriptAlias("Seconds")]
        public int Seconds { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Sleep for ", new Hilite(config[nameof(this.Seconds)]), " seconds")
            );
        }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.startTicks = DateTime.UtcNow.Ticks;
            this.endTicks = this.startTicks + (TimeSpan.TicksPerSecond * this.Seconds);
            this.LogInformation($"Sleeping for {this.Seconds} seconds...");
            await Task.Delay(this.Seconds * 1000, context.CancellationToken).ConfigureAwait(false);

            this.LogInformation("Done sleeping.");
        }
        public override OperationProgress GetProgress()
        {
            long delta = Math.Max(this.endTicks - this.startTicks, 0);
            long current = DateTime.UtcNow.Ticks - this.startTicks;
            long remaining = Math.Max(delta - current, 0);
            return new OperationProgress(Math.Min((int)((100.0 * current) / delta), 100), (remaining / TimeSpan.TicksPerSecond).ToString() + "s remaining");
        }
    }
}
