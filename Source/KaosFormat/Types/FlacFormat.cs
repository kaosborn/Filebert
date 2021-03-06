﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using KaosIssue;
using KaosCrypto;

namespace KaosFormat
{
    // xiph.org/flac/format.html
    public partial class FlacFormat : FormatBase
    {
        public static string[] SNames => new string[] { "flac" };
        public override string[] Names => SNames;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (hdr.Length >= 12
                    && hdr[0]=='f' && hdr[1]=='L' && hdr[2]=='a' && hdr[3]=='C' && hdr[4]==0)
                return new Model (stream, hdr, path);
            return null;
        }


        public new class Model : FormatBase.Model
        {
            public new readonly FlacFormat Data;

            public Model (Stream stream, byte[] hdr, string path)
            {
                base._data = Data = new FlacFormat (this, stream, path);

                Data.MetadataBlockStreamInfoSize = ConvertTo.FromBig24ToInt32 (hdr, 5);
                if (Data.MetadataBlockStreamInfoSize < 34)
                {
                    IssueModel.Add ($"Bad metablock size of {Data.MetadataBlockStreamInfoSize}", Severity.Fatal);
                    return;
                }

                var bb = new byte[Data.MetadataBlockStreamInfoSize];
                Data.ValidSize = 8;

                Data.fbs.Position = Data.ValidSize;
                var got = Data.fbs.Read (bb, 0, Data.MetadataBlockStreamInfoSize);
                if (got != Data.MetadataBlockStreamInfoSize)
                {
                    IssueModel.Add ("File truncated", Severity.Fatal);
                    return;
                }

                Data.MinBlockSize = ConvertTo.FromBig16ToInt32 (bb, 0);
                Data.MinBlockSize = ConvertTo.FromBig16ToInt32 (bb, 2);
                Data.MinFrameSize = ConvertTo.FromBig24ToInt32 (bb, 4);
                Data.MaxFrameSize = ConvertTo.FromBig24ToInt32 (bb, 7);

                Data.MetaSampleRate = bb[10] << 12 | bb[11] << 4 | bb[12] >> 4;
                Data.ChannelCount = ((bb[12] & 0x0E) >> 1) + 1;
                Data.BitsPerSample = (((bb[12] & 1) << 4) | (bb[13] >> 4)) + 1;
                Data.TotalSamples = ((bb[13] & 0x0F) << 32) | bb[14] << 24 | bb[15] << 16 | bb[16] << 8 | bb[17];

                Data.storedAudioDataMD5 = new byte[16];
                Array.Copy (bb, 18, Data.storedAudioDataMD5, 0, 16);

                Data.ValidSize += Data.MetadataBlockStreamInfoSize;

                for (;;)
                {
                    bb = new byte[12];
                    try
                    {
                        Data.fbs.Position = Data.ValidSize;
                    }
                    catch (EndOfStreamException)
                    {
                        IssueModel.Add ("File truncated near meta data", Severity.Fatal);
                        return;
                    }

                    Data.fbs.Position = Data.ValidSize;
                    got = Data.fbs.Read (bb, 0, 4);
                    if (got != 4)
                    {
                        IssueModel.Add ("File truncated near meta data", Severity.Fatal);
                        return;
                    }

                    if (bb[0] == 0xFF)
                        break;

                    int blockSize = ConvertTo.FromBig24ToInt32 (bb, 1);
                    Data.ValidSize += 4;

                    switch ((FlacBlockType) (bb[0] & 0x7F))
                    {
                        case FlacBlockType.Padding:
                            Data.Blocks.AddPad ((int) Data.ValidSize, blockSize);
                            break;
                        case FlacBlockType.Application:
                            got = Data.fbs.Read (bb, 0, 4);
                            if (got != 4)
                            {
                                IssueModel.Add ("File truncated near tags", Severity.Fatal);
                                return;
                            }
                            int appId = ConvertTo.FromBig32ToInt32 (bb, 0);
                            Data.Blocks.AddApp ((int) Data.ValidSize, blockSize, appId);
                            break;
                        case FlacBlockType.SeekTable:
                            var st = new byte[blockSize];
                            got = Data.fbs.Read (st, 0, blockSize);
                            if (got != blockSize)
                            {
                                IssueModel.Add ("File truncated near seek table", Severity.Fatal);
                                return;
                            }
                            Data.Blocks.AddSeekTable ((int) Data.ValidSize, blockSize, st);
                            break;
                        case FlacBlockType.Tags:
                            bb = new byte[blockSize];
                            Data.fbs.Position = Data.ValidSize;
                            got = Data.fbs.Read (bb, 0, blockSize);
                            if (got != blockSize)
                            {
                                IssueModel.Add("File truncated near tags", Severity.Fatal);
                                return;
                            }
                            if (Data.Blocks.Tags != null)
                                IssueModel.Add ("Contains multiple tag blocks", Severity.Error);
                            else
                                Data.Blocks.AddTags ((int) Data.ValidSize, blockSize, bb);
                            break;
                        case FlacBlockType.CueSheet:
                            var sb = new byte[284];
                            got = Data.fbs.Read (sb, 0, 284);
                            if (got != 284)
                            {
                                IssueModel.Add ("File truncated near cuesheet", Severity.Fatal);
                                return;
                            }
                            var isCD = (sb[24] & 0x80) != 0;
                            int trackCount = sb[283];
                            Data.Blocks.AddCuesheet ((int) Data.ValidSize, blockSize, isCD, trackCount);
                            break;
                        case FlacBlockType.Picture:
                            var pb = new byte[blockSize];
                            got = Data.fbs.Read (pb, 0, blockSize);
                            if (got != blockSize)
                            {
                                IssueModel.Add ("File truncated near picture", Severity.Fatal);
                                return;
                            }
                            var picType = (PicType) ConvertTo.FromBig32ToInt32 (pb, 0);
                            var mimeLen = ConvertTo.FromBig32ToInt32 (pb, 4);
                            var mime = Encoding.UTF8.GetString (pb, 8, mimeLen);
                            var descLen = ConvertTo.FromBig32ToInt32 (pb, mimeLen + 8);
                            var desc = Encoding.UTF8.GetString (pb, mimeLen+12, descLen);
                            var width = ConvertTo.FromBig32ToInt32 (pb, mimeLen + descLen + 12);
                            var height = ConvertTo.FromBig32ToInt32 (pb, mimeLen + descLen + 16);
                            Data.Blocks.AddPic ((int) Data.ValidSize, blockSize, picType, width, height);
                            break;
                        case FlacBlockType.Invalid:
                            IssueModel.Add ("Encountered invalid block type", Severity.Fatal);
                            return;
                        default:
                            IssueModel.Add ($"Encountered reserved block type '{bb[0]}'", Severity.Warning);
                            break;
                    }

                    Data.ValidSize += blockSize;
                }

                try
                {
                    Data.fbs.Position = Data.ValidSize;
                }
                catch (EndOfStreamException)
                {
                    IssueModel.Add ("File truncated near frame header", Severity.Fatal);
                    return;
                }
                got = Data.fbs.Read (bb, 0, 4);
                if (got != 4)
                {
                    IssueModel.Add ("File truncated", Severity.Fatal);
                    return;
                }

                // Detect frame header sync code
                if (bb[0] != 0xFF || (bb[1] & 0xFC) != 0xF8)
                {
                    IssueModel.Add ("Audio data not found", Severity.Fatal);
                    return;
                }

                Data.mediaPosition = Data.ValidSize;

                Data.SampleOrFrameNumber = Data.fbs.ReadWobbly (out byte[] wtfBuf);
                if (Data.SampleOrFrameNumber < 0)
                {
                    IssueModel.Add ("File truncated or badly formed sample/frame number.", Severity.Fatal);
                    return;
                }
                Array.Copy (wtfBuf, 0, bb, 4, wtfBuf.Length);
                int bPos = 4 + wtfBuf.Length;

                Data.RawBlockingStrategy = bb[1] & 1;

                Data.RawBlockSize = bb[2] >> 4;
                if (Data.RawBlockSize == 0)
                    Data.BlockSize = 0;
                else if (Data.RawBlockSize == 1)
                    Data.BlockSize = 192;
                else if (Data.RawBlockSize >= 2 && Data.RawBlockSize <= 5)
                    Data.BlockSize = 576 * (1 << (Data.RawBlockSize - 2));
                else if (Data.RawBlockSize == 6)
                {
                    got = Data.fbs.Read (bb, bPos, 1);
                    Data.BlockSize = bb[bPos] + 1;
                    bPos += 1;
                }
                else if (Data.RawBlockSize == 7)
                {
                    got = Data.fbs.Read (bb, bPos, 2);
                    Data.BlockSize = (bb[bPos]<<8) + bb[bPos+1] + 1;
                    bPos += 2;
                }
                else
                    Data.BlockSize = 256 * (1 << (Data.RawBlockSize - 8));


                Data.RawSampleRate = bb[2] & 0xF;
                if (Data.RawSampleRate == 0xC)
                {
                    got = Data.fbs.Read (bb, bPos, 1);
                    Data.SampleRateText = bb[bPos] + "kHz";
                    bPos += 1;
                }
                else if (Data.RawSampleRate == 0xD || Data.RawSampleRate == 0xE)
                {
                    got = Data.fbs.Read (bb, bPos, 2);
                    Data.SampleRateText = (bb[bPos]<<8).ToString() + bb[bPos+1] + (Data.RawSampleRate == 0xD? " Hz" : " kHz");
                    bPos += 2;
                }
                else if (Data.RawSampleRate == 0)
                    Data.SampleRateText = Data.MetaSampleRate.ToString() + " Hz";
                else
                    Data.SampleRateText = SampleRateMap[Data.RawSampleRate];

                Data.RawChannelAssignment = bb[3] >> 4;

                Data.RawSampleSize = (bb[3] & 0xE) >> 1;
                if (Data.RawSampleSize == 0)
                    Data.SampleSizeText = Data.BitsPerSample.ToString() + " bits";
                else
                    Data.SampleSizeText = SampleSizeMap[Data.RawSampleSize];

                Data.aHdr = new byte[bPos];
                Array.Copy (bb, Data.aHdr, bPos);

                Data.ValidSize += bPos;
                Data.fbs.Position = Data.ValidSize;
                int octet = Data.fbs.ReadByte();
                if (octet < 0)
                {
                    IssueModel.Add ("File truncated near CRC-8", Severity.Fatal);
                    return;
                }
                Data.StoredAudioHeaderCRC8 = (Byte) octet;

                try
                {
                    Data.fbs.Position = Data.mediaPosition;
                }
                catch (EndOfStreamException)
                {
                    IssueModel.Add ("File truncated near audio data", Severity.Fatal);
                    return;
                }

                try
                {
                    Data.fbs.Position = Data.FileSize - 2;
                }
                catch (EndOfStreamException)
                {
                    IssueModel.Add ("File truncated looking for end", Severity.Fatal);
                    return;
                }

                bb = new byte[2];
                if (Data.fbs.Read (bb, 0, 2) != 2)
                {
                    IssueModel.Add ("Read failed on audio block CRC-16", Severity.Fatal);
                    return;
                }

                Data.StoredAudioBlockCRC16 = (UInt16) (bb[0] << 8 | bb[1]);
                Data.MediaCount = Data.FileSize - Data.mediaPosition;

                GetDiagnostics();
            }

            private void GetDiagnostics()
            {
                if (Data.MetadataBlockStreamInfoSize != 0x22)
                    IssueModel.Add ($"Unexpected Metadata block size of {Data.MetadataBlockStreamInfoSize}", Severity.Advisory);

                if (Data.MinBlockSize < 16)
                    IssueModel.Add ("Minimum block size too low");

                if (Data.MinBlockSize > 65535)
                    IssueModel.Add ("Maximum block size too high");

                if (Data.RawSampleRate == 0xF)
                    IssueModel.Add ("Invalid sample rate");

                if (Data.RawSampleSize == 3 || Data.RawSampleSize == 7)
                    IssueModel.Add ($"Use of sample size index {Data.RawSampleSize} is reserved");

                if (Data.RawChannelAssignment >= 0xB)
                    IssueModel.Add ($"Use of reserved (undefined) channel assignment {Data.RawChannelAssignment}", Severity.Warning);

                if (Data.RawBlockSize == 0)
                    IssueModel.Add ("Block size index 0 use is reserved", Severity.Warning);

                if (Data.RawSampleRate == 0xF)
                    IssueModel.Add ("Sample rate index 15 use is invalid");

                if (Data.RawChannelAssignment >= 0xB)
                    IssueModel.Add ($"Channel index {Data.RawChannelAssignment} use is reserved", Severity.Warning);

                if (Data.Blocks.Tags.Lines.Count != Data.Blocks.Tags.StoredTagCount)
                    IssueModel.Add ("Stored tag count wrong");

                foreach (var lx in Data.Blocks.Tags.Lines)
                {
                    if (lx.IndexOf ('=') < 0)
                        IssueModel.Add ("Invalid tag line: " + lx);

                    // U+FFFD is substituted by .NET when malformed utf8 encountered.
                    if (lx.Contains ('\uFFFD'))
                        IssueModel.Add ("Tag with malformed UTF-8 character encoding: " + lx);

                    if (lx.Any (cu => Char.IsSurrogate (cu)))
                        IssueModel.Add ("Tag contains character(s) beyond the basic multilingual plane (may cause player issues): " + lx, Severity.Trivia);
                }

                int picPlusPadSize = Data.Blocks.Items.Where (b => b.BlockType == FlacBlockType.Padding || b.BlockType == FlacBlockType.Picture).Sum (b => b.Size);
                int bloat = picPlusPadSize - MaxPicPlusPadSize;
                if (bloat > 0)
                {
                    var msg = $"Artwork+padding consume {picPlusPadSize} bytes.";

                    repairPadSize = Data.Blocks.PadBlock.Size - bloat;
                    if (repairPadSize < 0)
                        IssueModel.Add (msg, Severity.Trivia, IssueTags.StrictErr);
                    else
                    {
                        if (repairPadSize > 4096)
                            repairPadSize = 4096;
                        IssueModel.Add (msg, Severity.Trivia, IssueTags.StrictWarn,
                            $"Trim {Data.Blocks.PadBlock.Size-repairPadSize} bytes of excess padding",
                            RepairArtPadBloat);
                    }
                }
            }

            private int repairPadSize=-1;
            public string RepairArtPadBloat (bool isFinalRepair)
            {
                if (Data.fbs == null || Data.Issues.MaxSeverity >= Severity.Error || repairPadSize < 0)
                    return "Invalid attempt";

                string err = null;
                var padBlock = Data.Blocks.PadBlock;
                var delCount = padBlock.Size - repairPadSize;

                try
                {
                    var padHdr = new byte[3];
                    padHdr[0] = (byte) (repairPadSize >> 16);
                    padHdr[1] = (byte) ((repairPadSize >> 8) & 0xFF);
                    padHdr[2] = (byte) (repairPadSize & 0xFF);

                    var part2 = new byte[(int) Data.FileSize - padBlock.NextPosition];
                    Data.fbs.Position = padBlock.NextPosition;
                    int got = Data.fbs.Read (part2, 0, part2.Length);
                    if (got != part2.Length)
                        return "Read error.";

                    Data.fbs.Position = padBlock.Position - 3;
                    Data.fbs.Write (padHdr, 0, 3);
                    Data.fbs.Position = padBlock.Position + repairPadSize;
                    Data.fbs.Write (part2, 0, part2.Length);
                    Data.fbs.SetLength (Data.FileSize - delCount);

                    if (isFinalRepair)
                        CloseFile();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                { err = ex.Message.TrimEnd(null); }

                return err;
            }

            private static Process StartFlac (string name)
            {
                var px = new Process();
                px.StartInfo.UseShellExecute = false;
                px.StartInfo.RedirectStandardOutput = true;
                px.StartInfo.CreateNoWindow = true;
                px.StartInfo.Arguments = "-d -c -f --totally-silent --force-raw-format --endian=little --sign=signed " + '"' + name + '"';
                px.StartInfo.FileName = "flac";
                var isGo = px.Start();
                return px;
            }

            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                if (Data.Issues.HasFatal)
                    return;

                if ((hashFlags & Hashes.Intrinsic) != 0 && Data.ActualAudioHeaderCRC8 == null)
                {
                    var hasher1 = new Crc8Hasher();
                    hasher1.Append (Data.aHdr);
                    byte[] hash1 = hasher1.GetHashAndReset();
                    Data.ActualAudioHeaderCRC8 = hash1[0];

                    try
                    {
                        var hasher2 = new Crc16nHasher();
                        hasher2.Append (Data.fbs, Data.mediaPosition, Data.FileSize - Data.mediaPosition - 2);
                        byte[] hash2 = hasher2.GetHashAndReset();
                        Data.ActualAudioBlockCRC16 = BitConverter.ToUInt16 (hash2, 0);
                    }
                    catch (EndOfStreamException ex)
                    {
                        IssueModel.Add ("Read failed while checking audio CRC: " + ex.Message, Severity.Fatal);
                        return;
                    }

                    if (! Data.IsBadDataCRC && ! Data.IsBadHeaderCRC)
                        Data.ChIssue = Data.CdIssue = IssueModel.Add ("CRC checks successful on audio header and data.", Severity.Noise, IssueTags.Success);
                    else
                    {
                        if (Data.IsBadHeaderCRC)
                            Data.ChIssue = IssueModel.Add ("CRC-8 check failed on audio header.", Severity.Error, IssueTags.Failure);
                        else
                            Data.ChIssue = IssueModel.Add ("CRC-8 check successful on audio header.", Severity.Noise, IssueTags.Success);

                        if (Data.IsBadDataCRC)
                            Data.CdIssue = IssueModel.Add ("CRC-16 check failed on audio data.", Severity.Error, IssueTags.Failure);
                        else
                            Data.CdIssue = IssueModel.Add ("CRC-16 check successful on audio data.", Severity.Noise, IssueTags.Success);
                    }
                }

                if ((hashFlags & Hashes.PcmMD5) != 0 && Data.actualAudioDataMD5 == null)
                {
                    Process px = null;
                    try
                    { px = StartFlac (Data.Path); }
                    catch (Exception ex)
                    { IssueModel.Add ("flac executable failed with '" + ex.Message.Trim (null) + "'."); }

                    if (px != null)
                        using (var br = new BinaryReader (px.StandardOutput.BaseStream))
                        {
                            try
                            {
                                var hasher = new Md5Hasher();
                                hasher.Append (br);
                                var hash = hasher.GetHashAndReset();
                                Data.actualAudioDataMD5 = hash;
                            }
                            catch (EndOfStreamException ex)
                            { IssueModel.Add ("Read failed while verifying audio MD5: " + ex.Message, Severity.Fatal); }

                            if (Data.IsBadDataMD5)
                                Data.CmIssue = IssueModel.Add ("MD5 check failed on audio data.", Severity.Error, IssueTags.Failure);
                            else
                                Data.CmIssue = IssueModel.Add ("MD5 check successful on audio data.", Severity.Noise, IssueTags.Success);
                        }
                }

                if ((hashFlags & (Hashes.PcmCRC32|Hashes._FlacMatch)) != 0 && Data.ActualPcmCRC32 == null)
                {
                    Process px = null;
                    try
                    { px = StartFlac (Data.Path); }
                    catch (Exception ex)
                    { IssueModel.Add ("flac executable failed with '" + ex.Message.Trim (null) + "'."); }

                    if (px != null)
                        using (var br = new BinaryReader (px.StandardOutput.BaseStream))
                        {
                            var hasher = new Crc32rHasher();
                            hasher.Append (br);
                            byte[] hash = hasher.GetHashAndReset();
                            Data.ActualPcmCRC32 = BitConverter.ToUInt32 (hash, 0);
                        }
                }

                if ((hashFlags & (Hashes._FlacTags)) != 0)
                {
                    TagCheckNumber ("TRACKNUMBER");
                    TagCheckNumber ("TRACKTOTAL", optional:true);
                    TagCheckNumber ("DISCNUMBER", true);
                    TagCheckNumber ("DISCTOTAL", true);
                    TagCheckDate ("DATE");
                    TagCheckDate ("RELEASE DATE", optional:true);
                    TagCheckDate ("ORIGINAL RELEASE DATE", true);
                    TagCheckText ("TITLE");
                    TagCheckText ("ARTIST");
                    TagCheckText ("ALBUM");
                    TagCheckText ("ALBUMARTIST", optional:true);
                    TagCheckText ("ALBUMARTISTSORTORDER", true);
                    TagCheckText ("ORGANIZATION", true);
                    TagCheckText ("BARCODE", true);
                    TagCheckText ("CATALOGNUMBER", true);
                }

                base.CalcHashes (hashFlags, validationFlags);
            }

            private void TagCheckNumber (string key, bool optional=false)
            {
                string value = Data.GetTagValue (key);
                if (value == null)
                {
                    if (! optional)
                        IssueModel.Add (key + " tag is missing.", Severity.Warning, IssueTags.BadTag|IssueTags.StrictErr);
                }
                else if (value.Length == 0 || ! Char.IsDigit (value[0]))
                    IssueModel.Add (key + " tag is not a number.", Severity.Warning, IssueTags.BadTag|IssueTags.StrictErr);
            }

            private void TagCheckDate (string key, bool optional=false)
            {
                string value = Data.GetTagValue (key);
                if (value == null)
                {
                    if (! optional)
                        IssueModel.Add (key + " tag is missing.", Severity.Warning, IssueTags.BadTag|IssueTags.StrictErr);
                }
                else if ((value.Length != 4 && value.Length != 10) || (! value.StartsWith ("19") && ! value.StartsWith ("20"))
                                                                   || ! Char.IsDigit (value[2]) || ! Char.IsDigit (value[3]))
                    IssueModel.Add (key + " tag not like YYYY or YYYY-MM-DD with YYYY in 1900 to 2099.",
                                    Severity.Warning, IssueTags.BadTag|IssueTags.StrictErr);
            }

            private void TagCheckText (string key, bool optional=false)
            {
                string err = null;
                string value = Data.GetTagValue (key);
                if (String.IsNullOrEmpty (value))
                {
                    if (! optional)
                        err = key + " tag is missing";
                }
                else
                {
                    if (value.StartsWith (" "))
                        err = key + " tag has leading space";
                    if (value.EndsWith (" "))
                        err = (err == null ? key + " tag has" : err + ',') + " trailing space";
                    if (value.Contains ("  "))
                        err = (err == null ? key + " tag has" : err + ',') + " adjacent spaces";
                }
                if (err != null)
                    IssueModel.Add (err + ".", Severity.Warning, IssueTags.BadTag|IssueTags.StrictErr);
            }
        }


        private static string[] SampleRateMap =
        { "g0000", "88.2kHz", "176.4kHz", "192kHz", "8kHz", "16kHz", "22.05kHz", "24kHz",
          "32kHz", "44.1kHz", "48kHz", "96kHz", "g1100", "g1101", "g1110", "invalid" };

        private static string[] SampleSizeMap =
        { "getIt", "8 bits", "12 bits", "reserved",
          "16 bits", "20 bits", "24 bits", "reserved" };

        private static string[] ChannelAssignmentMap =
        {
            "mono", "L,R", "L,R,C", "FL,FR,BL,BR", "FL,FR,FC,BL,BR", "FL,FR,FC,LFE,BL,BR",
            "FL,FR,FC,LFE,BC,SL,SR", "FL,FR,FC,LFE,BL,BR,SL,SR",
            "left/side stereo", "right/side stereo", "mid/side stereo",
            "R1011", "R1100", "R1101", "R1111"
        };

        static public int MaxPicPlusPadSize { get; private set; } = 1024 * 1024;
        static public void SetMaxPicPlusPadSize (int maxSize)
        {
            if (maxSize >= 0)
                MaxPicPlusPadSize = maxSize;
        }

        private byte[] aHdr = null;

        public FlacBlockList Blocks { get; private set; } = new FlacBlockList();

        public int MetadataBlockStreamInfoSize { get; private set; }
        public int MinBlockSize { get; private set; }
        public int MaxBlockSize { get; private set; }
        public int MinFrameSize { get; private set; }
        public int MaxFrameSize { get; private set; }
        public int MetaSampleRate { get; private set; }
        public int ChannelCount { get; private set; }
        public int BitsPerSample { get; private set; }
        public long TotalSamples { get; private set; }
        public long SampleOrFrameNumber { get; private set; }

        public int RawBlockingStrategy { get; private set; }
        public string BlockingStrategyText => (RawBlockingStrategy == 0 ? "Fixed" : "Variable") + " size";
        public int RawBlockSize { get; private set; }
        public int BlockSize { get; private set; }
        public int RawSampleRate { get; private set; }
        public string SampleRateText { get; private set; }
        public int RawChannelAssignment { get; private set; }
        public string ChannelAssignmentText => ChannelAssignmentMap[RawChannelAssignment];
        public int RawSampleSize { get; private set; }
        public string SampleSizeText { get; private set; }

        public Byte StoredAudioHeaderCRC8 { get; private set; }
        public Byte? ActualAudioHeaderCRC8 { get; private set; }
        public string StoredAudioHeaderCRC8ToHex => StoredAudioHeaderCRC8.ToString ("X2");
        public string ActualAudioHeaderCRC8ToHex => ActualAudioHeaderCRC8?.ToString ("X2");

        public UInt16 StoredAudioBlockCRC16 { get; private set; }
        public UInt16? ActualAudioBlockCRC16 { get; private set; }
        public string StoredAudioBlockCRC16ToHex => StoredAudioBlockCRC16.ToString ("X4");
        public string ActualAudioBlockCRC16ToHex => ActualAudioBlockCRC16?.ToString ("X4");

        private byte[] storedAudioDataMD5 = null;
        private byte[] actualAudioDataMD5 = null;
        public string StoredAudioDataMD5ToHex => storedAudioDataMD5==null ? null : ConvertTo.ToHexString (storedAudioDataMD5);
        public string ActualAudioDataMD5ToHex => actualAudioDataMD5==null ? null : ConvertTo.ToHexString (actualAudioDataMD5);

        public UInt32? ActualPcmCRC32 { get; private set; }
        public string ActualPcmCRC32ToHex => ActualPcmCRC32?.ToString ("X8");

        public bool IsBadDataMD5 => actualAudioDataMD5 != null && ! actualAudioDataMD5.SequenceEqual (storedAudioDataMD5);
        public bool IsBadDataCRC => ActualAudioBlockCRC16 != null && ActualAudioBlockCRC16.Value != StoredAudioBlockCRC16;
        public bool IsBadHeaderCRC => ActualAudioHeaderCRC8 != null && ActualAudioHeaderCRC8.Value != StoredAudioHeaderCRC8;

        public override bool IsBadHeader => IsBadHeaderCRC;
        public override bool IsBadData => IsBadDataCRC || IsBadDataMD5;

        public Issue ChIssue { get; private set; }
        public Issue CdIssue { get; private set; }
        public Issue CmIssue { get; private set; }

        private FlacFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }

        public string GetTagValue (string key)
        {
            key = key.ToLower() + "=";
            foreach (var item in Blocks.Tags.Lines)
                if (item.ToLower().StartsWith (key))
                    return item.Substring (key.Length);
            return null;
        }

        public string GetMultiTagValues (string key)
        {
            string result = null;
            key = key.ToLower() + "=";
            foreach (var item in Blocks.Tags.Lines)
                if (item.ToLower().StartsWith (key))
                    if (result == null)
                        result = item.Substring (key.Length);
                    else
                        result += @"\\" + item.Substring (key.Length);
            return result;
        }

        public static bool? IsFlacTagsAllSame (IList<FlacFormat> flacs, string key)
        {
            if (flacs.Count == 0)
                return null;

            var val0 = flacs[0].GetTagValue (key);
            if (! String.IsNullOrEmpty (val0))
                return flacs.All (x => x.GetTagValue (key) == val0);

            var isAllEmpty = flacs.All (f => String.IsNullOrEmpty (f.GetTagValue (key)));
            if (isAllEmpty)
                return null;

            return false;
        }

        public static bool? IsFlacMultiTagAllSame (IList<FlacFormat> flacs, string key)
        {
            if (flacs.Count == 0)
                return null;

            string values = flacs[0].GetMultiTagValues (key);
            if (values != null)
            {
                for (int ix = 1; ix < flacs.Count; ++ix)
                    if (flacs[ix].GetMultiTagValues (key) != values)
                        return false;
                return true;
            }

            for (int ix = 1; ix < flacs.Count; ++ix)
                if (flacs[ix].GetTagValue (key) != null)
                    return false;

            return null;
        }


        private string layout = null;
        public string Layout
        {
            get
            {
                if (layout == null)
                {
                    var sb = new StringBuilder ("|");
                    foreach (var item in Blocks.Items)
                    {
                        sb.Append (' ');
                        sb.Append (item.Name);
                        sb.Append (" (");
                        sb.Append (item.Size);
                        sb.Append (") |");
                    }

                    if (aHdr != null)
                    {
                        sb.Append (" Audio (");
                        sb.Append (MediaCount);
                        sb.Append (") |");
                    }
                    layout = sb.ToString();
                }
                return layout;
            }
        }

        public override void GetReportDetail (IList<string> report)
        {
            if (report.Count > 0)
                report.Add (String.Empty);

            report.Add ("Meta header:");

            report.Add ($"  Minimum block size = {MinBlockSize}");
            report.Add ($"  Maximum block size = {MaxBlockSize}");
            report.Add ($"  Minimum frame size = {MinFrameSize}");
            report.Add ($"  Maximum frame size = {MaxFrameSize}");

            report.Add ($"  Sample rate = {MetaSampleRate} Hz");
            report.Add ($"  Number of channels = {ChannelCount}");
            report.Add ($"  Bits per sample = {BitsPerSample}");

            report.Add ("  Total samples = " + (TotalSamples != 0? TotalSamples.ToString() : " (unknown)"));

            report.Add (String.Empty);
            report.Add ("Raw audio header: " + ConvertTo.ToBitString (aHdr, 1));

            report.Add (String.Empty);
            report.Add ("Cooked audio header:");
            report.Add ($"  Blocking strategy = {BlockingStrategyText}");
            report.Add ($"  Block size = {BlockSize} samples");
            report.Add ($"  Sample rate = {SampleRateText}");
            report.Add ($"  Channel assignment = {ChannelAssignmentText}");
            report.Add ($"  Sample size = {SampleSizeText}");
            report.Add ($"  Sample/frame number = {SampleOrFrameNumber}");

            report.Add (String.Empty);
            report.Add ("Checks:");

            report.Add ($"  Stored audio header CRC-8 = {StoredAudioHeaderCRC8ToHex}");
            if (ActualAudioHeaderCRC8 != null)
                report.Add ($"  Actual audio header CRC-8 = {ActualAudioHeaderCRC8ToHex}");

            report.Add ($"  Stored audio block CRC-16 = {StoredAudioBlockCRC16ToHex}");
            if (ActualAudioBlockCRC16 != null)
                report.Add ($"  Actual audio block CRC-16 = {ActualAudioBlockCRC16ToHex}");

            report.Add ($"  Stored PCM MD5 = {StoredAudioDataMD5ToHex}");
            if (actualAudioDataMD5 != null)
                report.Add ($"  Actual PCM MD5 = {ActualAudioDataMD5ToHex}");

            if (ActualPcmCRC32 != null)
                report.Add ($"  Actual PCM CRC-32 = {ActualPcmCRC32ToHex}");

            report.Add (String.Empty);
            report.Add ($"Layout = {Layout}");

            if (Blocks.Tags != null)
            {
                report.Add (String.Empty);
                report.Add ("Tags:");
                report.Add ($"  Vendor: {Blocks.Tags.Vendor}");
                foreach (var item in Blocks.Tags.Lines)
                    report.Add ($"  {item}");
            }
        }
    }
}
