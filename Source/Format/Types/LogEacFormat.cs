using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using KaosIssue;

namespace KaosFormat
{
    public partial class LogEacFormat : FormatBase
    {
        public static string[] Names
         => new string[] { "log" };

        public static string Subname
         => "EAC";

        public override string[] ValidNames
         => Names;

        public override string LongName
         => "log (EAC)";

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (StartsWith (hdr, logEacSig0x) || StartsWith (hdr, logEacSig0y) || StartsWith (hdr, logEacSig1x))
                return new Model (stream, path);
            return null;
        }


        public partial class Model : FormatBase.Model
        {
            public new readonly LogEacFormat Data;
            public LogEacTrack.Vector.Model TracksModel;
            private LogBuffer parser;

            public Model (Stream stream, string path)
            {
                TracksModel = new LogEacTrack.Vector.Model();
                base._data = Data = new LogEacFormat (this, stream, path);

                Data.AccurateRip = null;
                Data.RipDate = String.Empty;

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

                if (got < 2 || Data.fBuf[0] != 0xFF || Data.fBuf[1] != 0xFE)
                    Data.Codepage = Encoding.GetEncoding (1252);
                else
                    Data.Codepage = Encoding.Unicode;

                parser = new LogBuffer (Data.fBuf, Data.Codepage);
                string lx = ParseHeader();

                if (! Data.IsRangeRip)
                {
                    lx = ParseTracks (lx);
                    if (Data.Issues.HasFatal)
                        return;
                }

                while (! parser.EOF && ! lx.Contains ("errors") && ! lx.StartsWith ("==== "))
                    lx = parser.ReadLineLTrim();

                if (lx == "No errors occured" || lx == "No errors occurred")
                    lx = parser.ReadLineLTrim();
                else if (lx == "There were errors")
                {
                    if (Data.Issues.MaxSeverity < Severity.Error)
                        IssueModel.Add ("There were errors.");
                    lx = parser.ReadLineLTrim();
                }
                else
                    IssueModel.Add ("Missing 'errors' line.");

                if (Data.IsRangeRip)
                {
                    if (lx == "AccurateRip summary")
                    {
                        for (;;)
                        {
                            lx = parser.ReadLineLTrim();
                            if (parser.EOF || ! lx.StartsWith ("Track "))
                                break;

                            if (lx.Contains ("ccurately ripped (confidence "))
                            {
                                int arVersion = lx.Contains("AR v2")? 2 : 1;
                                bool isOk = ToInt (lx, 40, out int val);
                                int arConfidence = isOk && val > 0? val : -1;
                                lx = parser.ReadLineLTrim();

                                if (Data.AccurateRipConfidence == null || Data.AccurateRipConfidence.Value > arConfidence)
                                    Data.AccurateRipConfidence = arConfidence;
                                if (Data.AccurateRip == null || Data.AccurateRip.Value > arVersion)
                                    Data.AccurateRip = arVersion;
                            }
                        }
                    }
                }

                if (lx == "All tracks accurately ripped")
                    lx = parser.ReadLineLTrim();

                if (lx.StartsWith ("End of status report"))
                    lx = parser.ReadLineLTrim();

                if (lx.StartsWith ("---- CUETools"))
                    lx = ParseCueTools (lx);

                while (! parser.EOF && ! lx.StartsWith ("==== "))
                    lx = parser.ReadLine();

                if (lx.StartsWith ("==== Log checksum ") && lx.Length >= 82)
                {
                    Data.storedHash = ConvertTo.FromHexStringToBytes (lx, 18, 32);

                    lx = parser.ReadLine();
                    if (! parser.EOF || ! String.IsNullOrEmpty (lx))
                        IssueModel.Add ("Unexpected content at end of file.", Severity.Warning, IssueTags.FussyErr);
                }

                parser = null;
                GetDiagnostics();
            }

            public void SetRpIssue (string err)
             => Data.RpIssue = IssueModel.Add (err, Severity.Error, IssueTags.Failure);

            public void ValidateRip (IList<FlacFormat> flacs, bool checkTags)
            {
                Data.IsLosslessRip = true;
                if (flacs.Count != Data.Tracks.Items.Count || flacs.Count == 0)
                {
                    Data.TkIssue = IssueModel.Add ($"Directory contains {flacs.Count} FLACs, EAC log contains {Data.Tracks.Items.Count} tracks.");
                    return;
                }

                Severity baddest = flacs.Max (tk => tk.Issues.MaxSeverity);
                if (flacs.Count != flacs.Where (tk => tk.ActualAudioBlockCRC16 != null).Count ())
                {
                    IssueModel.Add ("Track CRC checks not performed.", Severity.Warning, IssueTags.FussyErr);
                    if (baddest < IssueModel.Data.MaxSeverity)
                        baddest = IssueModel.Data.MaxSeverity;
                }

                for (int ix = 0; ix < flacs.Count; ++ix)
                    TracksModel.MatchFlac (flacs[ix]);
                if (Data.TkIssue == null && Data.Tracks.Items.Any (t => t.MatchName == null))
                    Data.TkIssue = IssueModel.Add ("CRC-32 against FLAC failed.", Severity.Error, IssueTags.Success);
                else if (checkTags)
                    CheckFlacTags (flacs);

                if (baddest < Data.Issues.MaxSeverity)
                    baddest = Data.Issues.MaxSeverity;
                if (baddest >= Severity.Error)
                    Data.RpIssue = IssueModel.Add ("EAC FLAC rip check failed.", baddest, IssueTags.Failure);
                else if (baddest >= Severity.Warning)
                    Data.RpIssue = IssueModel.Add ("EAC FLAC rip check successful with warnings.", baddest, IssueTags.Success);
                else
                    Data.RpIssue = IssueModel.Add ("EAC FLAC rip check successful!", Severity.Advisory, IssueTags.Success);
            }

            void CheckFlacTags (IList<FlacFormat> flacs)
            {
                int prevTrackNum = -1;
                foreach (FlacFormat flac in flacs)
                {
                    var trackTag = flac.GetTag ("TRACKNUMBER");

                    var integerRegex = new Regex ("^([0-9]+)", RegexOptions.Compiled);
                    MatchCollection reMatches = integerRegex.Matches (trackTag);
                    string trackTagCapture = reMatches.Count == 1 ? reMatches[0].Groups[1].ToString() : trackTag;

                    if (! int.TryParse (trackTagCapture, out int trackNum))
                        IssueModel.Add ($"Invalid TRACKNUMBER '{trackTag}'.");
                    else
                    {
                        if (prevTrackNum >= 0 && trackNum != prevTrackNum + 1)
                            IssueModel.Add ($"Gap in TRACKNUMBER tags near '{trackTag}'.");
                        prevTrackNum = trackNum;
                    }
                }

                bool? isSameAA = FlacFormat.IsFlacTagsAllSame (flacs, "ALBUMARTIST");
                if (isSameAA == false)
                    IssueModel.Add ("Inconsistent ALBUMARTIST tag.");

                if (isSameAA == null)
                {
                    bool? isSameArtist = FlacFormat.IsFlacTagsAllSame (flacs, "ARTIST");
                    if (isSameArtist == false)
                        IssueModel.Add ("Inconsistent ARTIST or missing ALBUMARTIST.", Severity.Warning, IssueTags.BadTag);
                    else if (isSameArtist == null)
                        IssueModel.Add ("Missing ARTIST.", Severity.Warning, IssueTags.BadTag);
                }

                bool? isSameAlbum = FlacFormat.IsFlacTagsAllSame (flacs, "ALBUM");
                if (isSameAlbum == false)
                    IssueModel.Add ("Inconsistent ALBUM tag.");
                else if (isSameAlbum == null)
                    IssueModel.Add ("Missing ALBUM tag.", Severity.Warning, IssueTags.BadTag);

                bool? isSameDate = FlacFormat.IsFlacTagsAllSame (flacs, "DATE");
                if (isSameDate == false)
                    IssueModel.Add ("Inconsistent DATE tag.");
                else if (isSameDate == null)
                    IssueModel.Add ("Missing DATE tag.", Severity.Warning, IssueTags.BadTag);

                if (FlacFormat.IsFlacTagsAllSame (flacs, "ORGANIZATION") == false)
                    IssueModel.Add ("Inconsistent ORGANIZATION tag.");

                if (FlacFormat.IsFlacTagsAllSame (flacs, "BARCODE") == false)
                    IssueModel.Add ("Inconsistent BARCODE tag.");

                if (FlacFormat.IsFlacTagsAllSame (flacs, "CATALOGNUMBER") == false)
                    IssueModel.Add ("Inconsistent BARCODE tag.");

                if (FlacFormat.IsFlacTagsAllSame (flacs, "DISCTOTAL") == false)
                    IssueModel.Add ("Inconsistent DISCTOTAL tag.");
            }

            public void ValidateRip (IList<Mp3Format> mp3s)
            {
                Data.IsLosslessRip = false;
                if (mp3s.Count != Data.Tracks.Items.Count || mp3s.Count == 0)
                    Data.RpIssue = Data.TkIssue = IssueModel.Add ($"Directory contains {mp3s.Count} MP3s, EAC log contains {Data.Tracks.Items.Count} tracks.", Severity.Error, IssueTags.Failure);
                else
                {
                    if (mp3s.Count != mp3s.Where (tk => tk.Lame != null && tk.Lame.ActualDataCrc != null).Count())
                        IssueModel.Add ("Track CRC checks not performed.", Severity.Warning, IssueTags.FussyErr);

                    if (mp3s[0].Lame != null)
                    {
                        string profile = mp3s[0].Lame.Profile;
                        for (int ix = 1; ; ++ix)
                        {
                            if (ix == mp3s.Count)
                            { IssueModel.Add ($"MP3 rip profile is {profile}.", Severity.Advisory); break; }

                            if (mp3s[ix].Lame == null || mp3s[ix].Lame.Profile != profile)
                            {
                                IssueModel.Add ("Inconsistent MP3 encoder settings.", Severity.Warning, IssueTags.FussyErr);
                                break;
                            }
                        }
                    }

                    Severity baddest = mp3s.Max (tk => tk.Issues.MaxSeverity);
                    if (baddest < IssueModel.Data.MaxSeverity)
                        baddest = IssueModel.Data.MaxSeverity;

                    if (baddest >= Severity.Error)
                        Data.RpIssue = IssueModel.Add ("EAC MP3 rip check failed.", baddest, IssueTags.Failure);
                    else if (baddest >= Severity.Warning)
                        Data.RpIssue = IssueModel.Add ("EAC MP3 rip check successful with warnings.", baddest, IssueTags.Success);
                    else
                        Data.RpIssue = IssueModel.Add ($"EAC MP3 rip check okay!", Severity.Advisory, IssueTags.Success);
                }
            }

            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                base.CalcHashes (hashFlags, validationFlags);

                if ((hashFlags & Hashes._WebCheck) != 0)
                    CalcHashWebCheck();
            }

            public void CalcHashWebCheck()
            {
                if (Data.storedHash == null)
                {
                    Severity sev = Data.EacVersionText != null && Data.EacVersionText.StartsWith ("1")? Severity.Warning : Severity.Noise;
                    Data.ShIssue = IssueModel.Add ("EAC log self-hash not present.", sev, IssueTags.FussyErr);
                }
                else
                {
                    string boundary = "---------------------------" + DateTime.Now.Ticks;
                    string header = "Content-Disposition: form-data; name=\"LogFile\"; filename=\""
                                    + "SubmittedByConDiags.log\"\r\nContent-Type: application/octet-stream\r\n\r\n";

                    byte[] bndBuf = Encoding.UTF8.GetBytes ("\r\n--" + boundary + "\r\n");
                    byte[] hdrBuf = Encoding.UTF8.GetBytes (header);
                    byte[] tlrBuf = Encoding.UTF8.GetBytes ("\r\n--" + boundary + "--\r\n");

                    var req = (HttpWebRequest) WebRequest.Create ("http://www.exactaudiocopy.de/log/check.aspx");
                    req.ContentType = "multipart/form-data; boundary=" + boundary;
                    req.Method = "POST";
                    req.KeepAlive = true;
                    req.Credentials = CredentialCache.DefaultCredentials;

                    try
                    {
                        using (var qs = req.GetRequestStream())
                        {
                            qs.Write (bndBuf, 0, bndBuf.Length);
                            qs.Write (hdrBuf, 0, hdrBuf.Length);
                            qs.Write (Data.fBuf, 0, Data.fBuf.Length);
                            qs.Write (tlrBuf, 0, tlrBuf.Length);
                        }

                        using (WebResponse res = req.GetResponse())
                            using (Stream ps = res.GetResponseStream())
                                using (StreamReader rdr = new StreamReader (ps))
                                {
                                    string answer = rdr.ReadLine();
                                    if (answer.Contains ("is fine"))
                                        Data.ShIssue = IssueModel.Add ("EAC log self-hash verify successful.", Severity.Advisory, IssueTags.Success);
                                    else if (answer.Contains ("incorrect"))
                                        Data.ShIssue = IssueModel.Add ("EAC log self-hash mismatch, file has been modified.", Severity.Error, IssueTags.Failure);
                                    else
                                        Data.ShIssue = IssueModel.Add ("EAC log self-hash verify attempt returned unknown result.", Severity.Advisory, IssueTags.FussyErr);
                                }
                    }
                    catch (Exception ex)
                    { Data.ShIssue = IssueModel.Add ("EAC log self-hash verify attempt failed: " + ex.Message.Trim (null), Severity.Warning, IssueTags.FussyErr); }
                }
            }

            private void GetDiagnostics()
            {
                if (String.IsNullOrEmpty (Data.Artist))
                    IssueModel.Add ("Missing artist", Severity.Warning, IssueTags.Substandard);

                if (String.IsNullOrEmpty (Data.Album))
                    IssueModel.Add ("Missing album", Severity.Warning, IssueTags.Substandard);

                if (String.IsNullOrEmpty (Data.Drive))
                    IssueModel.Add ("Missing 'Used drive'.");

                if (String.IsNullOrEmpty (Data.ReadMode))
                    IssueModel.Add ("Missing 'Read mode'.");
                else if (Data.ReadMode != "Secure with NO C2, accurate stream, disable cache"
                      && Data.ReadMode != "Secure with NO C2, accurate stream,  disable cache")
                {
                    if (Data.ReadMode != "Secure")
                        Data.DsIssue = IssueModel.Add ("Nonpreferred drive setting: Read mode: " + Data.ReadMode, Severity.Warning, IssueTags.Substandard);

                    if (Data.AccurateStream == null || Data.AccurateStream != "Yes")
                        Data.DsIssue = IssueModel.Add ("Missing drive setting: 'Utilize accurate stream: Yes'." + Data.AccurateStream, Severity.Warning, IssueTags.FussyErr);

                    if (Data.DefeatCache == null || Data.DefeatCache != "Yes")
                        Data.DsIssue = IssueModel.Add ("Missing drive setting: 'Defeat audio cache: Yes'.", Severity.Warning, IssueTags.FussyErr);

                    if (Data.UseC2 == null || Data.UseC2 != "No")
                        Data.DsIssue = IssueModel.Add ("Missing drive setting: 'Make use of C2 pointers: No'.", Severity.Warning, IssueTags.Substandard);
                }

                if (String.IsNullOrEmpty (Data.ReadOffset))
                    IssueModel.Add ("Missing 'Read offset correction'.", Severity.Trivia, IssueTags.FussyWarn);

                if (Data.FillWithSilence != null && Data.FillWithSilence != "Yes")
                    IssueModel.Add ("Missing 'Fill up missing offset samples with silence: Yes'.", Severity.Trivia, IssueTags.FussyWarn);

                if (Data.Quality != null && Data.Quality != "High")
                    IssueModel.Add ("Missing 'Quality: High'.", Severity.Advisory, IssueTags.Substandard);

                if (Data.TrimSilence == null || Data.TrimSilence != "No")
                    Data.TsIssue = IssueModel.Add ("Missing 'Delete leading and trailing silent blocks: No'.", Severity.Warning, IssueTags.FussyErr);

                if (Data.CalcWithNulls != null && Data.CalcWithNulls != "Yes")
                    IssueModel.Add ("Missing 'Null samples used in CRC calculations: Yes'.");

                if (Data.GapHandling != null)
                    if (Data.GapHandling != "Appended to previous track")
                    {
                        IssueTags gapTag = IssueTags.FussyErr;
                        if (Data.GapHandling != "Not detected, thus appended to previous track")
                            gapTag |= IssueTags.FussyErr;

                        Data.GpIssue = IssueModel.Add ("Gap handling preferred setting is 'Appended to previous track'.", Severity.Advisory, gapTag);
                    }

                if (Data.Id3Tag == "Yes")
                    IssueModel.Add ("Append ID3 tags preferred setting is 'No'.", Severity.NoIssue, IssueTags.FussyErr);

                if (Data.ReadOffset == "0" && Data.Drive.Contains ("not found in database"))
                    IssueModel.Add ("Unknown drive with offset '0'.", Severity.Advisory, IssueTags.FussyErr);

                if (Data.NormalizeTo != null)
                    Data.NzIssue = IssueModel.Add ("Use of normalization considered harmful.", Severity.Warning, IssueTags.FussyErr);

                if (Data.SampleFormat != null && Data.SampleFormat != "44.100 Hz; 16 Bit; Stereo")
                    IssueModel.Add ("Missing 'Sample format: 44.100 Hz; 16 Bit; Stereo'.", Severity.Warning, IssueTags.Substandard);

                if (Data.IsRangeRip)
                    IssueModel.Add ("Range rip detected.", Severity.Advisory, IssueTags.FussyWarn);
                else
                {
                    if (! Data.Tracks.IsNearlyAllPresent())
                        Data.TkIssue = IssueModel.Add ("Gap detected in track numbers.");

                    if (Data.TocTrackCount != null)
                    {
                        int diff = Data.TocTrackCount.Value - Data.Tracks.Items.Count;
                        if (diff != 0)
                        {
                            Severity sev = diff == 1? Severity.Advisory : Severity.Error;
                            IssueModel.Add ("Found " + Data.Tracks.Items.Count + " of " + Data.TocTrackCount.Value + " tracks.", sev);
                        }
                    }
                }

                var tpTag = IssueTags.FussyErr;
                var arTag = IssueTags.None;
                var arSev = Severity.Trivia;
                if (Data.AccurateRipConfidence != null)
                    if (Data.AccurateRipConfidence.Value > 0)
                    {
                        tpTag = IssueTags.None;
                        arTag = IssueTags.Success;
                    }
                    else
                    {
                        arSev = Severity.Advisory;
                        if (Data.AccurateRipConfidence.Value < 0)
                            arTag = IssueTags.Failure;
                    }
                Data.ArIssue = IssueModel.Add ("AccurateRip verification " + Data.AccurateRipLong + ".", arSev, arTag);

                var ctSev = Severity.Trivia;
                var ctTag = IssueTags.None;
                if (Data.CueToolsConfidence == null)
                    ctTag = IssueTags.FussyErr;
                else if (Data.CueToolsConfidence.Value < 0)
                    ctSev = Severity.Error;
                else if (Data.CueToolsConfidence.Value == 0)
                    ctSev = Severity.Advisory;
                else
                {
                    ctTag = IssueTags.Success;
                    tpTag = IssueTags.None;
                }

                Data.CtIssue = IssueModel.Add ("CUETools DB verification " + Data.CueToolsLong + ".", ctSev, ctTag);

                var kt = Data.Tracks.Items.Where (it => it.TestCRC != null).Count();
                if (kt == 0)
                    Data.TpIssue = IssueModel.Add ("Test pass not performed.", Severity.Noise, IssueTags.FussyWarn | tpTag);
                else if (kt < Data.Tracks.Items.Count)
                    Data.TpIssue = IssueModel.Add ("Test pass incomplete.", Severity.Error, IssueTags.Failure);
                else if (Data.Tracks.Items.All (it => it.TestCRC == it.CopyCRC))
                {
                    var sev = tpTag != IssueTags.None? Severity.Advisory : Severity.Trivia;
                    Data.TpIssue = IssueModel.Add ("Test/copy CRC-32s match for all tracks.", sev, IssueTags.Success);
                }

                int k1=0, k2=0, k3=0;
                int r1a=-1, r2a=-1, r3a=-1;
                int r1b=0, r2b=0, r3b=0;
                StringBuilder m1 = new StringBuilder(), m2 = new StringBuilder(), m3 = new StringBuilder();
                foreach (LogEacTrack tk in Data.Tracks.Items)
                {
                    if (! tk.HasOk)
                    {
                        if (r1a < 0) r1a = tk.Number;
                        r1b = tk.Number;
                        ++k1;
                    }
                    else if (r1a >= 0)
                    {
                        if (m1.Length != 0) m1.Append (",");
                        m1.Append (r1a);
                        if (r1b > r1a) { m1.Append ("-"); m1.Append (r1b); }
                        r1a = -1;
                    }
 
                    if (tk.HasOk && ! tk.HasQuality)
                    {
                        if (r2a < 0) r2a = tk.Number;
                        r2b = tk.Number;
                        ++k2;
                    }
                    else if (r2a >= 0)
                    {
                        if (m2.Length != 0) m2.Append (",");
                        m2.Append (r2a);
                        if (r2b > r2a) { m2.Append ("-"); m2.Append (r2b); }
                        r2a = -1;
                    }

                    if (tk.IsBadCRC)
                    {
                        if (r3a < 0) r3a = tk.Number;
                        r3b = tk.Number;
                        ++k3;
                    }
                    else if (r3a >= 0)
                    {
                        if (m3.Length != 0) m3.Append (",");
                        m3.Append (r3a);
                        if (r3b > r3a) { m3.Append ("-"); m3.Append (r3b); }
                        r3a = -1;
                    }
                }

                if (k1 == 0 && k2 == 0 && k3 == 0)
                    return;

                if (r1a >= 0)
                {
                    if (m1.Length != 0) m1.Append (",");
                    m1.Append (r1a);
                    if (r1b > r1a) { m1.Append ("-"); m1.Append (r1b); }
                }
                if (r2a >= 0)
                {
                    if (m2.Length != 0) m2.Append (",");
                    m2.Append (r2a);
                    if (r2b > r2a) { m2.Append ("-"); m2.Append (r2b); }
                }
                if (r3a >= 0)
                {
                    if (m3.Length != 0) m3.Append (",");
                    m3.Append (r3a);
                    if (r3b > r3a) { m3.Append ("-"); m3.Append (r3b); }
                }

                Issue i1=null, i2=null, i3=null;
                if (k1 != 0)
                {
                    m1.Append (" not OK.");
                    i1 = IssueModel.Add ((k1 == 1? "Track " : "Tracks ") + m1);
                }
                if (k2 != 0)
                {
                    m2.Append (" OK but missing quality indicator.");
                    i2 = IssueModel.Add ((k2 == 1? "Track " : "Tracks ") + m2);
                }
                if (k3 != 0)
                {
                    m3.Append (" test/copy CRCs mismatched.");
                    i3 = IssueModel.Add ((k3 == 1? "Track " : "Tracks ") + m3, Severity.Error, IssueTags.Failure);
                    Data.TpIssue = i3;
                }

                for (int trackIndex = 0; trackIndex < TracksModel.Data.Items.Count; ++trackIndex)
                {
                    var tk = TracksModel.Data.Items[trackIndex];

                    if (tk.RipSeverest == null || tk.RipSeverest.Level < Severity.Error)
                        if (! tk.HasOk)
                            TracksModel.SetSeverest (trackIndex, i1);
                        else if (! tk.HasQuality)
                            TracksModel.SetSeverest (trackIndex, i2);
                        else if (tk.IsBadCRC)
                            TracksModel.SetSeverest (trackIndex, i3);
                }
            }
        }


        private static readonly byte[] logEacSig0x = new byte[] { (byte)'E', (byte)'A', (byte)'C', (byte)' ' };
        private static readonly byte[] logEacSig0y = Encoding.ASCII.GetBytes ("Exact Audio Copy V0");
        private static readonly byte[] logEacSig1x = Encoding.Unicode.GetBytes ("\uFEFFExact Audio Copy V");

        public LogEacTrack.Vector Tracks { get; private set; }
        public string EacVersionText { get; private set; }
        public string RipDate { get; private set; }
        public string Artist { get; private set; }
        public string Album { get; private set; }
        public string RipArtistAlbum => Artist + " / " + Album;
        public string Drive { get; private set; }
        public string ReadOffset { get; private set; }
        public string Overread { get; private set; }
        public string Id3Tag { get; private set; }
        public string FillWithSilence { get; private set; }
        public string TrimSilence { get; private set; }
        public string SampleFormat { get; private set; }
        public string CalcWithNulls { get; private set; }
        public string Interface { get; private set; }
        public string GapHandling { get; private set; }
        public string NormalizeTo { get; private set; }
        public string Quality { get; private set; }
        public int? TocTrackCount { get; private set; }
        public bool IsRangeRip { get; private set; }
        public int? AccurateRip { get; private set; }
        public int? AccurateRipConfidence { get; private set; }
        public int? CueToolsConfidence { get; private set; }

        private string accurateStream;
        private string defeatCache;
        private string useC2;
        private string readMode;
        private string readModeLongLazy;

        public string AccurateStream
        {
            get { return accurateStream; }
            private set { accurateStream = value; readModeLongLazy = null; }
        }
        public string DefeatCache
        {
            get { return defeatCache; }
            private set { defeatCache = value; readModeLongLazy = null; }
        }
        public string UseC2
        {
            get { return useC2; }
            private set { useC2 = value; readModeLongLazy = null; }
        }
        public string ReadMode
        {
            get { return readMode; }
            private set { readMode = value; readModeLongLazy = null; }
        }

        public string EacVersionLong => EacVersionText?? "unknown";

        public string AccurateRipLong
        {
            get
            {
                if (AccurateRipConfidence == null) return "not attempted";
                if (AccurateRipConfidence.Value < 0) return "failed";
                if (AccurateRipConfidence.Value == 0) return "data not present";
                return "confidence " + AccurateRipConfidence.Value + " (v" + AccurateRip + ")";
            }
        }

        public string CueToolsLong
        {
            get
            {
                if (CueToolsConfidence == null) return "not attempted";
                if (CueToolsConfidence.Value < 0) return "failed";
                if (CueToolsConfidence.Value == 0) return "data not present";
                return "confidence " + CueToolsConfidence.Value;
            }
        }

        public string ReadModeLong
        {
            get
            {
                if (readModeLongLazy == null)
                    if (AccurateStream == null && DefeatCache == null && UseC2 == null)
                        readModeLongLazy = readMode;
                    else
                        readModeLongLazy = (readMode?? "?") + " ("
                            + "AccurateStream=" + (AccurateStream?? "?")
                            + ", DefeatCache=" + (DefeatCache?? "?")
                            + ", UseC2=" + (UseC2?? "?")
                            + ")";

                return readModeLongLazy;
            }
        }

        private byte[] storedHash;
        public string SelfHashLong
         => storedHash==null? (EacVersionText==null || EacVersionText.StartsWith ("0")? "none" : "missing") : ((storedHash.Length * 8).ToString() + " bits");

        public bool HasRpIssue => RpIssue != null;
        public bool? IsLosslessRip { get; private set; } = null;

        public Issue DsIssue { get; private set; }
        public Issue NzIssue { get; private set; }
        public Issue ShIssue { get; private set; }
        public Issue ArIssue { get; private set; }
        public Issue CtIssue { get; private set; }
        public Issue GpIssue { get; private set; }
        public Issue TkIssue { get; private set; }
        public Issue TpIssue { get; private set; }
        public Issue TsIssue { get; private set; }
        public Issue RpIssue { get; private set; }  // Rip check result.

        public Encoding Codepage { get; private set; }

        private LogEacFormat (Model model, Stream stream, string path) : base (model, stream, path)
         => this.Tracks = model.TracksModel.Data;

        public override bool IsBadData
         => ShIssue != null && ShIssue.Failure;

        public override void GetReportDetail (IList<string> report, Granularity scope)
        {
            if (scope <= Granularity.Detail && report.Count > 0)
                report.Add (String.Empty);

            report.Add ($"EAC version = {EacVersionLong}");

            if (scope > Granularity.Detail)
                return;

            if (storedHash != null)
                report.Add ("EAC stored self-hash = " + ConvertTo.ToHexString (storedHash));

            report.Add ("AccurateRip = " + (AccurateRip == null? "(none)" : AccurateRip.Value.ToString()));
            report.Add ("CUETools confidence = " + (CueToolsConfidence == null? "(none)" : CueToolsConfidence.Value.ToString()));

            report.Add ($"Rip album = {RipArtistAlbum}");
            report.Add ($"Rip date = {RipDate}");

            report.Add ($"Drive = {Drive}");
            report.Add ($"Interface = {Interface}");
            report.Add ($"Read mode = {ReadMode}");
            if (AccurateStream != null) report.Add ($"  Accurate Stream = {AccurateStream}");
            if (DefeatCache != null) report.Add ($"  Defeat cache = {DefeatCache}");
            if (UseC2 != null) report.Add ($"  Use C2 info = {UseC2}");

            if (ReadOffset != null) report.Add ($"Drive offset = {ReadOffset}");
            if (Overread != null) report.Add ($"Overread = {Overread}");
            if (FillWithSilence != null) report.Add ($"Fill with silence = {FillWithSilence}");
            if (TrimSilence != null) report.Add ($"Trim silence = {TrimSilence}");
            if (CalcWithNulls != null) report.Add ($"Use nulls in CRC = {CalcWithNulls}");
            if (Quality != null) report.Add ($"Error recovery quality = {Quality}");
            if (NormalizeTo != null) report.Add ($"Normalization = {NormalizeTo}");

            if (GapHandling != null) report.Add ($"Gap handling = " + GapHandling);
            if (SampleFormat != null) report.Add ($"Sample format = " + SampleFormat);

            report.Add ("Track count (ToC) = " + (TocTrackCount==null? "(none)" : TocTrackCount.ToString()));
            report.Add ("Track count (rip) = " + Tracks.Items.Count);
            if (IsRangeRip)
                report.Add ("Range rip = true");
            else if (scope <= Granularity.Detail)
            {
                var sb = new StringBuilder();
                report.Add (String.Empty);
                report.Add ("Tracks:");
                foreach (var tk in Tracks.Items)
                {
                    sb.Clear();
                    sb.AppendFormat ("{0,3}", tk.Number);
                    sb.Append (": ");
                    sb.Append (tk.FilePath);
                    if (! String.IsNullOrEmpty (tk.Qual))
                    { sb.Append (" | "); sb.Append (tk.Qual); }
                    if (tk.CopyCRC != null)
                        sb.AppendFormat (" | {0:X8}", tk.CopyCRC);
                    if (! tk.HasOk)
                        sb.Append (" *BAD*");
                    report.Add (sb.ToString());
                }
            }
        }
    }
}
