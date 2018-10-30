﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KaosIssue;
using KaosFormat;
using KaosSysIo;

namespace KaosDiags
{
    public partial class Diags
    {
        public class Model
        {
            protected Diags _data;
            public Diags Data => _data;
            public FileFormat.Vector.Model FormatModel;

            public Model (string root, string filter=null, string exclusion=null,
                Interaction action=Interaction.None, Granularity scope=Granularity.Detail,
                IssueTags warnEscalator=IssueTags.None, IssueTags errEscalator=IssueTags.None)
                : this()
            {
                this._data = new Diags (this);
                Data.Root = root;
                Data.Filter = filter;
                Data.Exclusion = exclusion;
                Data.Response = action;
                Data.Scope = scope;
                Data.WarnEscalator = warnEscalator;
                Data.ErrEscalator = errEscalator;
            }

            protected Model()
            {
                FormatModel = new FileFormat.Vector.Model();
                LoadFormats();
            }

            // Interrogate the assembly for any classes to add to the list of file formats. They:
            // 1. Must be nested in a class that
            //    1a. ends with "Format"
            //    1b. derives from FormatBase
            //    1c. contains the method public static CreateModel (Stream, byte[], string)
            // 2. Must be named "Model" and derive from a FormatBase.ModelBase
            // 4. Must contain the property public static string[] Names { get; }
            // 5. May contain the property public static string Subname { get; }
            private void LoadFormats()
            {
                foreach (var type in Assembly.GetAssembly (typeof (FormatBase)).GetTypes())
                {
                    if (type.IsClass && type.Name.EndsWith ("Format"))
                    {
                        MethodInfo createrInfo = null, namesInfo = null, subnameInfo = null;
                        var modelType = type.GetNestedType ("Model");
                        foreach (var meth in type.GetMethods (BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly))
                        {
                            var ret = meth.ReturnType;
                            if (! meth.IsSpecialName)
                            {
                                if (meth.Name == "CreateModel")
                                {
                                    var parm = meth.GetParameters();
                                    while (ret.BaseType != typeof (System.Object))
                                        ret = ret.BaseType;
                                    if (ret == typeof (FormatBase.Model) && parm.Length==3
                                            && parm[0].ParameterType == typeof (Stream)
                                            && parm[1].ParameterType == typeof (System.Byte[])
                                            && parm[2].ParameterType == typeof (String))
                                        createrInfo = meth;
                                }
                            }
                            else if (meth.Name=="get_Names" && ret == typeof (System.String[]))
                                namesInfo = meth;
                            else if (meth.IsSpecialName && meth.Name=="get_Subname" && ret == typeof (System.String))
                                subnameInfo = meth;
                        }

                        if (namesInfo != null && createrInfo != null)
                        {
                            var names = (string[]) namesInfo.Invoke (null, null);
                            var subname = (string) subnameInfo?.Invoke (null, null);
                            var creater = (FormatModelFactory) Delegate.CreateDelegate (typeof (FormatModelFactory), createrInfo);
                            FormatModel.Add (creater, names, subname);
                        }
                    }
                }

                FormatModel.Sort();
            }

            public IEnumerable<FormatBase.Model> CheckRoot()
            {
                if (String.IsNullOrWhiteSpace (Data.Root))
                    yield break;

                FileAttributes atts;
                try
                { atts = File.GetAttributes (Data.Root); }
                catch (NotSupportedException)
                {
                    Data.Result = Severity.Fatal;
                    throw new ArgumentException ("Directory name is invalid.");
                }

                if ((atts & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    Data.Result = Severity.NoIssue;
                    foreach (FormatBase.Model fmtModel in CheckRootDir())
                        yield return fmtModel;
                }
                else
                {
                    var access = Data.Response != Interaction.None ? FileAccess.ReadWrite : FileAccess.Read;
                    Stream st = new FileStream (Data.Root, FileMode.Open, access, FileShare.Read);
                    var fmtModel = CheckFile (st, Data.Root, out Severity result);
                    Data.Result = result;
                    yield return fmtModel;
                }
            }

            private IEnumerable<FormatBase.Model> CheckRootDir()
            {
                foreach (string dn in new DirTraverser (Data.Root))
                {
                    var dInfo = new DirectoryInfo (dn);
                    FileInfo[] fileInfos = Data.Filter == null ? dInfo.GetFiles() : dInfo.GetFiles (Data.Filter);
                    var flacs = new List<FlacFormat>();
                    var mp3s = new List<Mp3Format>();
                    int logCount = SortDir (fileInfos);

                    foreach (FileInfo fInfo in fileInfos)
                    {
                        FormatBase.Model fmtModel;
                        try
                        {
                            // Many exceptions also caught by outer caller:
                            var access = Data.Response != Interaction.None ? FileAccess.ReadWrite : FileAccess.Read;
                            Stream stream = new FileStream (fInfo.FullName, FileMode.Open, access, FileShare.Read);
                            fmtModel = CheckFile (stream, fInfo.FullName, out Severity badness);

                            if (fmtModel is FlacFormat.Model flacModel)
                                flacs.Add (flacModel.Data);
                            else if (fmtModel is Mp3Format.Model mp3Model)
                                mp3s.Add (mp3Model.Data);
                            else if (fmtModel is LogEacFormat.Model logModel)
                                if (Data.IsFlacRipCheckEnabled || Data.IsMp3RipCheckEnabled)
                                {
                                    if (logCount > 1)
                                        logModel.SetRpIssue ("Folder contains more than one .log file.");
                                    else if (flacs.Count > 0 && mp3s.Count > 0)
                                        logModel.SetRpIssue ("Folder contains both .flac and .mp3 files.");
                                    else if (! Data.IsMp3RipCheckEnabled || Data.IsFlacRipCheckEnabled && flacs.Count > 0)
                                        logModel.ValidateRip (flacs, Data.IsFlacTagsCheckEnabled);
                                    else
                                        logModel.ValidateRip (mp3s);
                                    ReportIssues (logModel.Data.Issues);
                                }
    
                            if (badness > Data.Result)
                                Data.Result = badness;
                        }
                        catch (FileNotFoundException ex)
                        {
                            Data.Result = Severity.Fatal;
                            Data.OnMessageSend (ex.Message.Trim(), Severity.Fatal);
                            Data.OnMessageSend ("Ignored.", Severity.Advisory);
                            continue;
                        }
                        yield return fmtModel;
                    }
                }
            }

            private FormatBase.Model CheckFile (Stream stream, string path, out Severity resultCode)
            {
                SetCurrentFile (Path.GetDirectoryName (path), Path.GetFileName (path));

                bool isKnownExtension;
                FileFormat trueFormat;
                FormatBase.Model fmtModel = null;
                try
                {
                    fmtModel = FormatBase.Model.Create (stream, path, Data.HashFlags, Data.ValidationFlags,
                                                        Data.Filter, out isKnownExtension, out trueFormat);
                }
#pragma warning disable 0168
                catch (Exception ex)
#pragma warning restore 0168
                {
#if DEBUG
                    throw;
#else
                    Data.OnMessageSend ("Exception: " + ex.Message.TrimEnd (null), Severity.Fatal);
                    ++Data.TotalErrors;
                    resultCode = Severity.Fatal;
                    return null;
#endif
                }

                if (! isKnownExtension)
                {
                    if (Data.Scope <= Granularity.Verbose)
                        Data.OnMessageSend ("Ignored.", Severity.Trivia);
                    resultCode = Severity.NoIssue;
                    return fmtModel;
                }

                if (fmtModel == null)
                {
                    if (trueFormat != null)
                        resultCode = Severity.NoIssue;
                    else
                    {
                        if (Data.Scope <= Granularity.Quiet)
                            Data.OnMessageSend ("Unrecognized contents.", Severity.Error);
                        ++Data.TotalErrors;
                        resultCode = Severity.Fatal;
                    }
                    return null;
                }

                ++Data.TotalFiles;
                trueFormat.TrueTotal += 1;

                FormatBase fmt = fmtModel.Data;

                if (fmt.IsBadHeader)
                    ++trueFormat.TotalHeaderErrors;

                if (fmt.IsBadData)
                    ++trueFormat.TotalDataErrors;

                fmtModel.IssueModel.Escalate (Data.WarnEscalator, Data.ErrEscalator);

                ReportFormat (fmt);

                if (! fmt.Issues.HasError)
                {
                    int startRepairableCount = fmt.Issues.RepairableCount;
                    if (startRepairableCount > 0)
                    {
                        ++Data.TotalRepairable;
                        var didRename = RepairFile (fmtModel);
                        if (didRename)
                            --trueFormat.TotalMisnamed;
                        if (fmt.Issues.RepairableCount == 0)
                            --Data.TotalRepairable;
                        Data.TotalRepairs += startRepairableCount - fmt.Issues.RepairableCount;
                    }
                }

                resultCode = fmt.Issues.MaxSeverity;
                return fmtModel;
            }

            private int SortDir (FileInfo[] fileInfos)
            {
                Array.Sort (fileInfos, (f1, f2) => String.CompareOrdinal (f1.Name, f2.Name));

                int logCount = fileInfos.Count (i => i.Name.EndsWith (".log"));
                if (logCount > 0 && (Data.IsFlacRipCheckEnabled || Data.IsMp3RipCheckEnabled))
                {
                    int toSwap = logCount;
                    int dest = fileInfos.Length - 1;
                    for (int ix = dest; ix >= 0; --ix)
                        if (fileInfos[ix].Name.EndsWith (".log"))
                        {
                            if (ix != dest)
                            {
                                FileInfo temp = fileInfos[ix];
                                fileInfos[ix] = fileInfos[dest];
                                fileInfos[dest] = temp;
                            }
                            if (--toSwap <= 0)
                                break;
                            --dest;
                        }
                }
                return logCount;
            }

            public void SetCurrentFile (string baseName)
            {
                Data.CurrentFile = baseName;
                Data.OnFileVisit (Data.CurrentDirectory, baseName);
            }

            public void SetCurrentFile (string directoryName, string fileName)
            {
                Data.CurrentFile = fileName;
                Data.CurrentDirectory = directoryName;
                Data.OnFileVisit (directoryName, fileName);
            }

            public void ResetTotals()
            {
                Data.TotalFiles = 0;
                Data.TotalRepairable = 0;
                Data.TotalErrors = 0;
                Data.TotalWarnings = 0;

                FormatModel.ResetTotals();
            }

            private bool reportHasWarn = false, reportHasErr = false;
            private Granularity reportScope;
            private int reportIssueIndex;

            public void ReportFormat (FormatBase fb)
            {
                reportScope = Data.Scope;
                IList<string> report = fb.GetReportHeader (reportScope);
                if (reportScope == Granularity.Detail)
                {
                    fb.GetReportDetail (report);
                    Data.OnMessageSend (String.Empty, Severity.NoIssue);
                }
                else if (reportScope > Granularity.Terse)
                    if (Data.Response == Interaction.PromptToRepair && fb.Issues.RepairableCount > 0)
                        reportScope = Granularity.Terse;

                foreach (var lx in report)
                    Data.OnMessageSend (lx);

                reportHasWarn = false;
                reportHasErr = false;
                reportIssueIndex = 0;
                ReportIssues (fb.Issues);
            }

            public void ReportIssues (Issue.Vector issues)
            {
                while (reportIssueIndex < issues.Items.Count)
                {
                    Issue item = issues.Items[reportIssueIndex];
                    Severity severity = item.Level;
                    if (severity == Severity.Warning)
                    {
                        if (! reportHasWarn)
                        {
                            reportHasWarn = true;
                            ++Data.TotalWarnings;
                        }
                    }
                    else if (severity >= Severity.Error)
                    {
                        if (! reportHasErr)
                        {
                            reportHasErr = true;
                            ++Data.TotalErrors;
                        }
                    }

                    if (item.IsReportable (reportScope))
                    {
                        if (reportIssueIndex == 0)
                            if (reportScope <= Granularity.Detail)
                            { Data.OnMessageSend (String.Empty); Data.OnMessageSend ("Diagnostics:"); }

                        Data.OnMessageSend (item.Message, severity, item.IsRepairable ? Likeliness.Probable : Likeliness.None);
                    }
                    ++reportIssueIndex;
                }
            }

            public void ReportSummary()
            {
                if (Data.TotalFiles > 1)
                {
                    var rollups = Data.GetRollups (new List<string>(), "diagnosed");

                    Data.OnMessageSend (String.Empty);
                    Data.OnMessageSend ("==== ==== ==== ==== ====");
                    foreach (var lx in rollups)
                        Data.OnMessageSend (lx);
                    Data.OnMessageSend (String.Empty);
                }

                ResetTotals();
            }

            private bool RepairFile (FormatBase.Model formatModel)
            {
                bool result = false;

                if (Data.Response == Interaction.PromptToRepair)
                {
                    for (int ix = 0; ix < formatModel.Data.Issues.Items.Count; ++ix)
                    {
                        Issue issue = formatModel.Data.Issues.Items[ix];
                        if (issue.IsRepairable)
                            for (;;)
                            {
                                Data.OnMessageSend (String.Empty, Severity.NoIssue);
                                bool? isYes = Data.QuestionAsk (issue.RepairPrompt + "? ");
                                if (isYes != true)
                                    break;

                                string errorMessage = formatModel.IssueModel.Repair (ix);
                                if (errorMessage == null)
                                {
                                    Data.OnMessageSend ("Repair successful!", Severity.Advisory);
                                    if (formatModel.IssueModel.RepairerEquals (ix, formatModel.RepairWrongExtension))
                                        result = true;
                                    break;
                                }

                                Data.OnMessageSend ("Repair attempt failed: " + errorMessage, Severity.Warning);
                            }
                        if (issue.IsFinalRepairer)
                            break;
                    }

                    Data.OnMessageSend (String.Empty, Severity.NoIssue);
                    formatModel.CloseFile();
                }

                return result;
            }
        }
    }
}
