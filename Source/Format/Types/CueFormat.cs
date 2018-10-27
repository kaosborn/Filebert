﻿using System;
using System.Collections.Generic;
using System.IO;
using KaosIssue;

namespace KaosFormat
{
    // en.wikipedia.org/wiki/Cue_sheet_(computing)#Cue_sheet_syntax
    // digitalx.org/cue-sheet/syntax/index.html
    public class CueFormat : FilesContainer
    {
        public static string[] Names
         => new string[] { "cue" };

        public override string[] ValidNames
         => Names;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (path.ToLower().EndsWith(".cue"))
                return new Model (stream, path);
            return null;
        }


        public new class Model : FilesContainer.Model
        {
            public new readonly CueFormat Data;

            public Model (Stream stream, string path) : base (path)
            {
                base._data = Data = new CueFormat (this, stream, path);

                SetIgnoredName ("Range.wav");
                Data.fbs.Position = 0;
                TextReader tr = new StreamReader (Data.fbs, FormatBase.Cp1252);

                for (int line = 1; ; ++line)
                {
                    var lx = tr.ReadLine();
                    if (lx == null)
                        break;

                    lx = lx.TrimStart();
                    if (lx.Length == 0)
                        continue;

                    if (lx.StartsWith ("CATALOG "))
                    {
                        Data.Catalog = lx.Substring (8).Trim();
                        if (Data.Catalog.Length != 13)
                            IssueModel.Add ("Invalid CATALOG.");
                        continue;
                    }

                    if (lx.Length > 0 && lx.StartsWith ("FILE "))
                    {
                        var name = Data.GetQuotedField (lx, 5);
                        if (name.Length == 0)
                            IssueModel.Add ("Missing file name.");
                        else
                            FilesModel.Add (name);
                    }
                }
            }
        }


        public string Catalog { get; private set; }

        private CueFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }

        public string GetQuotedField (string text, int pos)
        {
            do
            {
                if (pos >= text.Length)
                    return String.Empty;
            }
            while (text[pos]==' ' || text[pos]=='\t');

            if (text[pos]=='"')
            {
                int pos2 = text.IndexOf ('"', pos+1);
                return text.Substring (pos+1, pos2-pos-1);
            }
            else
            {
                int pos2 = text.IndexOf (' ', pos+1);
                return text.Substring (pos, pos2-pos);
            }
        }

        public override void GetReportDetail (IList<string> report)
        {
            base.GetReportDetail (report);

            if (! String.IsNullOrEmpty (Catalog))
            {
                if (report.Count != 0)
                    report.Add (String.Empty);
                report.Add ($"Catalog = {Catalog}");
            }
        }
    }
}
