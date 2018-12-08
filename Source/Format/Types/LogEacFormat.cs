using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using KaosIssue;

namespace KaosFormat
{
    public class LogEacFormat : LogFormat
    {
        public static string[] Names => new string[] { "log" };
        public static string SSubname => "EAC";
        public override string[] ValidNames => Names;
        public override string Subname => "EAC";

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (StartsWith (hdr, logEacSig0x) || StartsWith (hdr, logEacSig0y) || StartsWith (hdr, logEacSig1x))
                return new Model (stream, path);
            return null;
        }


        public new class Model : LogFormat.Model
        {
            public new readonly LogEacFormat Data;
            public new LogEacTrack.Vector.Model TracksModel;

            public Model (Stream stream, string path)
            {
                base._tracksModel = TracksModel = new LogEacTrack.Vector.Model();
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

                Parse (new LogBuffer (Data.fBuf, Data.Codepage));
                GetDiagnostics();
            }

            string Parse (LogBuffer parser)
            {
                string lx = null;
                ParseHeader();

                if (! Data.IsRangeRip)
                {
                    lx = ParseTracks();
                    if (Data.Issues.HasFatal)
                        return null;
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
                                int arVersion = lx.Contains ("AR v2") ? 2 : 1;
                                int advanced = ConvertTo.FromStringToInt32 (lx, 40, out int val);
                                int arConfidence = advanced > 0 && val > 0 ? val : -1;
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
                    lx = ParseCueTools();

                while (! parser.EOF && ! lx.StartsWith ("==== "))
                    lx = parser.ReadLine();

                if (lx.StartsWith ("==== Log checksum ") && lx.Length >= 82)
                {
                    Data.storedHash = ConvertTo.FromHexStringToBytes (lx, 18, 32);

                    lx = parser.ReadLine();
                    if (! parser.EOF || ! String.IsNullOrEmpty (lx))
                        IssueModel.Add ("Unexpected content at end of file.", Severity.Warning, IssueTags.StrictErr);
                }

                return lx;

                string ParseHeader()
                {
                    lx = parser.ReadLine();
                    if (lx.StartsWith ("Exact Audio Copy V"))
                    {
                        var spacePos = lx.IndexOf (' ', 18);
                        if (spacePos > 0)
                            Data.EacVersionString = lx.Substring (18, spacePos - 18);

                        lx = parser.ReadLine();
                        lx = parser.ReadLine();
                    }

                    if (! lx.StartsWith ("EAC extraction logfile from "))
                    {
                        IssueModel.Add ("Unexpected contents, " + parser.GetPlace() + ": Expecting 'EAC extraction logfile'.");
                        return lx;
                    }

                    lx = lx.Substring (28);
                    if (lx.Length < 12)
                    {
                        IssueModel.Add ("Missing rip date, " + parser.GetPlace() + ".");
                        return lx;
                    }
                    Data.RipDate = lx.EndsWith (" for CD") ? lx.Substring (0, lx.Length - 7) : lx;

                    lx = parser.ReadLine();
                    if (String.IsNullOrWhiteSpace (lx))
                        lx = parser.ReadLine();

                    int slashPos = lx.IndexOf ('/');
                    if (slashPos < 0)
                    {
                        IssueModel.Add ("Missing '<artist> / <album>', " + parser.GetPlace() + ".");
                        return lx;
                    }
                    Data.RipArtist = lx.Substring (0, slashPos).Trim();
                    Data.RipAlbum = lx.Substring (slashPos + 1).Trim();

                    for (;;)
                    {
                        TOP:
                        if (parser.EOF)
                        {
                            lx = String.Empty;
                            return lx;
                        }

                        lx = parser.ReadLine();
                        if (lx.StartsWith ("Track ") || lx.StartsWith ("===="))
                            return lx;

                        if (lx == "Range status and errors")
                        {
                            Data.IsRangeRip = true;
                            lx = parser.ReadLine();
                            return lx;
                        }

                        if (lx == "TOC of the extracted CD")
                            for (Data.TocTrackCount = 0;;)
                            {
                                lx = parser.ReadLine();
                                if (parser.EOF || lx.StartsWith ("==== ") || (lx.StartsWith ("Track") && ! lx.Contains ("|")))
                                    return lx;

                                if (lx == "Range status and errors")
                                {
                                    Data.IsRangeRip = true;
                                    lx = parser.ReadLine();
                                    return lx;
                                }

                                if (lx.Length >= 60)
                                {
                                    if (int.TryParse (lx.Substring (0, 9), out int tn))
                                        ++Data.TocTrackCount;
                                }
                            }

                        int ik0 = 0, ik1=0,  ii=0, iv0=0, iv1;
                        for (;; ++ik0)
                            if (ik0 >= lx.Length) goto TOP;
                            else if (lx[ik0] != ' ')
                            {
                                for (ii = ik0, ik1 = ii;;)
                                {
                                    ++ii;
                                    if (ii >= lx.Length)
                                    {
                                        string kk = lx.Substring (ik0, ik1-ik0+1);
                                        if (kk == "Installed external ASPI interface" || kk == "Native Win32 interface for Win NT & 2000")
                                            Data.Interface = kk;
                                        goto TOP;
                                    }
                                    if (lx[ii] == ':') break;
                                    if (lx[ii] != ' ') ik1 = ii;
                                }

                                for (iv0 = ii+1;; ++iv0)
                                    if (iv0 >= lx.Length) goto TOP;
                                    else if (lx[iv0] != ' ')
                                    {
                                        for (ii=iv0+1, iv1=iv0;; ++ii)
                                            if (ii == lx.Length) break;
                                            else if (lx[ii] != ' ') iv1=ii;

                                        string optKey = lx.Substring (ik0, ik1-ik0+1),
                                               optVal = lx.Substring (iv0, iv1-iv0+1);
                                        if (optKey == "Used drive")
                                            Data.Drive = optVal;
                                        else if (optKey == "Utilize accurate stream")
                                            Data.AccurateStream = optVal;
                                        else if (optKey == "Defeat audio cache")
                                            Data.DefeatCache = optVal;
                                        else if (optKey == "Make use of C2 pointers")
                                            Data.UseC2 = optVal;
                                        else if (optKey == "Read mode")
                                            Data.ReadMode = optVal;
                                        else if (optKey == "Read offset correction" || optKey == "Combined read/write offset correction")
                                            Data.ReadOffset = optVal;
                                        else if (optKey == "Overread into Lead-In and Lead-Out")
                                            Data.Overread = optVal;
                                        else if (optKey == "Fill up missing offset samples with silence")
                                            Data.FillWithSilence = optVal;
                                        else if (optKey == "Delete leading and trailing silent blocks")
                                            Data.TrimSilence = optVal;
                                        else if (optKey == "Null samples used in CRC calculations")
                                            Data.CalcWithNulls = optVal;
                                        else if (optKey == "Normalize to")
                                            Data.NormalizeTo = optVal;
                                        else if (optKey == "Used interface")
                                            Data.Interface = optVal;
                                        else if (optKey == "Gap handling")
                                            Data.GapHandling = optVal;
                                        else if (optKey == "Sample format")
                                            Data.SampleFormat = optVal;
                                        else if (optKey == "Quality")
                                            Data.Quality = optVal;
                                        else if (optKey == "Add ID3 tag")
                                            Data.Id3Tag = optVal;
                                        break;
                                    }
                            }
                    }
                }

                string ParseTracks()
                {
                    for (;;)
                    {
                        if (parser.EOF
                                || lx == "No errors occured"
                                || lx == "No errors occurred"
                                || lx == "There were errors"
                                || lx.Contains ("accurate"))
                            break;

                        if (! lx.StartsWith ("Track") || lx.StartsWith ("Track quality"))
                        {
                            lx = parser.ReadLineLTrim();
                            continue;
                        }

                        bool success = Int32.TryParse (lx.Substring (6), out int num);
                        if (! success)
                        {
                            Data.TkIssue = IssueModel.Add ("Invalid track " + parser.GetPlace(), Severity.Fatal, IssueTags.Failure);
                            break;
                        }

                        lx = parser.ReadLineLTrim();
                        if (! lx.StartsWith ("Filename "))
                            Data.TkIssue = IssueModel.Add ($"Track {num}: Missing 'Filename'.", Severity.Error, IssueTags.Failure);
                        else
                            lx = ParseTrack (num);
                    }
                    return lx;
                }

                string ParseTrack (int num)
                {
                    string name="", peak="", speed="", pregap="", qual="";
                    int? arVersion=null;
                    int? arConfidence=null;
                    uint? testCrc=null, copyCrc=null;
                    bool trackErr = false;
                    uint word;

                    name = lx.Substring (9);
                    if (name.Length < 10 || (lx.Length < 252 && ! lx.EndsWith (".wav")))
                        IssueModel.Add ("Unexpected extension " + parser.GetPlace());

                    lx = parser.ReadLineLTrim();
                    if (lx.StartsWith ("Pre-gap length  "))
                    { pregap = lx.Substring (16); lx = parser.ReadLineLTrim(); }

                    for (;;)
                    {
                        if (parser.EOF)
                            return lx;

                        if (lx.StartsWith ("Track quality") || lx.StartsWith ("Peak level "))
                            break;

                        if (lx.StartsWith ("Track "))
                            return lx;  // Unexpected start of next track.

                        if (! trackErr)
                        {
                            trackErr = true;
                            IssueModel.Add ($"Track {num}: '{lx}'.");
                        }
                        lx = parser.ReadLineLTrim();
                    }

                    if (lx.StartsWith ("Peak level "))
                    { peak = lx.Substring (11); lx = parser.ReadLineLTrim(); }

                    if (lx.StartsWith ("Extraction speed "))
                    { speed = lx.Substring (17); lx = parser.ReadLineLTrim(); }

                    if (lx.StartsWith ("Track quality "))
                    { qual = lx.Substring (14); lx = parser.ReadLineLTrim(); }

                    if (lx.StartsWith ("Test CRC "))
                    {
                        if (uint.TryParse (lx.Substring (9), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out word))
                            testCrc = word;
                        else
                            Data.TpIssue = IssueModel.Add ($"Track {num}: Invalid test CRC-32.", Severity.Error, IssueTags.Failure);
                        lx = parser.ReadLineLTrim();
                    }

                    if (! lx.StartsWith ("Copy CRC "))
                        IssueModel.Add ($"Track {num}: Missing copy CRC-32.", Severity.Warning);
                    else
                    {
                        if (uint.TryParse (lx.Substring (9), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out word))
                            copyCrc = word;
                        else
                            Data.TkIssue = IssueModel.Add ($"Track {num}: Invalid copy CRC-32.", Severity.Error, IssueTags.Failure);
                        lx = parser.ReadLineLTrim();
                    }

                    if (lx.StartsWith ("Accurately ripped (confidence "))
                    {
                        arVersion = lx.Contains ("AR v2") ? 2 : 1;
                        int advanced = ConvertTo.FromStringToInt32 (lx, 30, out int val);
                        arConfidence = advanced > 0 && val > 0 ? val : -1;
                        lx = parser.ReadLineLTrim();
                    }
                    else if (lx.StartsWith ("Cannot be verified"))
                    {
                        arVersion = lx.Contains ("AR v2") ? 2 : 1;
                        arConfidence = -1;
                        lx = parser.ReadLineLTrim();
                    }
                    else if (lx.StartsWith ("Track not present"))
                    {
                        if (peak != "0.0 %")
                            arConfidence = 0;
                        lx = parser.ReadLineLTrim();
                    }

                    if (arConfidence != null)
                    {
                        if (Data.AccurateRipConfidence == null || Data.AccurateRipConfidence > arConfidence)
                            Data.AccurateRipConfidence = arConfidence;
                        if (arVersion != null)
                            if (Data.AccurateRip == null || Data.AccurateRip.Value > arVersion.Value)
                                Data.AccurateRip = arVersion;
                    }

                    bool hasOK = false;
                    if (lx == "Copy OK")
                    {
                        hasOK = true;
                        lx = parser.ReadLineLTrim();
                    }

                    TracksModel.Add (num, name, pregap, peak, speed, qual, testCrc, copyCrc, hasOK, arVersion, arConfidence);
                    return lx;
                }

                string ParseCueTools()
                {
                    do
                    {
                        lx = parser.ReadLineLTrim();
                        if ( parser.EOF || lx.StartsWith ("==== "))
                            return lx;
                    } while (! lx.StartsWith ("[CTDB"));

                    if (lx.Contains ("not present"))
                    {
                        Data.CueToolsConfidence = 0;
                        lx = parser.ReadLineLTrim();
                        if (! parser.EOF && lx.StartsWith ("Submit"))
                            lx = parser.ReadLineLTrim();
                        return lx;
                    }

                    if (! lx.Contains (" found"))
                        return lx;

                    do
                    {
                        lx = parser.ReadLineLTrim();
                        if (parser.EOF || lx.StartsWith ("==== "))
                            return lx;

                        if (lx.StartsWith ("["))
                        {
                            int ctConfidence = -1;
                            if (lx.Contains ("Accurately ripped"))
                            {
                                int advanced = ConvertTo.FromStringToInt32 (lx, 12, out ctConfidence);
                            }
                            Data.CueToolsConfidence = ctConfidence;
                            lx = parser.ReadLineLTrim();
                            return lx;
                        }
                    } while (! lx.StartsWith ("Track"));

                    for (int tx = 0; ; ++tx)
                    {
                        lx = parser.ReadLine();
                        if (parser.EOF || lx.StartsWith ("==== ") || lx.StartsWith ("All tracks"))
                            return lx;

                        int ctConfidence;
                        if (lx.Contains ("Accurately ripped"))
                        {
                            int advanced = ConvertTo.FromStringToInt32 (lx, 9, out ctConfidence);
                            if (advanced == 0)
                            { Data.CueToolsConfidence = -1; return lx; }
                        }
                        else if (lx.Contains ("Differs"))
                            ctConfidence = -1;
                        else
                            break;

                        if (tx < TracksModel.Data.Items.Count)
                            TracksModel.SetCtConfidence (tx, ctConfidence);
                        if (Data.CueToolsConfidence == null || Data.CueToolsConfidence > ctConfidence)
                            Data.CueToolsConfidence = ctConfidence;
                    }
                    return parser.ReadLineLTrim();
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
                    Severity sev = Data.EacVersionString != null && Data.EacVersionString.StartsWith ("1")? Severity.Warning : Severity.Noise;
                    Data.ShIssue = IssueModel.Add ("EAC log self-hash not present.", sev, IssueTags.StrictErr);
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
                                        Data.ShIssue = IssueModel.Add ("EAC log self-hash verify attempt returned unknown result.", Severity.Advisory, IssueTags.StrictErr);
                                }
                    }
                    catch (Exception ex)
                    { Data.ShIssue = IssueModel.Add ("EAC log self-hash verify attempt failed: " + ex.Message.Trim (null), Severity.Warning, IssueTags.StrictErr); }
                }
            }

            protected override void GetDiagnostics()
            {
                base.GetDiagnostics();

                string OkErr = TracksModel.GetOkDiagnostics();
                if (OkErr != null)
                    Data.OkIssue = IssueModel.Add (OkErr, Severity.Error, IssueTags.Failure);

                string QualErr = TracksModel.GetQualDiagnostics();
                if (QualErr != null)
                    Data.QiIssue = IssueModel.Add (QualErr, Severity.Error, IssueTags.Failure);

                if (String.IsNullOrEmpty (Data.RipArtist))
                    IssueModel.Add ("Missing artist", Severity.Warning, IssueTags.Substandard);

                if (String.IsNullOrEmpty (Data.RipAlbum))
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
                        Data.DsIssue = IssueModel.Add ("Missing drive setting: 'Utilize accurate stream: Yes'." + Data.AccurateStream, Severity.Warning, IssueTags.StrictErr);

                    if (Data.DefeatCache == null || Data.DefeatCache != "Yes")
                        Data.DsIssue = IssueModel.Add ("Missing drive setting: 'Defeat audio cache: Yes'.", Severity.Warning, IssueTags.StrictErr);

                    if (Data.UseC2 == null || Data.UseC2 != "No")
                        Data.DsIssue = IssueModel.Add ("Missing drive setting: 'Make use of C2 pointers: No'.", Severity.Warning, IssueTags.Substandard);
                }

                if (String.IsNullOrEmpty (Data.ReadOffset))
                    IssueModel.Add ("Missing 'Read offset correction'.", Severity.Trivia, IssueTags.StrictWarn);

                if (Data.FillWithSilence != null && Data.FillWithSilence != "Yes")
                    IssueModel.Add ("Missing 'Fill up missing offset samples with silence: Yes'.", Severity.Trivia, IssueTags.StrictWarn);

                if (Data.Quality != null && Data.Quality != "High")
                    IssueModel.Add ("Missing 'Quality: High'.", Severity.Advisory, IssueTags.Substandard);

                if (Data.TrimSilence == null || Data.TrimSilence != "No")
                    Data.TsIssue = IssueModel.Add ("Missing 'Delete leading and trailing silent blocks: No'.", Severity.Warning, IssueTags.StrictErr);

                if (Data.CalcWithNulls != null && Data.CalcWithNulls != "Yes")
                    IssueModel.Add ("Missing 'Null samples used in CRC calculations: Yes'.");

                if (Data.GapHandling != null)
                    if (Data.GapHandling != "Appended to previous track")
                    {
                        IssueTags gapTag = IssueTags.StrictErr;
                        if (Data.GapHandling != "Not detected, thus appended to previous track")
                            gapTag |= IssueTags.StrictErr;

                        Data.GpIssue = IssueModel.Add ("Gap handling preferred setting is 'Appended to previous track'.", Severity.Advisory, gapTag);
                    }

                if (Data.Id3Tag == "Yes")
                    IssueModel.Add ("Append ID3 tags preferred setting is 'No'.", Severity.NoIssue, IssueTags.StrictErr);

                if (Data.ReadOffset == "0" && Data.Drive.Contains ("not found in database"))
                    IssueModel.Add ("Unknown drive with offset '0'.", Severity.Advisory, IssueTags.StrictErr);

                if (Data.NormalizeTo != null)
                    Data.NzIssue = IssueModel.Add ("Use of normalization considered harmful.", Severity.Warning, IssueTags.StrictErr);

                if (Data.SampleFormat != null && Data.SampleFormat != "44.100 Hz; 16 Bit; Stereo")
                    IssueModel.Add ("Missing 'Sample format: 44.100 Hz; 16 Bit; Stereo'.", Severity.Warning, IssueTags.Substandard);

                if (Data.IsRangeRip)
                    IssueModel.Add ("Range rip detected.", Severity.Advisory, IssueTags.StrictWarn);
                else
                {
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

                var arTag = IssueTags.None;
                var arSev = Severity.Trivia;
                if (Data.AccurateRipConfidence != null)
                    if (Data.AccurateRipConfidence.Value > 0)
                        arTag = IssueTags.Success;
                    else
                    {
                        arSev = Severity.Advisory;
                        if (Data.AccurateRipConfidence.Value < 0)
                            arTag = IssueTags.Failure;
                    }
                Data.ArIssue = IssueModel.Add ($"AccurateRip verification {Data.AccurateRipText}.", arSev, arTag);

                var ctSev = Severity.Trivia;
                var ctTag = IssueTags.None;
                if (Data.CueToolsConfidence == null)
                    ctTag = IssueTags.StrictErr;
                else if (Data.CueToolsConfidence.Value < 0)
                    ctSev = Severity.Error;
                else if (Data.CueToolsConfidence.Value == 0)
                    ctSev = Severity.Advisory;
                else
                    ctTag = IssueTags.Success;

                Data.CtIssue = IssueModel.Add ($"CUETools DB verification {Data.CueToolsText}.", ctSev, ctTag);
            }
        }


        private static readonly byte[] logEacSig0x = { (byte)'E', (byte)'A', (byte)'C', (byte)' ' };
        private static readonly byte[] logEacSig0y = Encoding.ASCII.GetBytes ("Exact Audio Copy V0");
        private static readonly byte[] logEacSig1x = Encoding.Unicode.GetBytes ("\uFEFFExact Audio Copy V");

        public LogEacTrack.Vector Tracks { get; private set; }

        public string EacVersionString { get; private set; }
        public string Overread { get; private set; }
        public string Id3Tag { get; private set; }
        public string FillWithSilence { get; private set; }
        public string TrimSilence { get; private set; }
        public string SampleFormat { get; private set; }
        public string CalcWithNulls { get; private set; }
        public string Interface { get; private set; }
        public string NormalizeTo { get; private set; }
        public string Quality { get; private set; }
        public int? TocTrackCount { get; private set; }
        public bool IsRangeRip { get; private set; }
        public int? CueToolsConfidence { get; private set; }

        private string accurateStream;
        private string defeatCache;
        private string useC2;
        private string readMode;

        public string AccurateStream
        {
            get => accurateStream;
            private set { accurateStream = value; readModeText = null; }
        }
        public string DefeatCache
        {
            get => defeatCache;
            private set { defeatCache = value; readModeText = null; }
        }
        public string UseC2
        {
            get => useC2;
            private set { useC2 = value; readModeText = null; }
        }
        public string ReadMode
        {
            get => readMode;
            private set { readMode = value; readModeText = null; }
        }

        public string EacVersionText => EacVersionString?? "unknown";

        public string CueToolsText
        {
            get
            {
                if (CueToolsConfidence == null) return "not attempted";
                if (CueToolsConfidence.Value < 0) return "failed";
                if (CueToolsConfidence.Value == 0) return "data not present";
                return "confidence " + CueToolsConfidence.Value;
            }
        }

        private string readModeText;
        public string ReadModeText
        {
            get
            {
                if (readModeText == null)
                    if (AccurateStream == null && DefeatCache == null && UseC2 == null)
                        readModeText = readMode;
                    else
                        readModeText = (readMode?? "?") + " ("
                            + "AccurateStream=" + (AccurateStream?? "?")
                            + ", DefeatCache=" + (DefeatCache?? "?")
                            + ", UseC2=" + (UseC2?? "?")
                            + ")";
                return readModeText;
            }
        }

        private byte[] storedHash;
        public string SelfHashText
         => storedHash==null? (EacVersionString==null || EacVersionString.StartsWith ("0")? "none" : "missing") : ((storedHash.Length * 8).ToString() + " bits");

        public bool HasRpIssue => RpIssue != null;

        public Issue DsIssue { get; private set; }
        public Issue NzIssue { get; private set; }
        public Issue ShIssue { get; private set; }
        public Issue CtIssue { get; private set; }
        public Issue GpIssue { get; private set; }
        public Issue OkIssue { get; private set; }  // Track OK
        public Issue QiIssue { get; private set; }  // Quality %
        public Issue TsIssue { get; private set; }

        public Encoding Codepage { get; private set; }

        private LogEacFormat (Model model, Stream stream, string path) : base (model, stream, path)
         => this.Tracks = model.TracksModel.Data;

        public override bool IsBadData
         => ShIssue != null && ShIssue.Failure;

        public override void GetReportDetail (IList<string> report)
        {
            if (report.Count > 0)
                report.Add (String.Empty);

            report.Add ($"EAC version = {EacVersionText}");

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
            else
            {
                var sb = new StringBuilder();
                report.Add (String.Empty);
                report.Add ("Tracks:");
                foreach (var tk in Tracks.Items)
                {
                    sb.Clear();
                    sb.AppendFormat ("{0,3}", tk.Number);
                    sb.Append (": ");
                    sb.Append (tk.FileName);
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
