﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using KaosIssue;

namespace KaosFormat
{
    public class AviFormat : RiffContainer
    {
        public static string[] Names
         => new string[] { "avi", "divx" };

        public override string[] ValidNames
         => Names;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (hdr.Length >= 0x0C
                    && hdr[0x00]=='R' && hdr[0x01]=='I' && hdr[0x02]=='F' && hdr[0x03]=='F'
                    && hdr[0x08]=='A' && hdr[0x09]=='V' && hdr[0x0A]=='I' && hdr[0x0B]==' ')
                return new Model (stream, hdr, path);
            return null;
        }


        public new class Model : RiffContainer.Model
        {
            public new readonly AviFormat Data;

            public Model (Stream stream, byte[] header, string path)
            {
                base._data = Data = new AviFormat (this, stream, path);

                ParseRiff (header);

                var buf = new byte[0xC0];

                stream.Position = 0;
                int got = stream.Read (buf, 0, 0xC0);
                if (got != 0xC0)
                {
                    IssueModel.Add ("File is short", Severity.Fatal);
                    return;
                }

                Data.StreamCount = ConvertTo.FromLit32ToInt32 (buf, 0x38);
                Data.Width = ConvertTo.FromLit32ToInt32 (buf, 0x40);
                Data.Height = ConvertTo.FromLit32ToInt32 (buf, 0x44);
                Data.Codec = Encoding.ASCII.GetString (buf, 0xBC, 4).Trim();

                CalcMark();
                GetDiagsForMarkable();
            }
        }


        public int StreamCount { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public string Codec {get; private set; }
        public string Dimensions => Width.ToString() + 'x' + Height;

        private AviFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }

        public override void GetReportDetail (IList<string> report)
        {
            base.GetReportDetail (report);
            if (report.Count > 0)
                report.Add (String.Empty);

            report.Add ($"Codec = {Codec}");
            report.Add ($"Dimensions = {Dimensions}");
            report.Add ($"Streams = {StreamCount}");
        }
    }
}
