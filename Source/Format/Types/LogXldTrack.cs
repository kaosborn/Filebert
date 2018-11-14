using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KaosFormat
{
    public class LogXldTrack : LogTrack
    {
        public new class Vector : LogTrack.Vector
        {
            public new class Model : LogTrack.Vector.Model
            {
                public readonly new Vector Data;

                public Model()
                 => _data = Data = new Vector();

                // alas it's .NET 4.0
                public override LogTrack GetItem (int ix) => Data.items[ix];
                public override int GetCount() => Data.items.Count;

                public void Add (int number, string fileName, uint? testCRC, uint? copyCRC)
                 => Data.items.Add (new LogXldTrack (number, fileName, testCRC, copyCRC));
            }


            private readonly List<LogXldTrack> items;
            public ReadOnlyCollection<LogXldTrack> Items { get; private set; }

            public Vector()
            {
                this.items = new List<LogXldTrack>();
                this.Items = new ReadOnlyCollection<LogXldTrack> (this.items);
            }
        }

        public LogXldTrack (int number, string fileName, uint? testCRC, uint? copyCRC)
            : base (number, fileName, testCRC, copyCRC)
        { }
    }
}
