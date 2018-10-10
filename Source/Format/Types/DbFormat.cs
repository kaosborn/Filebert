﻿using System;
using System.IO;

namespace KaosFormat
{
    public class DbFormat : FormatBase
    {
        public static string[] Names
         => new string[] { "db" };

        public static string Subname
         => "Thumbs";

        public override string[] ValidNames
         => Names;

        public override string LongName
         => "db (Thumbs)";

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (path.EndsWith (System.IO.Path.DirectorySeparatorChar + "thumbs.db", StringComparison.InvariantCultureIgnoreCase))
                return new Model (stream, path);
            return null;
        }


        public new class Model : FormatBase.Model
        {
            public new readonly DbFormat Data;

            public Model (Stream stream, string path)
            {
                base._data = Data = new DbFormat (this, stream, path);

                // No content diagnostics at this time.
                if (Data.fbs.Length == 0)
                    IssueModel.Add ("File is empty.");
            }
        }


        private DbFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }
    }


    // Class DbOtherFormat exists just to suppress errors on non-thumbs .db files.
    public class DbOtherFormat : FormatBase
    {
        public static string[] Names
         => new string[] { "db" };

        public static string Subname
         => "*hidden*";

        public override string[] ValidNames
         => Names;

        public override string LongName
         => "db (*hidden*)";

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (path.EndsWith (".db", StringComparison.InvariantCultureIgnoreCase))
                if (! path.EndsWith (System.IO.Path.DirectorySeparatorChar + "thumbs.db", StringComparison.InvariantCultureIgnoreCase))
                    return new DbOtherFormat.Model (stream, path);
            return null;
        }


        public new class Model : FormatBase.Model
        {
            public new readonly DbOtherFormat Data;

            public Model (Stream stream, string path)
             => base._data = Data = new DbOtherFormat (this, stream, path);
        }


        private DbOtherFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }
    }
}
