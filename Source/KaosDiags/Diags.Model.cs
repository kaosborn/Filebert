using System;
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
                Data.Scope = scope;
                Data.Filter = filter;
                Data.Exclusion = exclusion;
                Data.WarnEscalator = warnEscalator;
                Data.ErrEscalator = errEscalator;
                Data.Response = action;
            }

            protected Model()
            {
                FormatModel = new FileFormat.Vector.Model();
                LoadFormats();
            }

            // Interrogate the assembly for any classes to add to the list of file formats.
            // Acceptable formats:
            // 1. Must be named "Model"
            // 2. Must derive from FormatBase.ModelBase
            // 3. Must contain the property public static string[] SNames { get; }
            // 4. May contain the property public static string SSubname { get; }
            // 5. Must be nested. Outer class:
            //    5a. Must end with "Format"
            //    5b. Must derive from FormatBase
            //    5c. Must contain the method public static CreateModel (Stream, byte[], string)
            private void LoadFormats()
            {
                // Load known formats into the static table via duck typing.
                foreach (var duck in Assembly.GetAssembly (typeof (FormatBase)).GetTypes())
                {
                    if (duck.IsClass && duck.Name.EndsWith ("Format"))
                    {
                        MethodInfo createrInfo = null, namesInfo = null, subnameInfo = null;
                        Type modelType = duck.GetNestedType ("Model");
                        foreach (var meth in duck.GetMethods (BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly))
                        {
                            Type ret = meth.ReturnType;
                            if (! meth.IsSpecialName)
                            {
                                if (meth.Name == "CreateModel")
                                {
                                    ParameterInfo[] parm = meth.GetParameters();
                                    while (ret.BaseType != typeof (System.Object))
                                        ret = ret.BaseType;
                                    if (ret == typeof (FormatBase.Model) && parm.Length==3
                                            && parm[0].ParameterType == typeof (Stream)
                                            && parm[1].ParameterType == typeof (System.Byte[])
                                            && parm[2].ParameterType == typeof (String))
                                        createrInfo = meth;
                                }
                            }
                            else if (meth.Name=="get_SNames" && ret == typeof (System.String[]))
                                namesInfo = meth;
                            else if (meth.IsSpecialName && meth.Name == "get_SSubname" && ret == typeof (System.String))
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

            public IEnumerable<FormatBase.Model> CheckRoot (bool isTwoPass=false)
            {
                if (String.IsNullOrWhiteSpace (Data.Root))
                    yield break;

                Data.ProgressCounter = 0;

                FileAttributes atts;
                try
                { atts = File.GetAttributes (Data.Root); }
                catch (NotSupportedException ex)
                {
                    Data.Result = Severity.Fatal;
                    throw new ArgumentException ("Directory name is invalid: " + ex.Message.Trim(null));
                }

                if ((atts & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    Data.Result = Severity.NoIssue;
                    Data.ProgressTotal = 0;
                    if (isTwoPass)
                        foreach (var dummy in new DirTraverser (Data.Root))
                        {
                            var dInfo = new DirectoryInfo (dummy);
                            FileInfo[] fileInfos = Data.Filter == null ? dInfo.GetFiles() : dInfo.GetFiles (Data.Filter);
                            Data.ProgressTotal += fileInfos.Length;
                        }
                    foreach (FormatBase.Model fmtModel in CheckRootDir())
                    {
                        ++Data.ProgressCounter;
                        yield return fmtModel;
                    }
                }
                else
                {
                    SetCurrentFile (Path.GetDirectoryName (Data.Root), Path.GetFileName (Data.Root));

                    FormatBase.Model fmtModel;
                    try
                    {
                        FileAccess xs = Data.Response != Interaction.None ? FileAccess.ReadWrite : FileAccess.Read;
                        Stream fs = new FileStream (Data.Root, FileMode.Open, xs, FileShare.Read);
                        fmtModel = CheckFile (fs, Data.Root, Data.HashFlags & ~ Hashes._FlacMatch, out FileFormat trueFormat, out Severity result);
                        if (result < Severity.Error)
                            TryRepairs (fmtModel, trueFormat);
                        Data.Result = result;
                    }
                    catch (Exception ex) when (ex is FileNotFoundException || ex is IOException || ex is UnauthorizedAccessException)
                    {
                        Data.Result = Severity.Fatal;
                        Data.OnMessageSend (ex.Message.Trim(), Severity.Fatal);
                        yield break;
                    }
                    yield return fmtModel;
                }
            }

            private IEnumerable<FormatBase.Model> CheckRootDir()
            {
                foreach (string dn in new DirTraverser (Data.Root))
                {
                    var flacs = new List<FlacFormat>();
                    var mp3s = new List<Mp3Format>();
                    var dInfo = new DirectoryInfo (dn);
                    FileInfo[] fileInfos = Data.Filter == null ? dInfo.GetFiles() : dInfo.GetFiles (Data.Filter);
                    int logCount = SortDir (fileInfos);

                    foreach (FileInfo fInfo in fileInfos)
                    {
                        FormatBase.Model fmtModel;
                        try
                        {
                            SetCurrentFile (Path.GetDirectoryName (fInfo.FullName), Path.GetFileName (fInfo.FullName));

                            // Unsetting _FlacMatch will optimize away unused PCM-32 calculations.
                            Hashes hFlags = Data.HashFlags;
                            if ((hFlags & Hashes._FlacMatch) != 0 && logCount == 0)
                                hFlags &= ~ Hashes._FlacMatch;

                            // Many exceptions also caught by outer caller:
                            FileAccess xs = Data.Response != Interaction.None ? FileAccess.ReadWrite : FileAccess.Read;
                            Stream fs = new FileStream (fInfo.FullName, FileMode.Open, xs, FileShare.Read);
                            fmtModel = CheckFile (fs, fInfo.FullName, hFlags, out FileFormat trueFormat, out Severity badness);

                            if (fmtModel != null)
                            {
                                if (fmtModel is FlacFormat.Model flacModel)
                                {
                                    if (logCount == 0)
                                        flacModel.IssueModel.Add ("Found .flac file without .log file in same folder.", Severity.Noise, IssueTags.StrictErr);
                                    flacs.Add (flacModel.Data);
                                }
                                else if (fmtModel is Mp3Format.Model mp3Model)
                                    mp3s.Add (mp3Model.Data);
                                else if (fmtModel is CueFormat.Model cueModel)
                                    cueModel.Validate (flacs);
                                else if (fmtModel is LogFormat.Model logModel)
                                    if (Data.IsFlacRipCheckEnabled || Data.IsMp3RipCheckEnabled)
                                        if (logCount > 1)
                                            logModel.SetRpIssue ("Folder contains more than one .log file.");
                                        else if (flacs.Count > 0 && mp3s.Count > 0)
                                            logModel.SetRpIssue ("Folder contains both .flac and .mp3 files.");
                                        else if (! Data.IsMp3RipCheckEnabled || Data.IsFlacRipCheckEnabled && flacs.Count > 0)
                                            logModel.ValidateRip (flacs, Data.IsFlacTagsCheckEnabled);
                                        else
                                            logModel.ValidateRip (mp3s);

                                ReportIssues (fmtModel.Data.Issues);
                                TryRepairs (fmtModel, trueFormat);
                            }
                            if (Data.Result < badness)
                                Data.Result = badness;
                        }
                        catch (Exception ex) when (ex is FileNotFoundException || ex is IOException || ex is UnauthorizedAccessException)
                        {
                            Data.Result = Severity.Fatal;
                            Data.OnMessageSend (ex.Message.Trim(), Severity.Fatal);
                            Data.OnMessageSend ("Unrecognized.", Severity.Advisory);
                            continue;
                        }
                        yield return fmtModel;
                    }
                }
            }

            private FormatBase.Model CheckFile (Stream stream, string path, Hashes hashFlags, out FileFormat trueFormat, out Severity resultCode)
            {
                FormatBase.Model fmtModel = null;
                try
                {
                    fmtModel = FormatBase.Model.Create (stream, path, hashFlags, Data.ValidationFlags,
                                                        Data.Filter, out bool isKnownExtension, out trueFormat);
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
                    trueFormat = null;
                    resultCode = Severity.Fatal;
                    return null;
#endif
                }


                if (fmtModel == null)
                {
                    if (Data.Scope <= Granularity.Verbose)
                    {
                        if (Data.Scope == Granularity.Verbose && Data.IsDigestForm)
                            Data.OnMessageSend ("; " + Data.CurrentFile, Severity.NoIssue);
                        Data.OnMessageSend ("Unknown extension ignored.", Severity.Trivia);
                    }
                    stream.Dispose();
                    resultCode = Severity.NoIssue;
                    return null;
                }

                FormatBase fmt = fmtModel.Data;

                ++Data.TotalFiles;
                if (trueFormat != null)
                {
                    ++trueFormat.TrueTotal;
                    if (fmt.IsBadHeader)
                        ++trueFormat.TotalHeaderErrors;
                    if (fmt.IsBadData)
                        ++trueFormat.TotalDataErrors;
                }

                fmtModel.IssueModel.Escalate (Data.WarnEscalator, Data.ErrEscalator);
                ReportFormat (fmt);
                resultCode = fmt.Issues.MaxSeverity;
                return fmtModel;
            }

            private void TryRepairs (FormatBase.Model fmtModel, FileFormat trueFormat)
            {
                if (! fmtModel.IssueModel.Data.HasError)
                {
                    int startRepairableCount = fmtModel.IssueModel.Data.RepairableCount;
                    if (startRepairableCount != 0)
                        if (Data.Response == Interaction.PromptToRepair)
                        {
                            ++Data.TotalRepairable;
                            bool didRename = RepairFile (fmtModel);
                            if (didRename)
                            {
                                System.Diagnostics.Debug.Assert (trueFormat != null);
                                if (trueFormat != null)
                                    --trueFormat.TotalMisnamed;
                            }

                            if (fmtModel.IssueModel.Data.RepairableCount == 0)
                                --Data.TotalRepairable;
                            Data.TotalRepairs += startRepairableCount - fmtModel.IssueModel.Data.RepairableCount;
                        }
                        else if (Data.Response == Interaction.RepairLater)
                            // Keep file open.
                            return;
                }

                fmtModel.CloseFile();
            }

            private int SortDir (FileInfo[] fileInfos)
            {
                Array.Sort (fileInfos, (f1, f2) => String.CompareOrdinal (f1.Name, f2.Name));

                // Check folder-level rip files last.
                moveAllToEnd (".cue");
                return moveAllToEnd (".log");

                int moveAllToEnd (string extension)
                {
                    int pivot;
                    for (pivot = fileInfos.Length; pivot > 0; --pivot)
                        if (! fileInfos[pivot - 1].Name.EndsWith (extension))
                            break;

                    for (int ix = 0; ix < pivot; ++ix)
                        if (fileInfos[ix].Name.EndsWith (extension))
                        {
                            FileInfo temp = fileInfos[ix];
                            for (int iy = ix+1; iy < pivot; ++iy)
                                if (fileInfos[iy].Name.EndsWith (extension))
                                {
                                    fileInfos[iy-1] = temp;
                                    temp = fileInfos[iy];
                                }
                                else
                                    fileInfos[iy-1] = fileInfos[iy];
                            --pivot;
                            fileInfos[pivot] = temp;
                        }

                    return fileInfos.Length - pivot;
                }
            }

            private string shownDir=null, shownFile=null;
            public void SetCurrentFile (string directoryName, string fileName)
            {
                if (shownDir != directoryName)
                {
                    shownDir = directoryName;
                    shownFile = fileName;
                    Data.IsDirShown = Data.IsFileShown = false;
                }
                else if (shownFile != fileName)
                {
                    shownFile = fileName;
                    Data.IsFileShown = false;
                }

                Data.CurrentFile = fileName;
                Data.CurrentDirectory = directoryName;
            }

            public void ResetTotals()
            {
                Data.TotalFiles = 0;
                Data.TotalErrors = 0;
                Data.TotalWarnings = 0;
                Data.TotalRepairable = 0;
                FormatModel.ResetTotals();
            }

            private bool reportHasWarn=false, reportHasErr=false;
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

                        Data.OnMessageSend (item.Message, severity);
                    }
                    ++reportIssueIndex;
                }
            }

            public void ReportSummary (string verb)
            {
                SetCurrentFile (null, null);
                IList<string> rollups = Data.GetReportRollups (verb);
                if (Data.TotalFiles > 1)
                {
                    if (Data.ConsoleLinesReported > 0)
                    {
                        Data.OnMessageSend (String.Empty);
                        Data.OnMessageSend ((Data.IsDigestForm ? "; " : String.Empty) + KaosDiags.Diags.MajorSeparator);
                    }

                    foreach (var lx in rollups)
                        Data.OnMessageSend (Data.IsDigestForm ? "; " + lx : lx);
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
