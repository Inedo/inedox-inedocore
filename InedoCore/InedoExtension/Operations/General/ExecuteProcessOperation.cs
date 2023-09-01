using System.Text.RegularExpressions;
using Inedo.Agents;

namespace Inedo.Extensions.Operations.General
{
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
        private readonly Lazy<Regex> warnRegex;
        private readonly Lazy<Regex> progressRegex;
        private readonly Lazy<Regex> outputFilterRegex;
        private volatile int percent;
        private volatile string status;

        public ExecuteProcessOperation()
        {
            this.warnRegex = new Lazy<Regex>(() => !string.IsNullOrWhiteSpace(this.WarningTextRegex) ? new Regex(this.WarningTextRegex) : null);
            this.progressRegex = new Lazy<Regex>(() => !string.IsNullOrWhiteSpace(this.ReportProgressRegex) ? new Regex(this.ReportProgressRegex) : null);
            this.outputFilterRegex = new Lazy<Regex>(() => !string.IsNullOrWhiteSpace(this.OutputTextRegex) ? new Regex(this.OutputTextRegex) : null);
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
        [Category("Logging")]
        [ScriptAlias("LogArguments")]
        [DisplayName("Log arguments")]
        [DefaultValue(true)]
        public bool LogArguments { get; set; } = true;
        [Category("Advanced")]
        [ScriptAlias("ReportProgressRegex")]
        [DisplayName("Report progress regex")]
        [Description("When set to a valid regular expression string, attempts to parse every output message for real-time progress updates. To capature a status message, use a group with name \"m\". To capture a percent complete as a number from 0 to 100, use a group with name \"p\".")]
        public string ReportProgressRegex { get; set; }
        [Category("Advanced")]
        [ScriptAlias("OutputFilterRegex")]
        [DisplayName("Output filter regex")]
        [Description("When set to a valid regular expression string, only output messages which match this expression will be logged.")]
        public string OutputTextRegex { get; set; }

        public override OperationProgress GetProgress() => new(AH.NullIf(this.percent, -1), this.status);

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

            if (!string.IsNullOrEmpty(this.Target) && (!string.IsNullOrEmpty(this.FileName) || !string.IsNullOrEmpty(this.Arguments)))
            {
                this.LogWarning("When using the default property (i.e. \"exec process.exe args (...)\", do not specify a Filename or Arguments property, because they will be ignored.");
            }

            var startInfo = GetFileNameAndArguments(this.FileName, this.Arguments, this.Target);
            if (startInfo == null)
            {
                this.LogError("Invalid configuration; Target or FileName must be specified.");
                return;
            }

            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();
            var resolvedFileName = context.ResolvePath(startInfo.FileName);
            if (await fileOps.FileExistsAsync(resolvedFileName))
                startInfo.FileName = resolvedFileName;
            startInfo.WorkingDirectory = context.ResolvePath(this.WorkingDirectory);

            this.LogDebug("Process: " + startInfo.FileName);
            if (this.LogArguments)
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
            var progressRegex = this.progressRegex.Value;
            if (progressRegex != null)
            {
                var match = progressRegex.Match(e.Data);
                if (match.Success)
                {
                    var percentGroup = match.Groups["p"];
                    if (percentGroup != null && percentGroup.Success)
                    {
                        // use decimal to allow for easier capturing of percentages
                        if (decimal.TryParse(percentGroup.Value, out var percent))
                            this.percent = (int)Math.Min(Math.Max(percent, 0), 100);
                    }

                    var statusGroup = match.Groups["m"];
                    if (statusGroup != null && statusGroup.Success)
                        this.status = AH.NullIf(statusGroup.Value, string.Empty);
                }
            }

            var filterRegex = this.outputFilterRegex.Value;
            if (filterRegex != null)
            {
                if (!filterRegex.IsMatch(e.Data))
                    return;
            }

            var warnRegex = this.warnRegex.Value;
            if (warnRegex != null)
            {
                var match = warnRegex.Match(e.Data);
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
                    arguments = target[match.Length..].Trim();
                }
                else
                {
                    var match2 = Regex.Match(target, "\\s");
                    if (match2.Success)
                    {
                        fileName = target[..match2.Index].Trim();
                        arguments = target[match2.Index..].Trim();
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
                return this.Operator switch
                {
                    "=" or "==" => exitCode == this.Value,
                    "!=" => exitCode != this.Value,
                    "<" => exitCode < this.Value,
                    ">" => exitCode > this.Value,
                    "<=" => exitCode <= this.Value,
                    ">=" => exitCode >= this.Value,
                    _ => false
                };
            }
        }
    }
}
