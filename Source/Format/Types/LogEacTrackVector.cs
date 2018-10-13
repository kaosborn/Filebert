using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using KaosIssue;

namespace KaosFormat
{
    public class LogEacTrack
    {
        public class Vector
        {
            public class Model
            {
                public readonly Vector Data;

                public Model()
                 => Data = new Vector();

                public void SetSeverest (int index, Issue issue)
                 => Data.items[index].RipSeverest = issue;

                public void Add (int number, string fileName, string pregap, string peak, string speed,
                                 string quality, uint? testCRC, uint? copyCRC, bool hasOK, int? arVersion, int? arConfidence)
                 => Data.items.Add (new LogEacTrack (number, fileName, pregap, peak, speed, quality, testCRC, copyCRC, hasOK, arVersion, arConfidence));

                public string MatchFlac (FlacFormat flac)
                {
                    if (flac.ActualPcmCRC32 != null)
                        foreach (LogEacTrack item in Data.items)
                            if (item.match == null)
                                if (item.CopyCRC != flac.ActualPcmCRC32)
                                    break;
                                else
                                {
                                    item.match = flac;
                                    return null;
                                }
                    return "PCM CRC-32 check mismatch.";
                }

                public void SetCtConfidence (int number, int confidence)
                 => Data.items[number].CtConfidence = confidence;
            }


            private readonly List<LogEacTrack> items;
            public ReadOnlyCollection<LogEacTrack> Items { get; private set; }

            public Vector()
            {
                this.items = new List<LogEacTrack>();
                this.Items = new ReadOnlyCollection<LogEacTrack> (this.items);
            }


#region log-EAC log entry methods
            // This routine allows track 1 to be missing (e.g. Quake soundtrack)
            public bool IsNearlyAllPresent()
            {
                if (items.Count == 0)
                    return false;

                int tn0 = items[0].Number;
                if (tn0 > 2)
                    return false;

                for (int ti = 1; ti < items.Count; ++ti)
                    if (ti != items[ti].Number - tn0)
                        return false;

                return true;
            }

            public bool AllHasOK()
            {
                foreach (var item in items)
                    if (! item.HasOK)
                        return false;
                return true;
            }

            public bool AllHasOkWithQuality()
            {
                foreach (var item in items)
                    if (! item.HasOK || String.IsNullOrWhiteSpace (item.Qual))
                        return false;
                return true;
            }
#endregion
        }


        public int Number { get; private set; }
        public string FilePath { get; private set; }
        public string Pregap { get; private set; }
        public string Peak { get; private set; }
        public string Speed { get; private set; }
        public string Qual { get; private set; }
        public UInt32? TestCRC { get; private set; }
        public UInt32? CopyCRC { get; private set; }
        public bool HasOK { get; private set; }
        public int? AR { get; private set; }
        public int? ArConfidence { get; private set; }
        public int? CtConfidence { get; private set; }

        public bool HasQuality => ! String.IsNullOrWhiteSpace (Qual);
        public bool IsBadCRC => CopyCRC != null && TestCRC != null && TestCRC != CopyCRC;
        public bool IsTrackOK => HasOK && HasQuality && ! IsBadCRC;
        public bool IsRipOK => IsTrackOK && match != null && match.Issues.MaxSeverity < Severity.Error;

        private FormatBase match = null;
        public string MatchName => match == null ? null : match.Name;

        public Issue RipSeverest { get; private set; }

        private LogEacTrack (int number, string path, string pregap, string peak, string speed,
                             string quality, uint? testCRC, uint? copyCRC, bool isOK, int? arVersion, int? confidence)
        {
            this.Number = number;
            this.FilePath = path;
            this.Pregap = pregap;
            this.Peak = peak;
            this.Speed = speed;
            this.Qual = quality;
            this.TestCRC = testCRC;
            this.CopyCRC = copyCRC;
            this.HasOK = isOK;
            this.AR = arVersion;
            this.ArConfidence = confidence;
        }
    }
}
