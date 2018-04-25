using System;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Operations.General
{
    [DisplayName("Execute Process")]
    [Description("Executes a process, logs its output, and waits until it exits.")]
    [ScriptAlias("Exec")]
    [ScriptAlias("Execute-Process")]
    [DefaultProperty(nameof(Target))]
    [Example(@"
# execute 7zip and only succeed if the executable returns a non-negative exit code
Exec c:\tools\7za.exe (
    Arguments: i *.*,
    SuccessExitCode: >= 0
);")]
    public sealed class ExecuteProcessOperation : ExecuteOperation
    {
        private Lazy<Regex> warnRegex;

        public ExecuteProcessOperation()
        {
            this.warnRegex = new Lazy<Regex>(
                () => !string.IsNullOrWhiteSpace(this.WarningTextRegex) ? new Regex(this.WarningTextRegex) : null
            );
        }

        public string Target { get; set; }

        [ScriptAlias("FileName")]
        [DisplayName("File name")]
        public string FileName { get; set; }
        [ScriptAlias("Arguments")]
        public string Arguments { get; set; }
        [ScriptAlias("WorkingDirectory")]
        [DisplayName("Working directory")]
        public string WorkingDirectory { get; set; }
        [Category("Logging")]
        [ScriptAlias("OutputLogLevel")]
        [DisplayName("Output log level")]
        public MessageLevel OutputLevel { get; set; } = MessageLevel.Information;
        [Category("Logging")]
        [ScriptAlias("ErrorOutputLogLevel")]
        [DisplayName("Error log level")]
        public MessageLevel ErrorLevel { get; set; } = MessageLevel.Error;
        [ScriptAlias("SuccessExitCode")]
        [DisplayName("Success exit code")]
        [Description("Integer exit code which indicates no error. The default is 0. This can also be an integer prefixed with an inequality operator.")]
        [Example("SuccessExitCode: 0 # Fail on nonzero.")]
        [Example("SuccessExitCode: >= 0 # Fail on negative numbers.")]
        [DefaultValue("== 0")]
        public string SuccessExitCode { get; set; }
        [ScriptAlias("ImportVariables")]
        [DisplayName("Import variables")]
        [Description("When set to true, all scalar execution variables currently accessible will be exported as environment variables to the process.")]
        public bool ImportVariables { get; set; }
        [Category("Logging")]
        [ScriptAlias("WarnRegex")]
        [DisplayName("Warning regex")]
        [Description("When set to a valid regular expression string, output messages which are matched will be logged as warnings. To log only part of the message, use a group with name \"m\".")]
        public string WarningTextRegex { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var startInfo = GetFileNameAndArguments(config[nameof(FileName)], config[nameof(Arguments)], config[nameof(Target)]);
            var shortDesc = new RichDescription(
                "Execute ",
                new DirectoryHilite(startInfo?.FileName),
                " ",
                new Hilite(startInfo?.Arguments)
            );

            var longDesc = new RichDescription(
                "in ",
                new DirectoryHilite(config[nameof(WorkingDirectory)])
            );

            return new ExtendedRichDescription(shortDesc, longDesc);
        }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (context.Agent == null)
            {
                this.LogError("Server context is required to execute a process.");
                return;
            }

            var startInfo = GetFileNameAndArguments(this.FileName, this.Arguments, this.Target);
            if (startInfo == null)
            {
                this.LogError("Invalid configuration; Target or FileName must be specified.");
                return;
            }

            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();
            startInfo.FileName = context.ResolvePath(startInfo.FileName);
            startInfo.WorkingDirectory = context.ResolvePath(this.WorkingDirectory);

            this.LogDebug("Process: " + startInfo.FileName);
            this.LogDebug("Arguments: " + startInfo.Arguments);
            this.LogDebug("Working directory: " + startInfo.WorkingDirectory);

            this.LogDebug($"Ensuring that {startInfo.WorkingDirectory} exists...");
            await fileOps.CreateDirectoryAsync(startInfo.WorkingDirectory).ConfigureAwait(false);

            this.LogInformation($"Executing {startInfo.FileName}...");

            var remoteProcess = context.Agent.GetService<IRemoteProcessExecuter>();
            int exitCode;

            using (var process = remoteProcess.CreateProcess(startInfo))
            {
                process.OutputDataReceived += this.Process_OutputDataReceived;
                process.ErrorDataReceived += this.Process_ErrorDataReceived;

                process.Start();
                try
                {
                    await process.WaitAsync(context.CancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    try
                    {
                        process.Terminate();
                    }
                    catch
                    {
                    }

                    throw;
                }

                exitCode = process.ExitCode ?? -1;
            }

            bool exitCodeLogged = false;

            if (!string.IsNullOrEmpty(this.SuccessExitCode))
            {
                var comparator = ExitCodeComparator.TryParse(this.SuccessExitCode);
                if (comparator != null)
                {
                    bool result = comparator.Evaluate(exitCode);
                    if (result)
                        this.LogInformation($"Process exited with code: {exitCode} (success)");
                    else
                        this.LogError($"Process exited with code: {exitCode} (failure)");

                    exitCodeLogged = true;
                }
            }

            if (!exitCodeLogged)
                this.LogDebug("Process exited with code: " + exitCode);
        }

        private void Process_ErrorDataReceived(object sender, ProcessDataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                this.Log(this.ErrorLevel, e.Data);
        }
        private void Process_OutputDataReceived(object sender, ProcessDataReceivedEventArgs e)
        {
            var regex = this.warnRegex.Value;
            if (regex != null)
            {
                var match = regex.Match(e.Data);
                if (match.Success)
                {
                    this.LogWarning(AH.CoalesceString(match.Groups["m"]?.Value, e.Data));
                    return;
                }
            }

            this.Log(this.OutputLevel, e.Data);
        }

        private static RemoteProcessStartInfo GetFileNameAndArguments(string fileName, string arguments, string target)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
            }
            else if (!string.IsNullOrEmpty(target))
            {
                var match = Regex.Match(target, "^\"(?<1>[^\"]+)\"");
                if (match.Success)
                {
                    fileName = match.Groups[1].Value;
                    arguments = target.Substring(match.Length).Trim();
                }
                else
                {
                    var match2 = Regex.Match(target, "\\s");
                    if (match2.Success)
                    {
                        fileName = target.Substring(0, match2.Index).Trim();
                        arguments = target.Substring(match2.Index).Trim();
                    }
                    else
                    {
                        fileName = target;
                        arguments = null;
                    }
                }
            }
            else
            {
                return null;
            }

            return new RemoteProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments
            };
        }

        private sealed class ExitCodeComparator
        {
            private static readonly string[] ValidOperators = new[] { "=", "==", "!=", "<", ">", "<=", ">=" };

            private ExitCodeComparator(string op, int value)
            {
                this.Operator = op;
                this.Value = value;
            }

            public string Operator { get; }
            public int Value { get; }

            public static ExitCodeComparator TryParse(string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                    return null;

                var match = Regex.Match(s, @"^\s*(?<1>[=<>!])*\s*(?<2>[0-9]+)\s*$", RegexOptions.ExplicitCapture);
                if (!match.Success)
                    return null;

                var op = match.Groups[1].Value;
                if (string.IsNullOrEmpty(op) || !ValidOperators.Contains(op))
                    op = "==";

                return new ExitCodeComparator(op, int.Parse(match.Groups[2].Value));
            }

            public bool Evaluate(int exitCode)
            {
                switch (this.Operator)
                {
                    case "=":
                    case "==":
                        return exitCode == this.Value;

                    case "!=":
                        return exitCode != this.Value;

                    case "<":
                        return exitCode < this.Value;

                    case ">":
                        return exitCode > this.Value;

                    case "<=":
                        return exitCode <= this.Value;

                    case ">=":
                        return exitCode >= this.Value;
                }

                return false;
            }
        }
    }
}
