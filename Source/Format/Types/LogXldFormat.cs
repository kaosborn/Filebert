﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using KaosIssue;

namespace KaosFormat
{
    public partial class LogXldFormat : FormatBase
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


        public new class Model : FormatBase.Model
        {
            public new readonly LogXldFormat Data;
            public LogEacTrack.Vector.Model TracksModel;
            private LogBuffer parser;

            public Model (Stream stream, string path)
            {
                TracksModel = new LogEacTrack.Vector.Model();
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
                string lx = parser.ReadLineLTrim();
                Data.XldVersionText = lx.Substring (logXldSig.Length);
                lx = parser.ReadLineLTrim();
                if (parser.EOF)
                    return;

                if (lx.StartsWith ("XLD extraction logfile from "))
                    Data.RipDate = lx.Substring (28);

                lx = parser.ReadLineLTrim();
                if (parser.EOF)
                    return;

                int slashPos = lx.IndexOf ('/');
                if (slashPos < 0)
                {
                    IssueModel.Add ("Missing '<artist> / <album>', " + parser.GetPlace() + ".");
                    return;
                }
                Data.RipArtist = lx.Substring (0, slashPos).Trim();
                Data.RipAlbum = lx.Substring (slashPos + 1).Trim();

                for (;;)
                {
                    if (parser.EOF) break;
                    lx = parser.ReadLineLTrim();
                    if (lx == "-----BEGIN XLD SIGNATURE-----")
                    {
                        lx = parser.ReadLineLTrim();
                        if (! parser.EOF)
                        {
                            Data.storedHash = lx;
                            lx = parser.ReadLineLTrim();
                        }
                    }
                }

                GetDiagnostics();
            }

            private void GetDiagnostics()
            {
                if (Data.storedHash == null)
                    IssueModel.Add ("No signature.", Severity.Trivia, IssueTags.FussyErr);
            }
        }


        private LogXldFormat (Model model, Stream stream, string path) : base (model, stream, path)
         => Tracks = model.TracksModel.Data;

        private static readonly byte[] logXldSig = Encoding.ASCII.GetBytes ("X Lossless Decoder version ");
        public LogEacTrack.Vector Tracks { get; private set; }

        public string XldVersionText { get; private set; }
        public string RipDate { get; private set; }
        public string RipArtist { get; private set; }
        public string RipAlbum { get; private set; }
        public string RipArtistAlbum => RipArtist + " / " + RipAlbum;

        private string storedHash = null;
        public string StoredHash => storedHash;

        public override void GetReportDetail (IList<string> report)
        {
            if (report.Count > 0)
                report.Add (String.Empty);

            report.Add ($"XLD version = {XldVersionText}");
            report.Add ("Signature = " + (StoredHash?? "(missing)"));
            report.Add ($"Rip date = {RipDate}");
            report.Add ($"Rip artist = {RipArtist}");
            report.Add ($"Rip album = {RipAlbum}");
        }
    }
}
