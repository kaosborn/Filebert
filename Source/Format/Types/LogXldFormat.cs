using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using KaosIssue;

namespace KaosFormat
{
    public class LogXldFormat : LogFormat
    {
        public static string[] Names
         => new string[] { "log" };

        public static string Subname
         => "XLD";

        public override string[] ValidNames
         => Names;

        public override string LongName
         => "log (XLD)";

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (StartsWith (hdr, logXldSig))
                return new Model (stream, path);
            return null;
        }


        public new class Model : LogFormat.Model
        {
            public new readonly LogXldFormat Data;
            public new LogXldTrack.Vector.Model TracksModel => (LogXldTrack.Vector.Model) _tracksModel;

            public Model (Stream stream, string path)
            {
                base._tracksModel = new LogXldTrack.Vector.Model();
                base._data = Data = new LogXldFormat (this, stream, path);

                // Arbitrary limit.
                if (Data.FileSize > 250000)
                {
                    IssueModel.Add ("File insanely huge.", Severity.Fatal);
                    return;
                }

                Data.fBuf = new byte[Data.FileSize];
                Data.fbs.Position = 0;
                var got = Data.fbs.Read (Data.fBuf, 0, (int) Data.FileSize);
                if (got != Data.FileSize)
                {
                    IssueModel.Add ("Read failed", Severity.Fatal);
                    return;
                }

                Parse (new LogBuffer (Data.fBuf, Encoding.ASCII));
                GetDiagnostics();
            }

            private void Parse (LogBuffer parser)
            {
                List<int> arItems = null;
                int arV2 = 0;
                string lx = parser.ReadLineLTrim();
                if (lx.StartsWith ("X Lossless Decoder"))
                    Data.Application = lx;

                lx = parser.ReadLineLTrim();
                if (lx.StartsWith ("XLD extraction logfile from "))
                    Data.RipDate = lx.Substring (28);

                lx = parser.ReadLineNonempty();
                int slashPos = lx.IndexOf (" / ");
                if (slashPos < 0)
                {
                    IssueModel.Add ("Missing '<artist> / <album>', " + parser.GetPlace() + ".");
                    return;
                }
                Data.RipArtist = lx.Substring (0, slashPos);
                Data.RipAlbum = lx.Substring (slashPos + 3);

                for (;;)
                {
                    lx = parser.ReadLineNonempty();
                    if (parser.EOF) return;

                    string px = ParseArg (lx, out string arg);
                    if (arg == null)
                        break;
                    if (px == "Used drive")
                        Data.Drive = arg;
                    else if (px == "Media type")
                        Data.Media = arg;
                    else if (px == "Ripper mode")
                        Data.ReadMode = arg;
                    else if (px == "Disable audio cache")
                        Data.DefeatCache = arg;
                    else if (px == "Make use of C2 pointers")
                        Data.UseC2 = arg;
                    else if (px == "Read offset correction")
                        Data.ReadOffset = arg;
                    else if (px == "Gap status")
                        Data.GapHandling = arg;
                }

                for (;;)
                {
                    if (parser.EOF || lx == null) return;
                    if (lx.StartsWith ("TOC"))
                        ParseToC();
                    if (lx.StartsWith ("AccurateRip Summary"))
                        ParseAccurateRip();
                    else if (lx.StartsWith ("Track "))
                        ParseTracks();
                    else if (lx == "No errors occurred")
                    {
                        Data.HasNoErrors = true;
                        lx = parser.ReadLine();
                        break;
                    }
                    else
                        lx = parser.ReadLine();
                }

                for (;;)
                {
                    if (parser.EOF)
                        break;
                    if (lx == "-----BEGIN XLD SIGNATURE-----")
                    {
                        lx = parser.ReadLineLTrim();
                        if (! parser.EOF)
                        {
                            Data.storedHash = lx;
                            lx = parser.ReadLineLTrim();
                        }
                    }
                    lx = parser.ReadLineLTrim();
                }

                if (arItems != null)
                    if (arItems.Count != TracksModel.Data.Items.Count || arItems.Count == 0)
                        Data.ArIssue = IssueModel.Add ("AccurateRip table mismatch.", Severity.Error, IssueTags.Failure);
                    else
                    {
                        Data.AccurateRipConfidence = arItems.Min();
                        Data.AccurateRip = arV2 == arItems.Count ? 2 : 1;
                    }
                return;

                void ParseToC()
                {
                    // Q: does the ToC include data tracks?
                    lx = parser.ReadLine();
                }

                void ParseAccurateRip()
                {
                    lx = parser.ReadLine();
                    if (lx != null && lx.Contains ("Disc not found"))
                    { Data.AccurateRipConfidence = 0; return; }

                    arItems = new List<int>();
                    for (;;)
                    {
                        if (String.IsNullOrWhiteSpace (lx) || lx.StartsWith ("->"))
                            return;
                        string px = ParseArg (lx, out string arg);
                        if (px.Length != 8 || ! Char.IsDigit (px[6]) || ! Char.IsDigit (px[7]))
                            return;
                        int.TryParse (px.Substring (6), out int tn);
                        if (tn != arItems.Count + 1)
                            return;
                        if (arg == "Not Found")
                            arItems.Add (0);
                        else if (arg.Contains ("OK "))
                        {
                            int p1 = arg.IndexOf ("confidence ");
                            if (p1 < 0) return; // malformed AR
                            int p2 = p1 = p1 + 11;
                            for (; p2 < arg.Length; ++p2)
                                if (! Char.IsDigit (arg[p2])) break;
                            if (p2 == p1)
                                return; // malformed AR
                            int.TryParse (arg.Substring (p1, p2-p1), out int val);
                            arItems.Add (val);
                            if (arg.Contains ("v2"))
                                ++arV2;
                        }
                        else
                            arItems.Add (-1);
                        lx = parser.ReadLine();
                    }
                }

                void ParseTracks()
                {
                    for (;;)
                    {
                        int number = 0;
                        string fileName = null;
                        uint? testCRC = null, copyCRC = null;
                        string arg = lx.Substring (6);
                        bool isOk = arg.Length >= 2 && Char.IsDigit (arg[0]);
                        if (isOk)
                            isOk = int.TryParse (arg, out number);
                        if (! isOk)
                        { IssueModel.Add ($"Malformed track number '{arg}'."); return; }

                        for (;;)
                        {
                            lx = parser.ReadLine();
                            if (parser.EOF || (lx.Length > 0 && lx[0] != ' '))
                            {
                                TracksModel.Add (number, fileName, testCRC, copyCRC);
                                if (lx.StartsWith ("Track "))
                                    break;
                                else
                                    return;
                            }
                            string px = ParseArg (lx, out string ax);
                            if (px == "Filename")
                                fileName = px;
                            else if (px == "CRC32 hash (test run)")
                            {
                                if (uint.TryParse (ax, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint word))
                                    testCRC = word;
                                else
                                { IssueModel.Add ("Malformed test CRC"); return; }
                            }
                            else if (px == "CRC32 hash")
                            {
                                if (uint.TryParse (ax, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint word))
                                    copyCRC = word;
                                else
                                { IssueModel.Add ("Malformed copy CRC"); return; }
                            }
                        }
                    }
                }
            }

            private static string ParseArg (string mx, out string arg)
            {
                arg = null;
                int cIx;                 // Index of ':'
                int pIx0 = 0, pIx1 = 1;  // Param first, last chars.

                while (pIx0 < mx.Length && mx[pIx0] == ' ')
                    ++pIx0;

                for (cIx = pIx0;;)
                {
                    if (++cIx >= mx.Length)
                        return pIx0 == 0 ? mx : mx.Substring (pIx0);
                    if (mx[cIx] != ' ')
                        if (mx[cIx] != ':')
                            pIx1 = cIx;
                        else
                            break;
                }

                for (int ix = cIx; ++ix < mx.Length; )
                    if (mx[ix] != ' ')
                    { arg = mx.Substring (ix).Trim (null); break; }

                return mx.Substring (pIx0, pIx1 - pIx0 + 1);
            }

            protected override void GetDiagnostics()
            {
                base.GetDiagnostics();

                Severity sev;
                IssueTags tag;
                if (Data.ArIssue == null)
                {
                    if (Data.AccurateRipConfidence < 0)
                    { sev = Severity.Error; tag = IssueTags.Failure; }
                    else
                    { sev = Severity.Advisory; tag = Data.AccurateRipConfidence > 0 ? IssueTags.Success : IssueTags.None; }
                    Data.ArIssue = IssueModel.Add ($"AccurateRip {Data.AccurateRipLong}.", sev, tag);
                }

                if (Data.storedHash == null)
                    IssueModel.Add ("No signature.", Severity.Trivia, IssueTags.StrictErr);
            }
        }


        private static readonly byte[] logXldSig = Encoding.ASCII.GetBytes ("X Lossless Decoder version ");
        public LogXldTrack.Vector Tracks { get; private set; }

        public bool HasNoErrors { get; private set; } = false;
        public string Media { get; private set; }
        public string ReadMode { get; private set; }
        public string DefeatCache { get; private set; }
        public string UseC2 { get; private set; }

        private string storedHash = null;
        public string StoredHash => storedHash;
        public string DerivedHash => storedHash == null ? String.Empty : storedHash.Length.ToString() + " bytes";

        private LogXldFormat (Model model, Stream stream, string path) : base (model, stream, path)
         => Tracks = model.TracksModel.Data;

        public override void GetReportDetail (IList<string> report)
        {
            if (report.Count > 0)
                report.Add (String.Empty);

            report.Add ($"Application = {Application}");
            report.Add ($"Rip date = {RipDate}");
            report.Add ($"Rip artist = {RipArtist}");
            report.Add ($"Rip album = {RipAlbum}");
            report.Add ($"Drive = {Drive}");
            if (ReadOffset != null) report.Add ($"Read offset = {ReadOffset}");
            if (ReadMode != null) report.Add ($"Ripper mode = {ReadMode}");
            if (DefeatCache != null) report.Add ($"Disable cache = {DefeatCache}");
            if (UseC2 != null) report.Add ($"Use C2 = {UseC2}");
            if (GapHandling != null) report.Add ($"Gap handling = {GapHandling}");
            report.Add ($"AccurateRip = {AccurateRipLong}");
            if (StoredHash != null) report.Add ($"Signature = {DerivedHash}");

            report.Add (String.Empty);
            report.Add ("   Copy CRC Test CRC");
            foreach (LogTrack tk in Tracks.Items)
                report.Add ($"{tk.Number:00} {tk.CopyCRC:X8} {tk.TestCRC:X8}");
        }
    }
}
