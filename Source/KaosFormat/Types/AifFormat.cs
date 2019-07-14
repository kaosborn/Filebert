using KaosIssue;
using System;
using System.Collections.Generic;
using System.IO;

namespace KaosFormat
{
    // https://en.wikipedia.org/wiki/Audio_Interchange_File_Format
    // http://muratnkonar.com/aiff/
    // http://www-mmsp.ece.mcgill.ca/Documents/AudioFormats/AIFF/Samples.html
    // http://www-mmsp.ece.mcgill.ca/Documents/AudioFormats/AIFF/AIFF.html

    public class AifFormat : IffContainer
    {
        public static string[] SNames => new string[] { "aif", "aiff", "aifc" };
        public override string[] Names => SNames;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (hdr.Length >= 0x24 && hdr[0]=='F' && hdr[1]=='O' && hdr[2]=='R' && hdr[3]=='M'
                                   && hdr[8]=='A' && hdr[9]=='I' && hdr[10]=='F'
                                   && (hdr[11]=='F' || hdr[11]=='C'))
                return new Model (stream, hdr, path);
            return null;
        }

        public new class Model : IffContainer.Model
        {
            public new readonly AifFormat Data;

            public Model (Stream stream, byte[] header, string path)
            {
                base._data = Data = new AifFormat (this, stream, path);

                ParseAif (stream, header);
                if (IssueModel.Data.HasError)
                    return;

                CalcMark();
                GetDiagsForMarkable();
            }

            private void ParseAif (Stream stream, byte[] hdr)
            {
                Data.GroupId = ConvertTo.FromAsciiToString (hdr, 0, 4);
                Data.FormType = ConvertTo.FromAsciiToString (hdr, 8, 4);

                UInt32 gSize = ConvertTo.FromBig32ToUInt32 (hdr, 4);
                if (gSize < 12 || gSize > stream.Length - 8)
                { IssueModel.Add ("File truncated or corrupt.", Severity.Fatal); return; }

                int hPos = 12;
                string id0 = ConvertTo.FromAsciiToString (hdr, 12, 4);
                if (Data.IsCompressed)
                {
                    if (id0 != "FVER")
                    { IssueModel.Add ("Missing 'FVER' chunk."); return; }

                    UInt32 vSize = ConvertTo.FromBig32ToUInt32 (hdr, 16);
                    if (vSize != 4)
                    { IssueModel.Add ("Bad 'FVER' chunk."); return; }

                    hPos = 24;
                    id0 = ConvertTo.FromAsciiToString (hdr, 24, 4);
                }

                if (id0 != "COMM")
                { IssueModel.Add ("Missing 'COMM' chunk."); return; }

                UInt32 cSize = ConvertTo.FromBig32ToUInt32 (hdr, hPos + 4);
                if (cSize < 18 || cSize > stream.Length - 8)
                { IssueModel.Add ("Bad 'COMM' chunk."); return; }

                Data.ChannelCount = ConvertTo.FromBig16ToInt32 (hdr, hPos + 8);
                Data.SampleSize = ConvertTo.FromBig16ToInt32 (hdr, hPos + 14);

                ++Data.IffChunkCount;
                Data.IffSize = gSize;
                Data.mediaPosition = 0;
                Data.MediaCount = gSize + 8;
                Data.ValidSize = hPos + cSize + 8;

                var hasSSND = false;
                var buf = new byte[8];
                while (Data.ValidSize < Data.MediaCount)
                {
                    stream.Position = Data.ValidSize;

                    int got = stream.Read (buf, 0, 8);
                    if (got != 8)
                    { IssueModel.Add ("Read failed."); return; }

                    cSize = ConvertTo.FromBig32ToUInt32 (buf, 4);
                    if (cSize < 8 || Data.ValidSize + cSize > Data.MediaCount - 8)
                    { IssueModel.Add ("Bad chunk size or truncated file."); return; }

                    string id = ConvertTo.FromAsciiToString (buf, 0, 4);
                    if (id == "SSND")
                    {
                        if (hasSSND)
                        { IssueModel.Add ("Many 'SSND' chunks."); return; }
                        hasSSND = true;
                    }
                    else if (id != "(c) " && id != "ANNO" && id != "AUTH" && id != "NAME")
                    { IssueModel.Add ($"Unexpected '{id}' chunk."); return; }

                    Data.ValidSize += cSize + 8;
                    if ((cSize & 1) != 0)
                        ++Data.ValidSize;
                }

                if (! hasSSND)
                    IssueModel.Add ("Missing 'SSND' chunk.", Severity.Warning);
            }
        }


        public string GroupId { get; private set; }
        public string FormType { get; private set; }
        public int ChannelCount { get; private set; }
        public int SampleSize { get; private set; }
        public bool IsCompressed => FormType == "AIFC";

        private AifFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }

        public override void GetReportDetail (IList<string> report)
        {
            base.GetReportDetail (report);
            if (report.Count > 0)
                report.Add (String.Empty);

            report.Add ($"Channels = {ChannelCount}");
            report.Add ($"Sample size = {SampleSize}");
            report.Add ($"Compressed = {IsCompressed}");
        }
    }
}
