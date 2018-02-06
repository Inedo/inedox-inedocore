using System.Linq;
using Inedo.BuildMaster.Extensibility.Actions.General;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Diagnostics;
using Inedo.Extensions.Operations.General;

namespace Inedo.Extensions.Legacy.ActionImporters
{
    internal static class General
    {
        public sealed class CommandLine : IActionOperationConverter<CommandLineAction, ExecuteProcessOperation>
        {
            public ConvertedOperation<ExecuteProcessOperation> ConvertActionToOperation(CommandLineAction action, IActionConverterContext context)
            {
                return new ExecuteProcessOperation
                {
                    FileName = context.ConvertLegacyExpression(action.ExePath),
                    Arguments = AH.NullIf(context.ConvertLegacyExpression(action.Arguments), string.Empty),
                    ErrorLevel = action.DoNotFailOnStandardError ? MessageLevel.Information : MessageLevel.Error,
                    WorkingDirectory = AH.NullIf(context.ConvertLegacyExpression(action.OverriddenSourceDirectory), string.Empty),
                    SuccessExitCode = AH.Switch<CommandLineSuccessExitCode, string>(action.SuccessExitCode)
                        .Case(CommandLineSuccessExitCode.Zero, "0")
                        .Case(CommandLineSuccessExitCode.Negative, "<0")
                        .Case(CommandLineSuccessExitCode.Positive, ">0")
                        .Case(CommandLineSuccessExitCode.NonNegative, ">=0")
                        .Case(CommandLineSuccessExitCode.Ignore, "ignore")
                        .End()
                };
            }
        }

        public sealed class Sleep : IActionOperationConverter<SleepAction, SleepOperation>
        {
            public ConvertedOperation<SleepOperation> ConvertActionToOperation(SleepAction action, IActionConverterContext context)
            {
                return new ConvertedOperation<SleepOperation>
                {
                    [nameof(SleepOperation.Seconds)] = context.ConvertLegacyExpression(action.SecondsToSleep)
                };
            }
        }

        public sealed class SendEmail : IActionOperationConverter<SendEmailAction, SendEmailOperation>
        {
            public ConvertedOperation<SendEmailOperation> ConvertActionToOperation(SendEmailAction action, IActionConverterContext context)
            {
                return new ConvertedOperation<SendEmailOperation>(
                    new SendEmailOperation
                    {
                        To = from a in context.ConvertLegacyExpression(action.To)?.Split(';') ?? new string[0]
                             where !string.IsNullOrWhiteSpace(a)
                             select a.Trim(),
                        Subject = context.ConvertLegacyExpression(action.Subject),
                        BodyHtml = action.IsBodyHtml ? context.ConvertLegacyExpression(action.Message) : null,
                        BodyText = !action.IsBodyHtml ? context.ConvertLegacyExpression(action.Message) : null,
                        Attachments = !string.IsNullOrWhiteSpace(action.Attachment) ? new[] { context.ConvertLegacyExpression(action.Attachment) } : null
                    }
                )
                {
                    ServerId = action.ServerId
                };
            }
        }
    }
}
