using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.Files
{
    [PersistFrom("Inedo.BuildMaster.Extensibility.Actions.Files.SetFileAttributesAction,BuildMasterExtensions")]
    [DisplayName("Change File Attributes")]
    [Description("Sets or clears the read-only, system, or hidden attributes on one or more files.")]
    [CustomEditor(typeof(SetFileAttributesActionEditor))]
    [Tag(Tags.Files)]
    public sealed class SetFileAttributesAction : RemoteActionBase, IMissingPersistentPropertyHandler
    {
        [Persistent]
        public string[] FileMasks { get; set; }

        [Persistent]
        public bool Recursive { get; set; }

        [Persistent]
        public bool? ReadOnly { get; set; }
        
        [Persistent]
        public bool? Hidden { get; set; }
        
        [Persistent]
        public bool? System { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            var attribs = new  Dictionary<string, bool?>
            { 
                { "ReadOnly", this.ReadOnly },
                { "Hidden", this.Hidden },
                { "System", this.System  }
            };

            if (attribs.Where(kvp => kvp.Value.HasValue).Select(kvp => kvp.Value.Value).Distinct().Count() == 1)
            {
                return new ExtendedRichDescription(
                    new RichDescription(
                        attribs.Where(kvp => kvp.Value.HasValue).First().Value.Value ? "Set " : "Clear ",
                        new ListHilite(attribs.Where(kvp => kvp.Value.HasValue).Select(kvp => kvp.Key)),
                        " Attributes on ", new ListHilite(this.FileMasks)),
                    new RichDescription("in ", new DirectoryHilite(this.OverriddenSourceDirectory)));
            }
            else
            {
                return new ExtendedRichDescription(
                    new RichDescription(
                        "Change Attributes on ", new ListHilite(this.FileMasks)),
                    new RichDescription("in ", new DirectoryHilite(this.OverriddenSourceDirectory)));
            }
        }

        protected override void Execute()
        {
            if (this.FileMasks.Length == 0)
            {
                this.LogWarning("FileMasks is empty; nothing to do.");
                return;
            }

            if (this.ReadOnly.HasValue)
                this.LogDebug("Setting ReadOnly: " + this.ReadOnly.Value);
            if (this.Hidden.HasValue)
                this.LogDebug("Setting Hidden: " + this.Hidden.Value);
            if (this.System.HasValue)
                this.LogDebug("Setting System: " + this.System.Value);

            this.ExecuteRemoteCommand(null);
            this.LogInformation("Set attributes complete.");
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();

            var rootDir = fileOps.GetDirectoryEntry(
                new GetDirectoryEntryCommand
                {
                    Path = this.Context.SourceDirectory,
                    Recurse = this.Recursive,
                    IncludeRootPath = true
                }
            ).Entry;

            var matchedFileEntries = Util.Files.Comparison.GetMatches(
                this.Context.SourceDirectory,
                rootDir,
                this.FileMasks
            ).OfType<ExtendedFileEntryInfo>();
            
            FileAttributes attributesToChange = 0;
            if (this.ReadOnly.HasValue)
                attributesToChange |= FileAttributes.ReadOnly;
            if (this.Hidden.HasValue)
                attributesToChange |= FileAttributes.Hidden;
            if (this.System.HasValue)
                attributesToChange |= FileAttributes.System;

            FileAttributes attributeValues = 0;
            if (this.ReadOnly.GetValueOrDefault())
                attributeValues |= FileAttributes.ReadOnly;
            if (this.Hidden.GetValueOrDefault())
                attributeValues |= FileAttributes.Hidden;
            if (this.System.GetValueOrDefault())
                attributeValues |= FileAttributes.System;

            foreach (var entry in matchedFileEntries)
            {
                var fileAttributes = entry.Attributes;

                if (((fileAttributes & attributesToChange) ^ attributeValues) != 0)
                {
                    fileAttributes &= ~attributesToChange;
                    fileAttributes |= attributeValues;
                    this.LogDebug("Changing " + entry.Path + "...");
                    fileOps.SetAttributesAsync(entry.Path, fileAttributes).WaitAndUnwrapExceptions();
                }
            }

            return string.Empty;
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            string readOnly;
            if (missingProperties.TryGetValue("ReadOnlyState", out readOnly))
                this.ReadOnly = Util.Bool.ParseF(readOnly);
        }
    }
}
