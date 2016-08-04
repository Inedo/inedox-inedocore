using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using Inedo.Documentation;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.PromotionRequirements;
using Inedo.BuildMaster.Web;
using Inedo.IO;
using Inedo.Serialization;
using Inedo.Agents;

namespace Inedo.BuildMaster.Extensibility.Actions.General
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.General.CommandLineAction,BuildMasterExtensions")]
    [DisplayName("Execute Command Line"), Description("Runs a process via command line with arguments on the target server.")]
    [CustomEditor(typeof(CommandLineActionEditor))]
    [RequiresInterface(typeof(IFileOperationsExecuter))]
    [RequiresInterface(typeof(IRemoteProcessExecuter))]
    [Tag(Tags.Files)]
    [Tag(Tags.General)]
    public sealed class CommandLineAction : AgentBasedActionBase, IMissingPersistentPropertyHandler
    {
        [Persistent]
        public string ExePath { get; set; }
        [Persistent]
        public string Arguments { get; set; }
        [Persistent]
        public CommandLineSuccessExitCode SuccessExitCode { get; set; }
        [Persistent]
        public bool DoNotFailOnStandardError { get; set; }
        [Persistent]
        public bool ImportVariables { get; set; }

        protected override bool AddBuildMasterVariablesAsEnvironmentVariables
        {
            get { return this.ImportVariables; }
        }
        protected override bool LogProcessStandardErrorAsError
        {
            get { return !this.DoNotFailOnStandardError; }
        }

        public override ExtendedRichDescription GetActionDescription()
        {
            var longDesc = new RichDescription("in ", new DirectoryHilite(this.OverriddenSourceDirectory));
            if (!string.IsNullOrEmpty(this.Arguments))
                longDesc.AppendContent(" with arguments ", new Hilite(this.Arguments));

            return new ExtendedRichDescription(
                new RichDescription("Execute ", new Hilite(PathEx.GetFileName(this.ExePath))),
                longDesc
            );
        }

        protected override void Execute()
        {
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();
            var absExePath = this.GetAbsPath(fileOps, this.ExePath);

            this.LogDebug("Creating working directory \"{0}\" if it does not exist...", this.Context.SourceDirectory);
            fileOps.CreateDirectory(this.Context.SourceDirectory);

            this.LogInformation("Executing \"{0}\"...", this.ExePath);
            
            int exitCode;
            try
            {
                exitCode = this.ExecuteCommandLine(
                    new RemoteProcessStartInfo
                    {
                        FileName = absExePath,
                        Arguments = this.Arguments,
                        WorkingDirectory = this.Context.SourceDirectory
                    }
                );
            }
            catch (Win32Exception wex)
            {
                this.WarnIfBatchOrShortcutFile(wex.Message, absExePath);
                throw;
            }

            if (this.SuccessExitCode == CommandLineSuccessExitCode.Ignore)
            {
                this.LogDebug("Process exit code: {0} (0x{0:X8})", exitCode);
            }
            else
            {
                bool success = true;

                switch (this.SuccessExitCode)
                {
                    case CommandLineSuccessExitCode.Zero:
                        success = exitCode == 0;
                        break;

                    case CommandLineSuccessExitCode.Positive:
                        success = exitCode > 0;
                        break;

                    case CommandLineSuccessExitCode.Negative:
                        success = exitCode < 0;
                        break;

                    case CommandLineSuccessExitCode.NonZero:
                        success = exitCode != 0;
                        break;

                    case CommandLineSuccessExitCode.NonNegative:
                        success = exitCode >= 0;
                        break;
                }

                if (success)
                    this.LogDebug("Process exit code: {0} (0x{0:X8})", exitCode);
                else
                    this.LogError("Process exit code indicates error: {0} (0x{0:X8})", exitCode);
            }

            this.LogInformation("Process execution completed.");
        }

        private void WarnIfBatchOrShortcutFile(string message, string exePath)
        {
            string fileExtension = Path.GetExtension(exePath) ?? "";

            if (!string.Equals(fileExtension, ".bat", StringComparison.OrdinalIgnoreCase)
             && !string.Equals(fileExtension, ".lnk", StringComparison.OrdinalIgnoreCase))
                return;

            if (!Regex.IsMatch(message, @"The specified executable is not a valid application for this OS platform", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return;
                    
            this.LogWarning(string.Format(@"
Windows reported error 0x80004005 (""The specified executable is not a valid application for this OS platform"") 
when attempting to start the specified process; this can happen if a non-executable file is specified. Note that 
files such as .bat and .lnk are not executable files, but are instead parsed and executed by the shell (cmd.exe, start.exe, etc.) 
To execute a .bat file, try changing the file's extension to .cmd, or execute: 
  
  cmd.exe /C ""{0} {1}"" 

to have the shell run this file for you.", exePath, this.Arguments));
        }

        private string GetAbsPath(IFileOperationsExecuter agt, string path)
        {
            // TODO This duplicates pathing logic in AgentHelper::GetWorkingDirectory
            //   The reason we have to do this here is because the RemoteActionExecutor
            //   will split the working directory into source and target. Until there is
            //   a way to override or block the splitting, then this logic is required.

            if (path == null)
            {
                return this.Context.SourceDirectory;
            }
            else if (Path.IsPathRooted(path))
            {
                return path;
            }
            else if (path.StartsWith("~"))
            {
                return agt.GetLegacyWorkingDirectory((IGenericBuildMasterContext)this.Context, path);
            }
            else
            {
                return agt.CombinePath(
                    this.Context.SourceDirectory,
                    path
                );
            }
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            string workingDirectory;
            if (missingProperties.TryGetValue("WorkingDirectory", out workingDirectory) && !string.IsNullOrEmpty(workingDirectory))
                this.OverriddenSourceDirectory = workingDirectory;
        }
    }

    public enum CommandLineSuccessExitCode
    {
        /// <summary>
        /// An exit code of 0 indicates success.
        /// </summary>
        Zero,
        /// <summary>
        /// A positive exit code indicates success.
        /// </summary>
        Positive,
        /// <summary>
        /// A negative exit code indicates success.
        /// </summary>
        Negative,
        /// <summary>
        /// A nonzero exit code indicates success.
        /// </summary>
        NonZero,
        /// <summary>
        /// A nonnegative exit code indicates success.
        /// </summary>
        NonNegative,
        /// <summary>
        /// The exit code is ignored.
        /// </summary>
        Ignore
    }
}
