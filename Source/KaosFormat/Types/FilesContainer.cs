using System;
using System.Collections.Generic;
using System.IO;
using KaosIssue;

namespace KaosFormat
{
    public abstract class FilesContainer : FormatBase
    {
        public abstract new class Model : FormatBase.Model
        {
            public readonly FileItem.Vector.Model FilesModel;
            public new FilesContainer Data => (FilesContainer) _data;

            public Model (string rootPath)
             => FilesModel = new FileItem.Vector.Model (rootPath);

            public void SetAllowRooted (bool allow)
             => Data.ForbidRooted = ! allow;

            public void SetIgnoredName (string name)
             => Data.IgnoredName = name;

            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                if (base.Data.Issues.HasFatal)
                    return;

                if ((validationFlags & Validations.Exists) != 0)
                {
                    Data.MissingCount = 0;
                    if (Data.Files.Items.Count != 1 || Data.Files.Items[0].Name != Data.IgnoredName)
                        for (int ix = 0; ix < Data.Files.Items.Count; ++ix)
                        {
                            FileItem item = Data.Files.Items[ix];
                            var name = item.Name;

                            if (Data.AllowNonFile && (name.StartsWith ("http:") || name.StartsWith ("https:")))
                                IssueModel.Add ($"Ignoring URL '{name}'.", Severity.Trivia);
                            else
                            {
                                try
                                {
                                    if (! System.IO.Path.IsPathRooted (name))
                                        name = Data.Files.RootDir + System.IO.Path.DirectorySeparatorChar + name;
                                    else if (Data.ForbidRooted)
                                        IssueModel.Add ($"File is rooted: '{item.Name}'.");
                                }
                                catch (ArgumentException ex)
                                {
                                    IssueModel.Add ($"Malformed file name '{name}': {ex.Message}");
                                    FilesModel.SetIsFound (ix, false);
                                    ++Data.MissingCount;
                                    continue;
                                }

                                // Exists doesn't seem to throw any exceptions, so no try/catch.
                                bool isFound = File.Exists (name);
                                FilesModel.SetIsFound (ix, isFound);
                                if (! isFound)
                                {
                                    IssueModel.Add ($"File '{item.Name}' not found.", Severity.Advisory);
                                    ++Data.MissingCount;
                                }
                            }
                        }
                }

                base.CalcHashes (hashFlags, validationFlags);
            }

            protected void GetDiagnostics (string repairPrompt=null, Func<bool,string> repairer=null)
            {
                if (Data.MissingCount == null)
                    return;

                Severity sev = Severity.Advisory;
                IssueTags tag;
                var sfx = Data.Files.Items.Count == 1 ? String.Empty : "s";
                var msg = $"Existence check{sfx} of {Data.Files.Items.Count} file{sfx}";

                if (Data.MissingCount.Value == 0)
                {
                    msg += " successful.";
                    tag = IssueTags.Success;
                    repairPrompt = null;
                    repairer = null;
                }
                else
                {
                    msg += $" failed with {Data.MissingCount} not found.";
                    sev = repairPrompt != null ? Severity.Warning : Severity.Error;
                    tag = IssueTags.Failure;
                }

                Data.FcIssue = IssueModel.Add (msg, sev, tag, repairPrompt, repairer);
            }
        }


        public FileItem.Vector Files { get; private set; }
        public bool AllowNonFile { get; private set; }
        public bool ForbidRooted { get; private set; }
        public string IgnoredName { get; private set; }

        public int? MissingCount { get; private set; }
        public Issue FcIssue { get; protected set; }

        protected FilesContainer (Model model, Stream stream, string path) : base (model, stream, path)
        {
            this.Files = model.FilesModel.Data;
            this.AllowNonFile = true;
        }

        public override void GetReportDetail (IList<string> report)
        {
            if (Files.Items.Count == 0)
                return;

            if (report.Count != 0)
                report.Add (String.Empty);

            report.Add ("Files:");
            foreach (var item in Files.Items)
                report.Add ((item.IsFound == true? "+ " : (item.IsFound == false ? "* " : "  ")) + item.Name);
        }
    }
}
