using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Actions.Files;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Extensions.Operations.Files;
using Inedo.IO;

namespace Inedo.Extensions.Legacy.ActionImporters
{
    internal static class Files
    {
        public sealed class TransferFiles : IActionOperationConverter<TransferFilesAction, TransferFilesOperation>
        {
            public ConvertedOperation<TransferFilesOperation> ConvertActionToOperation(TransferFilesAction action, IActionConverterContext context)
            {
                var masks = context.ConvertLegacyMask(action.IncludeFileMasks, true);

                var actionConfigProp = context.GetType().GetProperty("ActionConfig", BindingFlags.NonPublic | BindingFlags.Instance);
                var actionConfig = (Tables.ActionGroupActions_Extended)actionConfigProp.GetValue(context);

                return new TransferFilesOperation
                {
                    SourceDirectory = context.ConvertLegacyExpression(AH.CoalesceString(action.SourceDirectory, "$WorkingDirectory")),
                    TargetDirectory = context.ConvertLegacyExpression(action.TargetDirectory),
                    DeleteTarget = action.DeleteTarget,
                    Includes = masks.Includes,
                    Excludes = masks.Excludes,
                    SourceServerName = context.ActionServerName,
                    TargetServerName = MungeName(context, actionConfig.Target_Server_Name, actionConfig.Target_Server_Variable_Name)
                };
            }

            private static string MungeName(IActionConverterContext context, string name, string variableName)
            {
                if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(variableName))
                    return null;

                if (string.IsNullOrEmpty(variableName))
                    return name;

                var wrappedVariableName = "${" + variableName + "}";
                return context.ConvertLegacyExpression(wrappedVariableName);
            }
        }

        public sealed class CopyFiles : IActionOperationConverter<CopyFilesAction, CopyFilesOperation>
        {
            public ConvertedOperation<CopyFilesOperation> ConvertActionToOperation(CopyFilesAction action, IActionConverterContext context)
            {
                var masks = context.ConvertLegacyMask(action.IncludeFileMasks, action.Recursive);
                return new CopyFilesOperation
                {
                    SourceDirectory = context.ConvertLegacyExpression(action.OverriddenSourceDirectory),
                    TargetDirectory = context.ConvertLegacyExpression(action.OverriddenTargetDirectory),
                    Includes = masks.Includes,
                    Excludes = masks.Excludes,
                    Overwrite = action.Overwrite,
                    VerboseLogging = action.VerboseLogging
                };
            }
        }

        public sealed class ConcatenateFiles : IActionOperationConverter<ConcatenateFilesAction, ConcatenateFilesOperation>
        {
            public ConvertedOperation<ConcatenateFilesOperation> ConvertActionToOperation(ConcatenateFilesAction action, IActionConverterContext context)
            {
                var masks = context.ConvertLegacyMask(action.FileMasks, action.Recursive);
                var separator = AH.NullIf(context.ConvertLegacyExpression(action.ContentSeparationText), string.Empty);
                if (!string.IsNullOrEmpty(separator))
                    separator = Regex.Replace(separator, @"\r?\n", action.ForceLinuxNewlines ? "${LinuxNewLine}" : "${NewLine}");

                return new ConcatenateFilesOperation
                {
                    Includes = masks.Includes,
                    Excludes = masks.Excludes,
                    ContentSeparationText = separator,
                    OutputFile = context.ConvertLegacyExpression(PathEx.Combine(action.OverriddenTargetDirectory, action.OutputFile)),
                    OutputFileEncoding = context.ConvertLegacyExpression(action.OutputFileEncoding),
                    SourceDirectory = context.ConvertLegacyExpression(AH.NullIf(action.OverriddenSourceDirectory, string.Empty))
                };
            }
        }

        public sealed class CreateFile : IActionOperationConverter<CreateFileAction, CreateFileOperation>
        {
            public ConvertedOperation<CreateFileOperation> ConvertActionToOperation(CreateFileAction action, IActionConverterContext context)
            {
                return new CreateFileOperation
                {
                    FileName = context.ConvertLegacyExpression(PathEx.Combine(action.OverriddenSourceDirectory, action.FileName)),
                    Text = context.ConvertLegacyExpression(action.Contents),
                    Overwrite = true
                };
            }
        }

        public sealed class DeleteFiles : IActionOperationConverter<DeleteFilesAction, DeleteFilesOperation>
        {
            public ConvertedOperation<DeleteFilesOperation> ConvertActionToOperation(DeleteFilesAction action, IActionConverterContext context)
            {
                var masks = context.ConvertLegacyMask(action.FileMasks, action.Recursive);
                return new DeleteFilesOperation
                {
                    Includes = masks.Includes,
                    Excludes = masks.Excludes,
                    SourceDirectory = context.ConvertLegacyExpression(AH.NullIf(action.OverriddenSourceDirectory, string.Empty)),
                    VerboseLogging = action.LogVerbose
                };
            }
        }

        public sealed class RenameFiles : IActionOperationConverter<RenameFilesAction, RenameFileOperation>
        {
            public ConvertedOperation<RenameFileOperation> ConvertActionToOperation(RenameFilesAction action, IActionConverterContext context)
            {
                if (action.SourceMask.Contains("*") || action.SourceMask.Contains("?") || action.TargetMask.Contains("*") || action.TargetMask.Contains("?"))
                    return null;

                return new RenameFileOperation
                {
                    SourceFileName = context.ConvertLegacyExpression(PathEx.Combine(action.OverriddenSourceDirectory, action.SourceMask)),
                    TargetFileName = context.ConvertLegacyExpression(PathEx.Combine(action.OverriddenSourceDirectory, action.TargetMask)),
                    Overwrite = action.OverwriteExisting,
                };
            }
        }

        public sealed class CreateZip : IActionOperationConverter<CreateZipFileAction, CreateZipFileOperation>
        {
            public ConvertedOperation<CreateZipFileOperation> ConvertActionToOperation(CreateZipFileAction action, IActionConverterContext context)
            {
                return new CreateZipFileOperation
                {
                    DirectoryToZip = context.ConvertLegacyExpression(AH.NullIf(action.OverriddenSourceDirectory, string.Empty)),
                    FileName = context.ConvertLegacyExpression(PathEx.Combine(action.OverriddenTargetDirectory, action.FileName)),
                    Overwrite = true
                };
            }
        }

        public sealed class ExtractZip : IActionOperationConverter<UnZipFileAction, UnzipFileOperation>
        {
            public ConvertedOperation<UnzipFileOperation> ConvertActionToOperation(UnZipFileAction action, IActionConverterContext context)
            {
                return new UnzipFileOperation
                {
                    ClearTargetDirectory = string.IsNullOrEmpty(action.OverriddenTargetDirectory),
                    FileName = context.ConvertLegacyExpression(PathEx.Combine(action.OverriddenSourceDirectory, action.FileName)),
                    TargetDirectory = AH.NullIf(context.ConvertLegacyExpression(action.OverriddenTargetDirectory), string.Empty),
                    Overwrite = true
                };
            }
        }

        public sealed class SetAttributes : IActionOperationConverter<SetFileAttributesAction, SetFileAttributesOperation>
        {
            public ConvertedOperation<SetFileAttributesOperation> ConvertActionToOperation(SetFileAttributesAction action, IActionConverterContext context)
            {
                var mask = context.ConvertLegacyMask(action.FileMasks, action.Recursive);
                return new SetFileAttributesOperation
                {
                    Includes = mask.Includes,
                    Excludes = mask.Excludes,
                    VerboseLogging = true,
                    SourceDirectory = AH.NullIf(context.ConvertLegacyExpression(action.OverriddenTargetDirectory), string.Empty),
                    ReadOnly = action.ReadOnly,
                    Hidden = action.Hidden,
                    System = action.System
                };
            }
        }

        public sealed class ReplaceText : IActionOperationConverter<ReplaceFileAction, ReplaceFileTextOperation>
        {
            public ConvertedOperation<ReplaceFileTextOperation> ConvertActionToOperation(ReplaceFileAction action, IActionConverterContext context)
            {
                var mask = context.ConvertLegacyMask(action.FileNameMasks, action.Recursive);
                return new ReplaceFileTextOperation
                {
                    Includes = mask.Includes,
                    Excludes = mask.Excludes,
                    SearchText = context.ConvertLegacyExpression(action.SearchText),
                    ReplaceText = context.ConvertLegacyExpression(action.ReplaceText),
                    UseRegex = action.UseRegex,
                    SourceDirectory = AH.NullIf(context.ConvertLegacyExpression(action.OverriddenSourceDirectory), string.Empty)
                };
            }
        }
    }
}
