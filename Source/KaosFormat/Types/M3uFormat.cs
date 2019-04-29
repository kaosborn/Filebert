using System;
using System.IO;

namespace KaosFormat
{
    // en.wikipedia.org/wiki/M3U
    public class M3uFormat : FilesContainer
    {
        public static string[] SNames => new string[] { "m3u" };
        public override string[] Names => SNames;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (path.ToLower().EndsWith(".m3u") ||
                  hdr.Length >= 7 && hdr[0]=='#' && hdr[1]=='E' && hdr[2]=='X'
                   && hdr[3]=='T' && hdr[4]=='M' && hdr[5]=='3' && hdr[6]=='U')
                return new Model (stream, path);
            return null;
        }

        public new class Model : FilesContainer.Model
        {
            public new readonly M3uFormat Data;

            public Model (Stream stream, string path) : base (path)
            {
                base._data = Data = new M3uFormat (this, stream, path);

                stream.Position = 0;
                TextReader tr = new StreamReader (stream, FormatBase.Cp1252);

                for (int line = 1; ; ++line)
                {
                    var lx = tr.ReadLine();
                    if (lx == null)
                        break;
                    lx = lx.TrimStart();
                    if (lx.Length > 0 && lx[0] != '#')
                        FilesModel.Add (lx);
                }
            }

            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                base.CalcHashes (hashFlags, validationFlags);
                GetDiagnostics();
            }
        }


        private M3uFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }
    }
}
