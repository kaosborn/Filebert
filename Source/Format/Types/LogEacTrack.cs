using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KaosFormat
{
    public class LogEacTrack : LogTrack
    {
        public new class Vector : LogTrack.Vector
        {
            public new class Model : LogTrack.Vector.Model
            {
                public readonly new Vector Data;

                public Model()
                 => _data = Data = new Vector();

                public override LogTrack GetItem (int ix) => Data.items[ix];
                public override int GetCount() => Data.items.Count;

                public void Add (int number, string fileName, string pregap, string peak, string speed,
                                 string quality, uint? testCRC, uint? copyCRC, bool hasOK, int? arVersion, int? arConfidence)
                 => Data.items.Add (new LogEacTrack (number, fileName, pregap, peak, speed, quality, testCRC, copyCRC, hasOK, arVersion, arConfidence));

                public void SetCtConfidence (int number, int confidence)
                 => Data.items[number].CtConfidence = confidence;

                public string GetOkDiagnostics()
                {
                    string err = null;
                    foreach (LogEacTrack tk in Data.items)
                        if (! tk.HasOk)
                        {
                            tk.IsTrackOk = false;
                            err = "Missing 'Copy OK'.";
                        }
                    return err;
                }

                public string GetQualDiagnostics()
                {
                    string err = null;
                    foreach (LogEacTrack tk in Data.items)
                        if (! tk.HasQuality)
                        {
                            tk.IsTrackOk = false;
                            err = "Missing 'Track quality'.";
                        }
                    return err;
                }
            }


            private readonly List<LogEacTrack> items;
            public ReadOnlyCollection<LogEacTrack> Items { get; private set; }

            public Vector()
            {
                this.items = new List<LogEacTrack>();
                this.Items = new ReadOnlyCollection<LogEacTrack> (this.items);
            }
        }


        public string Pregap { get; private set; }
        public string Peak { get; private set; }
        public string Speed { get; private set; }
        public string Qual { get; private set; }
        public bool HasOk { get; private set; }
        public int? AR { get; private set; }
        public int? ArConfidence { get; private set; }
        public int? CtConfidence { get; private set; }

        public bool HasQuality => ! String.IsNullOrWhiteSpace (Qual);

        private LogEacTrack (int number, string path, string pregap, string peak, string speed,
                             string quality, uint? testCRC, uint? copyCRC, bool isOk, int? arVersion, int? confidence)
            : base (number, path, testCRC, copyCRC)
        {
            this.Pregap = pregap;
            this.Peak = peak;
            this.Speed = speed;
            this.Qual = quality;
            this.HasOk = isOk;
            this.AR = arVersion;
            this.ArConfidence = confidence;
        }
    }
}
