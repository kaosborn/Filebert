using System;
using System.Collections.Generic;

namespace KaosFormat
{
    public class LogTrack
    {
        public class Vector
        {
            public abstract class Model
            {
                protected Vector _data;
                public Vector Data => _data;

                protected Model()
                { }

                public abstract LogTrack GetItem (int ix);
                public abstract int GetCount();

                // This routine allows track 1 to be missing (e.g. Quake soundtrack)
                public bool HasTrackNumberGap()
                {
                    if (GetCount() == 0)
                        return false;

                    int tn0 = GetItem(0).Number;
                    if (tn0 > 2)
                        return true;

                    for (int tx = 1; tx < GetCount(); ++tx)
                        if (tx != GetItem(tx).Number - tn0)
                            return true;

                    return false;
                }

                public void CountTestCopy()
                {
                    for (int tx = 0; tx < GetCount(); ++tx)
                    {
                        LogTrack track = GetItem (tx);
                        track.IsTrackOk = false;
                        if (track.CopyCRC != null)
                            ++Data.CopyCount;
                        if (track.TestCRC == null)
                            track.IsTrackOk = track.CopyCRC != null;
                        else
                        {
                            ++Data.TestCount;
                            track.IsTrackOk = track.CopyCRC == track.TestCRC;
                            if (! track.IsTrackOk)
                                ++Data.TestMismatchCount;
                        }
                    }

                    if (Data.TestCount > 0 && Data.TestCount < GetCount())
                        for (int tx = 0; tx < GetCount(); ++tx)
                        {
                            LogTrack track = GetItem (tx);
                            if (track.TestCRC == null)
                                track.IsTrackOk = false;
                        }
                }

                public void MatchFlacs (IList<FlacFormat> flacs)
                {
                    Data.RipMismatchCount = GetCount();
                    for (int fx = 0; fx < flacs.Count; ++fx)
                        for (int tx = 0; tx < GetCount(); ++tx)
                        {
                            LogTrack track = GetItem (tx);
                            if (track.Match == null && track.CopyCRC == flacs[fx].ActualPcmCRC32)
                            {
                                track.Match = flacs[fx];
                                track.IsMatchOk = tx == fx;
                                if (track.IsMatchOk == true)
                                    --Data.RipMismatchCount;
                                break;
                            }
                        }
                    for (int tx = 0; tx < GetCount(); ++tx)
                    {
                        LogTrack track = GetItem (tx);
                        if (track.Match == null)
                            track.IsMatchOk = false;
                    }
                }
            }


            public int TestCount { get; private set; } = 0;
            public int CopyCount { get; private set; } = 0;
            public int TestMismatchCount { get; private set; } = 0;
            public int? RipMismatchCount { get; private set; } = null;
        }

        public int Number { get; private set; }
        public string FileName { get; private set; }
        public UInt32? TestCRC { get; private set; }
        public UInt32? CopyCRC { get; private set; }
        public bool IsTrackOk { get; protected set; }
        public bool? IsMatchOk { get; private set; }

        public bool? IsRipOk => (! IsTrackOk) ? false : IsMatchOk;

        public FormatBase Match { get; private set; } = null;
        public string MatchName => Match?.Name;

        protected LogTrack (int number, string fileName, uint? testCRC, uint? copyCRC)
        {
            Number = number;
            FileName = fileName;
            TestCRC = testCRC;
            CopyCRC = copyCRC;
        }
    }
}
