﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KaosIssue;

namespace KaosFormat
{
    public abstract class LogFormat : FormatBase
    {
        public abstract new class Model : FormatBase.Model
        {
            public new LogFormat Data => (LogFormat) _data;
            protected LogTrack.Vector.Model _tracksModel;
            public LogTrack.Vector.Model TracksModel => _tracksModel;

            protected Model()
            { }

            protected virtual void GetDiagnostics()
            {
                if (TracksModel.HasTrackNumberGap())
                    Data.NrIssue = IssueModel.Add ("Gap detected in track numbers.", Severity.Error, IssueTags.Failure);

                TracksModel.CountTestCopy();
                if (TracksModel.Data.CopyCount != TracksModel.GetCount())
                    Data.TkIssue = IssueModel.Add ("Copy pass incomplete.", Severity.Error, IssueTags.Failure);
                else if (TracksModel.Data.TestCount == 0)
                    Data.TpIssue = IssueModel.Add ("Test pass not performed.", Severity.Noise, IssueTags.StrictErr);
                else if (TracksModel.Data.TestCount != TracksModel.GetCount())
                    Data.TpIssue = IssueModel.Add ("Test pass incomplete.", Severity.Error, IssueTags.Failure);
                else if (TracksModel.Data.TestMismatchCount != 0)
                    Data.TpIssue = IssueModel.Add ("Test/copy CRC-32 mismatch.", Severity.Error, IssueTags.Failure);
                else
                    Data.TkIssue = IssueModel.Add ("Test/copy CRC-32s match for all tracks.", Severity.Trivia, IssueTags.Success);
            }

            public void SetRpIssue (string err)
             => Data.RpIssue = IssueModel.Add (err, Severity.Error, IssueTags.Failure);

            public void ValidateRip (IList<FlacFormat> flacs, bool checkTags)
            {
                Data.IsLosslessRip = true;

                Severity baddest = Severity.NoIssue;
                PerformValidations();
                if (baddest < Data.Issues.MaxSeverity)
                    baddest = Data.Issues.MaxSeverity;

                if (baddest >= Severity.Error)
                    Data.RpIssue = IssueModel.Add ($"{Data.Subname} to FLAC rip check failed.", baddest, IssueTags.Failure);
                else if (baddest >= Severity.Warning)
                    Data.RpIssue = IssueModel.Add ($"{Data.Subname} to FLAC rip check successful with warnings.", baddest, IssueTags.Success);
                else
                    Data.RpIssue = IssueModel.Add ($"{Data.Subname} to FLAC rip check successful!", Severity.Advisory, IssueTags.Success);
                return;

                void PerformValidations()
                {
                    if (flacs.Count != TracksModel.GetCount() || flacs.Count == 0)
                    {
                        Data.TkIssue = IssueModel.Add ($"Folder contains {flacs.Count} .flac files, {Data.Subname} log contains {TracksModel.GetCount()} tracks.");
                        baddest = Severity.Error;
                        return;
                    }

                    int errCount = 0, warnCount = 0;
                    string errs = string.Empty, warns = string.Empty;
                    for (int fx = 0; fx < flacs.Count; ++fx)
                    {
                        FlacFormat flac = flacs[fx];
                        if (baddest < flac.Issues.MaxSeverity)
                            baddest = flac.Issues.MaxSeverity;
                        var level = flac.Issues.MaxLevelWhereAny (IssueTags.BadTag);
                        if (level >= Severity.Error)
                        {
                            if (warnCount < 2)
                            {
                                errs = warnCount == 1 ? "s" + errs + ", " : errs + " ";
                                ++errCount;
                            }
                            else
                                errs += ", ";
                            errs = errs + flac.GetTagValue ("TRACKNUMBER");
                        }
                        else if (level == Severity.Warning)
                        {
                            if (warnCount < 2)
                            {
                                warns = warnCount == 1 ? "s" + warns + ", " : warns + " ";
                                ++warnCount;
                            }
                            else
                                warns += ", ";
                            warns = warns + flac.GetTagValue ("TRACKNUMBER");
                        }
                    }
                    if (warns.Length > 0)
                        IssueModel.Add ($"Tag issues on track{warns}.", Severity.Warning);
                    if (errs.Length > 0)
                        IssueModel.Add ($"Tag issues on track{errs}.", Severity.Error);

                    baddest = flacs.Max (tk => tk.Issues.MaxSeverity);
                    if (flacs.Count != flacs.Where (tk => tk.ActualAudioBlockCRC16 != null).Count())
                        IssueModel.Add ("FLAC intrinsic CRC checks not performed.", Severity.Warning, IssueTags.StrictErr);

                    TracksModel.MatchFlacs (flacs);
                    if (TracksModel.Data.RipMismatchCount != 0)
                        Data.MhIssue = IssueModel.Add ("Log CRC-32 match to FLAC PCM CRC-32 failed.", Severity.Error, IssueTags.Failure);
                    else
                    {
                        Data.MhIssue = IssueModel.Add ("Log/FLAC CRC-32s match for all tracks.", Severity.Trivia);
                        if (checkTags)
                            CheckFlacRipTags (flacs);
                    }
                }
            }

            public void ValidateRip (IList<Mp3Format> mp3s)
            {
                Severity baddest = Severity.NoIssue;
                Data.IsLosslessRip = false;
                performValidations();

                if (mp3s.Count > 0)
                {
                    Severity baddestOfMp3s = mp3s.Max (tk => tk.Issues.MaxSeverity);
                    if (baddestOfMp3s >= Severity.Error)
                        IssueModel.Add (GetMessage (Severity.Error));
                    else if (baddestOfMp3s == Severity.Warning)
                        IssueModel.Add (GetMessage (Severity.Warning));
                }

                if (baddest < IssueModel.Data.MaxSeverity)
                    baddest = IssueModel.Data.MaxSeverity;

                if (baddest >= Severity.Error)
                    Data.RpIssue = IssueModel.Add ($"{Data.Subname} to MP3 rip check failed.", baddest, IssueTags.Failure);
                else if (baddest == Severity.Warning)
                    Data.RpIssue = IssueModel.Add ($"{Data.Subname} to MP3 rip check OK with warnings.", baddest, IssueTags.Success);
                else
                    Data.RpIssue = IssueModel.Add ($"{Data.Subname} to MP3 rip check OK!", Severity.Advisory, IssueTags.Success);

                void performValidations()
                {
                    if (mp3s.Count != TracksModel.GetCount() || mp3s.Count == 0)
                    {
                        Data.TkIssue = IssueModel.Add ($"Directory contains {mp3s.Count} .mp3 files, {Data.Subname} log contains {TracksModel.GetCount()} tracks.", Severity.Error, IssueTags.Failure);
                        baddest = Severity.Error;
                        return;
                    }

                    if (mp3s.Count != mp3s.Where (tk => tk.Lame != null && tk.Lame.ActualDataCrc != null).Count())
                        IssueModel.Add ("Track CRC checks not performed.", Severity.Warning, IssueTags.StrictErr);

                    if (mp3s[0].Lame != null)
                    {
                        string profile = mp3s[0].Lame.Profile;
                        for (int ix = 1; ; ++ix)
                        {
                            if (ix == mp3s.Count)
                            { IssueModel.Add ($"MP3 rip profile is {profile}.", Severity.Advisory); break; }

                            if (mp3s[ix].Lame == null || mp3s[ix].Lame.Profile != profile)
                            {
                                IssueModel.Add ("Inconsistent MP3 encoder settings.", Severity.Warning, IssueTags.StrictErr);
                                break;
                            }
                        }
                    }
                }

                string GetMessage (Severity level)
                {
                    string msg = null;
                    for (int mx = 0; mx < mp3s.Count; ++mx)
                        if (mp3s[mx].Issues.MaxSeverity >= level)
                            if (msg == null)
                                msg = "File issues on MP3 #" + (mx+1);
                            else
                                msg += ", #" + (mx+1);
                    return msg + ".";
                }
            }

            public void CheckFlacRipTags (IList<FlacFormat> flacs)
            {
                int prevTrackNum = -1;
                foreach (FlacFormat flac in flacs)
                {
                    var trackTag = flac.GetTagValue ("TRACKNUMBER");

                    var integerRegex = new Regex ("^([0-9]+)", RegexOptions.Compiled);
                    MatchCollection reMatches = integerRegex.Matches (trackTag);
                    string trackTagCapture = reMatches.Count == 1 ? reMatches[0].Groups[1].ToString() : trackTag;

                    if (int.TryParse (trackTagCapture, out int trackNum))
                    {
                        if (prevTrackNum >= 0 && trackNum != prevTrackNum + 1)
                            IssueModel.Add ($"Gap in TRACKNUMBER tags near '{trackTag}'.", Severity.Error, IssueTags.BadTag);
                        prevTrackNum = trackNum;
                    }
                }

                TagCheckTextIsSame (flacs, "TRACKTOTAL");
                TagCheckTextIsSame (flacs, "DISCNUMBER");
                TagCheckTextIsSame (flacs, "DISCTOTAL");
                TagCheckTextIsSame (flacs, "DATE");
                TagCheckTextIsSame (flacs, "RELEASE DATE");

                bool? isSameAA = FlacFormat.IsFlacTagsAllSame (flacs, "ALBUMARTIST");
                if (isSameAA == false)
                    IssueModel.Add ("ALBUMARTIST tags are inconsistent.", Severity.Warning, IssueTags.BadTag|IssueTags.StrictErr);
                else if (isSameAA == null)
                {
                    bool? isSameArtist = FlacFormat.IsFlacTagsAllSame (flacs, "ARTIST");
                    if (isSameArtist == false)
                        IssueModel.Add ("Inconsistent ARTIST tag or missing ALBUMARTIST tag.", Severity.Warning, IssueTags.BadTag);
                    else if (isSameArtist == null)
                        IssueModel.Add ("ARTIST is missing.", Severity.Warning, IssueTags.BadTag);
                }

                TagCheckTextIsSame (flacs, "ALBUM");
                TagCheckTextIsSame (flacs, "ORGANIZATION");
                TagCheckTextIsSame (flacs, "BARCODE");
                TagCheckTextIsSame (flacs, "CATALOGNUMBER");
                TagCheckTextIsSame (flacs, "ALBUMARTISTSORTORDER");
            }

            private void TagCheckTextIsSame (IList<FlacFormat> flacs, string key)
            {
                if (FlacFormat.IsFlacMultiTagAllSame (flacs, key) == false)
                    IssueModel.Add (key + " tags are inconsistent.", Severity.Warning, IssueTags.BadTag|IssueTags.StrictErr);
            }
        }

        public string Application { get; protected set; }
        public string RipDate { get; protected set; }
        public string RipArtist { get; protected set; }
        public string RipAlbum { get; protected set; }
        public string RipArtistAlbum => RipArtist + " / " + RipAlbum;
        public string Drive { get; protected set; }
        public string ReadOffset { get; protected set; }
        public string GapHandling { get; protected set; }

        public int? AccurateRip { get; protected set; }
        public int? AccurateRipConfidence { get; protected set; }

        public bool? IsLosslessRip { get; private set; } = null;

        public Issue ArIssue { get; protected set; }  // AccurateRip
        public Issue TkIssue { get; protected set; }  // Tracks
        public Issue NrIssue { get; private set; }    // Log track number
        public Issue TpIssue { get; protected set; }  // Test pass
        public Issue MhIssue { get; private set; }    // Lossless match
        public Issue RpIssue { get; private set; }    // Rip result

        public LogFormat (FormatBase.Model model, Stream stream, string path) : base (model, stream, path)
        { }

        public string AccurateRipText
        {
            get
            {
                if (AccurateRipConfidence == null) return "not attempted";
                if (AccurateRipConfidence.Value < 0) return "failed";
                if (AccurateRipConfidence.Value == 0) return "data not present";
                return $"confidence {AccurateRipConfidence.Value} (v{AccurateRip})";
            }
        }
    }
}
