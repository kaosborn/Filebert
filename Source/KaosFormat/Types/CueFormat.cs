using KaosIssue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KaosFormat
{
    // en.wikipedia.org/wiki/Cue_sheet_(computing)#Cue_sheet_syntax
    // digitalx.org/cue-sheet/syntax/index.html
    public class CueFormat : FilesContainer
    {
        public static string[] SNames => new string[] { "cue" };
        public override string[] Names => SNames;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (path.ToLower().EndsWith(".cue"))
                return new Model (stream, path);
            return null;
        }

        public new class Model : FilesContainer.Model
        {
            public new readonly CueFormat Data;

            public Model (Stream stream, string path) : base (path)
            {
                base._data = Data = new CueFormat (this, stream, path);
                SetIgnoredName ("Range.wav");

                if (Data.FileSize > 512 * 1024)
                {
                    IssueModel.Add ("Oversized file", Severity.Fatal);
                    return;
                }

                Data.fBuf = new byte[Data.FileSize];
                Data.fbs.Position = 0;
                if (Data.fbs.Read (Data.fBuf, 0, (int) Data.FileSize) != Data.FileSize)
                {
                    IssueModel.Add ("Read error", Severity.Fatal);
                    return;
                }

                Data.Codepage = Encoding.GetEncoding (1252);

                int fIx=0, fIx1=0, fIx2=0, bIxNS=-1, quoteIx1=-1, quoteIx2=-1;
                for (int line = 1;;)
                {
                    if (fIx < Data.fBuf.Length)
                    {
                        byte ch = Data.fBuf[fIx];
                        ++fIx;
                        if (ch == (byte) '\r')
                            fIx2 = fIx < Data.fBuf.Length && Data.fBuf[fIx] == (byte) '\n' ? fIx+1 : fIx;
                        else if (ch == (byte) '\n')
                            fIx2 = fIx;
                        else
                        {
                            if (ch == '\"')
                            {
                                if (quoteIx1 < 0)
                                    quoteIx1 = fIx;
                                else if (quoteIx2 < 0)
                                    quoteIx2 = fIx-1;
                            }
                            else if (bIxNS < 0 && ch != ' ')
                                bIxNS = fIx-1;
                            continue;
                        }
                    }
                    else
                        fIx2 = fIx;

                    if (ConvertTo.StartsWithAscii (Data.fBuf, bIxNS, "CATALOG "))
                    {
                        Data.Catalog = FormatBase.Cp1252.GetString (Data.fBuf, bIxNS+8, fIx2-bIxNS-8).Trim (null);
                        if (Data.Catalog.Length != 13)
                            IssueModel.Add ("Invalid CATALOG.");
                    }
                    else if (ConvertTo.StartsWithAscii (Data.fBuf, bIxNS, "FILE "))
                    {
                        if (quoteIx2 <= quoteIx1)
                            IssueModel.Add ("Malformed FILE.");
                        else
                        {
                            string quoted = FormatBase.Cp1252.GetString (Data.fBuf, quoteIx1, quoteIx2 - quoteIx1);
                            FilesModel.Add1252 (quoted, quoteIx1, quoteIx2-quoteIx1);
                        }
                    }

                    fIx = fIx1 = bIxNS = fIx2;
                    quoteIx1 = quoteIx2 = -1;
                    ++line;
                    if (fIx >= Data.fBuf.Length)
                        break;
                }
            }

            public void Validate (Hashes hashFlags, IList<FlacFormat> flacs)
            {
                actualFlacs = flacs;

                if (
                    flacs == null || flacs.Count == 0 || flacs.Max (s => s.Issues.MaxSeverity) >= Severity.Error)
                    GetDiagnostics();
                else if (flacs.Count == Data.Files.Items.Count)
                {
                    var m = "Repair bad file reference";
                    if (Data.MissingCount != 1)
                        m += "s";
                    GetDiagnostics (m, RepairMissing, (hashFlags & Hashes._Repairing) != 0);
                }
                else if (Data.Files.Items.Count != 1)
                    Data.FcIssue = IssueModel.Add ($"Folder contains {flacs.Count} .flac file(s) yet .cue contains {Data.Files.Items.Count} file reference(s).",
                                                   Severity.Error, IssueTags.Failure);
            }

            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                base.CalcHashes (hashFlags, validationFlags);

                if ((hashFlags & Hashes._DirCheck) == 0)
                    GetDiagnostics();
            }

            private IList<FlacFormat> actualFlacs = null;
            public string RepairMissing (bool isFinalRepair)
            {
                if (Data.fBuf == null || actualFlacs == null || actualFlacs.Count == 0 || actualFlacs.Count != Data.Files.Items.Count || Data.Issues.MaxSeverity >= Severity.Error)
                    return "Invalid attempt";

                string err = null;

                int buf2Size = Data.fBuf.Length;
                for (int ix = 0; ix < actualFlacs.Count; ++ix)
                    buf2Size += actualFlacs[ix].Name.Length - Data.Files.Items[ix].Name.Length;
                var buf2 = new byte[buf2Size];
                int bufIx0 = Data.Files.Items[0].BufIndex;
                int length = bufIx0;
                Array.Copy (Data.fBuf, buf2, bufIx0);

                int dstIx = 0;
                for (int ix = 0;;)
                {
                    dstIx += length;
                    if (dstIx >= buf2.Length)
                        break;
                    byte[] nameBytes = FormatBase.Cp1252.GetBytes (actualFlacs[ix].Name);
                    Array.Copy (nameBytes, 0, buf2, dstIx, nameBytes.Length);
                    dstIx += nameBytes.Length;

                    string try1252 = FormatBase.Cp1252.GetString (nameBytes);
                    if (err == null && try1252 != actualFlacs[ix].Name)
                        err = "Track file name(s) not Windows-1252 clean.";

                    int srcIx = Data.Files.Items[ix].BufIndex2;
                    ++ix;
                    length = (ix == actualFlacs.Count ? Data.fBuf.Length : Data.Files.Items[ix].BufIndex) - srcIx;
                    Array.Copy (Data.fBuf, srcIx, buf2, dstIx, length);
                }

                if (err == null)
                    try
                    {
                        Data.fbs.Position = bufIx0;
                        Data.fbs.Write (buf2, bufIx0, buf2.Length-bufIx0);
                        if (Data.fbs.Length != buf2.Length)
                            Data.fbs.SetLength (buf2.Length);

                        if (isFinalRepair)
                            CloseFile();

                        for (int ix = 0; ix < Data.Files.Items.Count; ++ix)
                        {
                            FilesModel.SetIsFound (ix, true);
                            FilesModel.SetName (ix, actualFlacs[ix].Name);
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    { err = ex.Message.TrimEnd (null); }

                return err;
            }
        }


        public string Catalog { get; private set; }

        private CueFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }

        public override void GetReportDetail (IList<string> report)
        {
            base.GetReportDetail (report);

            if (! String.IsNullOrEmpty (Catalog))
            {
                if (report.Count != 0)
                    report.Add (String.Empty);
                report.Add ($"Catalog = {Catalog}");
            }
        }
    }
}
