using System.Collections.Generic;
using System.IO;

namespace KaosFormat
{
    public class Mpeg1Format : IffContainer
    {
        public override string[] Names => Mpeg2Format.SNames;

        public new class Model : IffContainer.Model
        {
            public new readonly Mpeg1Format Data;

            public Model (Stream stream, byte[] header, string path)
            {
                base._data = Data = new Mpeg1Format (this, stream, path);
                ParseRiff (header);
                GetDiagsForMarkable();
            }
        }


        private Mpeg1Format (Model model, Stream stream, string path) : base (model, stream, path)
        { }

        public override void GetReportDetail (IList<string> report)
        {
            report.Add ("Format = MPEG-1 (CDXA)");
        }
    }
}
