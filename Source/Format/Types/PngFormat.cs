﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using KaosCrypto;
using KaosIssue;

// www.w3.org/TR/PNG-Structure.html

namespace KaosFormat
{
    public class PngFormat : FormatBase
    {
        public static string[] Names
         => new string[] { "png" };

        public override string[] ValidNames
         => Names;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (hdr.Length >= 0x28
                    && hdr[0x00]==0x89 && hdr[0x01]=='P' && hdr[0x02]=='N' && hdr[0x03]=='G'
                    && hdr[0x04]==0x0D && hdr[0x05]==0x0A && hdr[0x06]==0x1A && hdr[0x07]==0x0A)
                return new Model (stream, path);
            return null;
        }


        public new class Model : FormatBase.Model
        {
            public new readonly PngFormat Data;
            public readonly PngChunk.Vector.Model ChunksModel;

            public Model (Stream stream, string path)
            {
                ChunksModel = new PngChunk.Vector.Model();
                base._data = Data = new PngFormat (this, stream, path);

                // Arbitrary sanity limit.
                if (Data.FileSize > 100000000)
                {
                    IssueModel.Add ("File size insanely huge.", Severity.Fatal);
                    return;
                }

                Data.fBuf = new byte[(int) Data.FileSize];
                var fBuf = Data.fBuf;

                stream.Position = 0;
                int got = stream.Read (fBuf, 0, (int) Data.FileSize);
                if (got < Data.FileSize)
                {
                    IssueModel.Add ("Read failed.", Severity.Fatal);
                    return;
                }

                Data.ValidSize = 8;

                if (fBuf[0x0C]!='I' || fBuf[0x0D]!='H' || Data.fBuf[0x0E]!='D' || Data.fBuf[0x0F]!='R')
                {
                    IssueModel.Add ("Missing 'IHDR' chunk.", Severity.Fatal);
                    return;
                }

                Data.Width = ConvertTo.FromBig32ToInt32 (fBuf, 0x10);
                Data.Height = ConvertTo.FromBig32ToInt32 (fBuf, 0x14);
                Data.BitDepth = fBuf[0x18];
                Data.ColorType = fBuf[0x19];
                Data.CompressionMethod = fBuf[0x1A];
                Data.FilterMethod = fBuf[0x1B];
                Data.InterlaceMethod = fBuf[0x1C];

                do
                {
                    UInt32 chunkSize = ConvertTo.FromBig32ToUInt32 (fBuf, (int) Data.ValidSize);
                    if (Data.ValidSize + chunkSize + 12 > Data.FileSize)
                    {
                        IssueModel.Add ("File is corrupt or truncated.", Severity.Fatal);
                        return;
                    }

                    string type = Encoding.ASCII.GetString (fBuf, (int) Data.ValidSize+4, 4);
                    UInt32 storedCRC = ConvertTo.FromBig32ToUInt32 (fBuf, (int) (Data.ValidSize + chunkSize + 8));
                    ChunksModel.Add (type, chunkSize, storedCRC);

                    var typeLow = type.ToLower();
                    switch (typeLow)
                    {
                        case "idat":
                            if (Data.mediaPosition <= 0)
                                Data.mediaPosition = Data.ValidSize;
                            break;
                        case "iend":
                            if (Data.MediaCount > 0)
                                IssueModel.Add ("Multiple IEND chunks.");
                            else
                                Data.MediaCount = Data.ValidSize - Data.mediaPosition + chunkSize + 0xC;
                            break;
                        case "text":
                            if (chunkSize > 0x7FFF)
                                IssueModel.Add ("String size too large.");
                            else
                            {
                                var escaped = new StringBuilder();
                                for (int ix = (int) Data.ValidSize+8; ix < (int) Data.ValidSize+8+chunkSize; ++ix)
                                    if (fBuf[ix] < ' ' || fBuf[ix] > 0x7F)
                                        escaped.AppendFormat ("\\{0:x2}", fBuf[ix]);
                                    else
                                        escaped.Append ((char) fBuf[ix]);

                                Data.texts.Add (escaped.ToString());
                            }
                            break;
                        case "gama":
                            if (Data.Gamma != null)
                                IssueModel.Add ("Unexpected multiple gamma chunks.");
                            else
                                if (chunkSize != 4)
                                    IssueModel.Add ($"Bad gamma chunk size '{chunkSize}', expecting '4'.");
                                else
                                    Data.Gamma = ConvertTo.FromBig32ToUInt32 (fBuf, (int) Data.ValidSize+8) / 100000f;
                            break;
                    }

                    Data.ValidSize += chunkSize + 0xC;
                }
                while (Data.ValidSize < Data.FileSize);

                if (Data.Chunks.Items[Data.Chunks.Items.Count-1].Type != "IEND")
                    IssueModel.Add ("Missing 'IEND' chunk.");

                if (Data.Width <= 0 || Data.Height <= 0)
                    IssueModel.Add ("Invalid dimensions.");

                if (Data.BitDepth != 1 && Data.BitDepth != 2 && Data.BitDepth != 4 && Data.BitDepth != 8 && Data.BitDepth != 16)
                    IssueModel.Add ($"Invalid bit depth '{Data.BitDepth}'.");

                if (Data.CompressionMethod != 0)
                    IssueModel.Add ($"Invalid compression '{Data.CompressionMethod}'.");

                if (Data.FilterMethod != 0)
                    IssueModel.Add ($"Invalid filter '{Data.FilterMethod}'.");

                if (Data.InterlaceMethod != 0 && Data.InterlaceMethod != 1)
                    IssueModel.Add ($"Invalid interlace '{Data.InterlaceMethod}'.");
            }


            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                if ((hashFlags & Hashes.Intrinsic) != 0 && Data.BadCrcCount == null)
                {
                    Data.BadCrcCount = 0;
                    var hasher = new Crc32rHasher();
                    int pos = 12;
                    for (int ix = 0; ix < Data.Chunks.Items.Count; ++ix)
                    {
                        PngChunk chunk = Data.Chunks.Items[ix];
                        hasher.Append (Data.fBuf, pos, (int) chunk.Size + 4);
                        byte[] hash = hasher.GetHashAndReset();
                        UInt32 actualCRC = BitConverter.ToUInt32 (hash, 0);
                        ChunksModel.SetActualCRC (ix, actualCRC);

                        if (actualCRC != chunk.StoredCRC)
                            ++Data.BadCrcCount;
                        pos += (int) chunk.Size + 12;
                    }

                    var sb = new StringBuilder();
                    sb.Append ("CRC checks on ");

                    if (Data.BadCrcCount != 0)
                    {
                        sb.Append (Data.BadCrcCount);
                        sb.Append (" of ");
                        sb.Append (Data.Chunks.Items.Count);
                        sb.Append (" chunks failed.");
                    }
                    else
                    {
                        sb.Append (Data.Chunks.Items.Count);
                        sb.Append (" chunks successful.");
                    }

                    Data.CdIssue = IssueModel.Add (sb.ToString(), Data.BadCrcCount==0? Severity.Noise : Severity.Error,
                                                                  Data.BadCrcCount==0? IssueTags.Success : IssueTags.Failure);
                }

                base.CalcHashes (hashFlags, validationFlags);
            }
        }


        public readonly PngChunk.Vector Chunks;
        private readonly ObservableCollection<string> texts;
        public ReadOnlyObservableCollection<string> Texts { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte BitDepth { get; private set; }
        public byte ColorType { get; private set; }
        public byte CompressionMethod { get; private set; }
        public byte FilterMethod { get; private set; }
        public byte InterlaceMethod { get; private set; }
        public float? Gamma { get; private set; }
        public int? BadCrcCount { get; private set; }

        public string Dimensions => Width.ToString() + "x" + Height;
        public int ChunkCount => Chunks.Items.Count;
        public int? GoodChunkCount => BadCrcCount == null ? (int?) null : Chunks.Items.Count - BadCrcCount.Value;
        public override bool IsBadData => BadCrcCount != null && BadCrcCount.Value != 0;

        public string ColorTypeText
        {
            get
            {
                if (ColorType == 1) return "Palette";
                else if (ColorType == 2) return "Color";
                else if (ColorType == 4) return "Alpha";
                else if (ColorType == 6) return "Color+alpha";
                else return ColorType.ToString();
            }
        }

        public Issue CdIssue { get; private set; }

        public PngFormat (Model model, Stream stream, string path) : base (model, stream, path)
        {
            this.BadCrcCount = null;
            this.Chunks = model.ChunksModel.Data;
            this.texts = new ObservableCollection<string>();
            this.Texts = new ReadOnlyObservableCollection<string> (this.texts);
        }

        public override void GetReportDetail (IList<string> report)
        {
            report.Add ($"Dimensions = {Dimensions}");
            report.Add ($"Color type = {ColorType} ({ColorTypeText})");
            report.Add ("Gamma = " + (Gamma == null? "None" : Gamma.ToString()));
            report.Add ($"Bit depth = {BitDepth}");
            report.Add ($"Interlace method = {InterlaceMethod}");

            if (Texts.Count > 0)
            {
                report.Add (String.Empty);
                report.Add ("Text:");
                foreach (string text in Texts)
                    report.Add ("  " + text);
            }

            report.Add (String.Empty);
            var sb = new StringBuilder();
            int num = 0;
            foreach (PngChunk chunk in Chunks.Items)
            {
                ++num;
                sb.Clear();
                sb.Append ("Chunk ");
                sb.Append (num.ToString());
                sb.AppendLine (":");
                sb.Append ("  type = ");
                sb.AppendLine (chunk.Type);
                sb.Append ("  size = ");
                sb.AppendLine (chunk.Size.ToString());
                sb.Append ($"  stored CRC-32 = 0x{chunk.StoredCRC:X8}");
                sb.AppendLine();
                sb.Append ("  actual CRC-32 = ");
                if (chunk.ActualCRC == null)
                    sb.Append ('?');
                else
                    sb.Append ($"0x{chunk.ActualCRC.Value:X8}");
                report.Add (sb.ToString());
            }
        }
    }
}
