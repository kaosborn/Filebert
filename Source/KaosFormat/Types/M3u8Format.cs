﻿using System.IO;
using System.Text;

namespace KaosFormat
{
    public class M3u8Format : FilesContainer
    {
        public static string[] SNames => new string[] { "m3u8" };
        public override string[] Names => SNames;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (path.ToLower().EndsWith(".m3u8"))
                return new Model (stream, path);
            return null;
        }


        public new class Model : FilesContainer.Model
        {
            public new readonly M3u8Format Data;

            public Model (Stream stream, string path) : base (path)
            {
                base._data = Data = new M3u8Format (this, stream, path);

                stream.Position = 0;
                TextReader tr = new StreamReader (stream, Encoding.UTF8);

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
        }


        private M3u8Format (Model model, Stream stream, string path) : base (model, stream, path)
        { }
    }
}
