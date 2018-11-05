using System;
using System.Collections.Generic;
using System.Globalization;
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
            private LogBuffer parser;

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

                parser = new LogBuffer (Data.fBuf, Encoding.ASCII);

                string lx = Parse();
                if (parser.EOF) return;

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

                GetDiagnostics();
            }

            private string Parse()
            {
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
                    IssueModel.Add ("Missing '<artist> / <album>', " + parser.GetPlace () + ".");
                    return null;
                }
                Data.RipArtist = lx.Substring (0, slashPos);
                Data.RipAlbum = lx.Substring (slashPos + 3);

                for (;;)
                {
                    if (parser.EOF) return null;
                    if (lx.StartsWith ("Track "))
                        break;

                    string px = ParseArg (lx, out string arg);
                    if (px == "Used drive")
                        Data.Drive = arg;
                    else if (px == "Ripper mode")
                        Data.ReadMode = arg;
                    else if (px == "Make use of C2 pointers")
                        Data.UseC2 = arg;
                    else if (px == "Read offset correction")
                        Data.ReadOffset = arg;
                    else if (px == "Gap status")
                        Data.GapHandling = arg;

                    lx = parser.ReadLineNonempty();
                }

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
                    { IssueModel.Add ($"Malformed track number '{arg}'."); return null; }

                    for (;;)
                    {
                        lx = parser.ReadLine();
                        if (parser.EOF || (lx.Length > 0 && lx[0] != ' '))
                        {
                            TracksModel.Add (number, fileName, testCRC, copyCRC);
                            if (lx.StartsWith ("Track "))
                                break;
                            else
                                return lx;
                        }
                        string px = ParseArg (lx, out string ax);
                        if (px == "Filename")
                            fileName = px;
                        else if (px == "CRC32 hash (test run)")
                        {
                            if (uint.TryParse (ax, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint word))
                                testCRC = word;
                            else
                            { IssueModel.Add ("Malformed test CRC"); return null; }
                        }
                        else if (px == "CRC32 hash")
                        {
                            if (uint.TryParse (ax, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint word))
                                copyCRC = word;
                            else
                            { IssueModel.Add ("Malformed copy CRC"); return null; }
                        }
                    }
                }
            }

            private string ParseArg (string mx, out string arg)
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

            private void GetDiagnostics()
            {
                GetBaseDiagnostics();

                if (Data.storedHash == null)
                    IssueModel.Add ("No signature.", Severity.Trivia, IssueTags.StrictErr);
            }
        }


        private static readonly byte[] logXldSig = Encoding.ASCII.GetBytes ("X Lossless Decoder version ");
        public LogXldTrack.Vector Tracks { get; private set; }

        private string storedHash = null;
        public string StoredHash => storedHash;

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
            if (UseC2 != null) report.Add ($"Use C2 = {UseC2}");
            if (GapHandling != null) report.Add ($"Gap handling = {GapHandling}");
            report.Add ("Signature = " + (StoredHash?? "(missing)"));

            report.Add (String.Empty);
            report.Add ("Tracks:");
            for (int ix = 0; ix < Tracks.Items.Count; ++ix)
            {
                var tk = Tracks.Items[ix];
                report.Add ($"  {ix}: {tk.Number:00} {tk.CopyCRC:X8}");
            }
        }
    }
}
