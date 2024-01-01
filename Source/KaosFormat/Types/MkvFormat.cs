﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using KaosIssue;
using KaosCrypto;

// Specs:
// www.matroska.org/technical/specs/index.html

namespace KaosFormat
{
    public static partial class StringBuilderExtensions
    {
        public static StringBuilder AppendEbmlStack (this StringBuilder sb, Stack<EbmlStackItem> stack)
        {
            var nameList = new List<string>();
            foreach (var item in stack)
                nameList.Add (item.Node.Element.Name);

            int ix = nameList.Count;
            if (ix > 0)
                for (;;)
                {
                    --ix;
                    sb.Append (nameList[ix]);
                    if (ix == 0)
                        break;
                    sb.Append (' ');
                }
            return sb;
        }
    }

    public enum EbmlType { Master, Unsigned, Signed, Ascii, Utf8, Binary, Float, Date };

    [Flags]
    public enum ParseFlag { None=0, Persist=1, PrunePayload=2, L0=0x10 };

    public class EbmlSig
    {
        public string Name { get; private set; }
        public EbmlType Type { get; private set; }
        public ParseFlag Flag { get; private set; }

        private readonly byte[] sig;
        public ReadOnlyCollection<byte> Signature { get; private set; }
        public uint Sig32 { get; private set; }

        public EbmlSig (byte[] sig, string name, ParseFlag flag=ParseFlag.None, EbmlType type=EbmlType.Master)
        {
            this.Name = name;
            this.Type = type;
            this.Flag = flag;

            this.sig = sig;
            this.Signature = new ReadOnlyCollection<byte> (this.sig);

            this.Sig32 = this.sig[0];
            for (var bx = 1; bx < this.sig.Length; ++bx)
                this.Sig32 = this.Sig32 << 8 | this.sig[bx];
        }

        public EbmlSig (byte[] sig, EbmlType type, string name, ParseFlag flag=ParseFlag.None) : this (sig, name, flag, type)
        { }

        public bool SigIsStartOf (byte[] arg)
        {
            System.Diagnostics.Debug.Assert (arg.Length >= sig.Length);
            return sig[0]==arg[0] && (sig.Length==1
                || sig[1]==arg[1] && (sig.Length==2
                || sig[2]==arg[2] && (sig.Length==3
                || sig[3]==arg[3])));
        }
    }


    public class EbmlStackItem
    {
        public EbmlNodeMaster Node;
        public int Index;

        public EbmlStackItem (EbmlNodeMaster node, int index=0)
        { this.Node = node; this.Index = index; }
    }


    public class EbmlNode
    {
        public EbmlSig Element { get; private set; }
        public long Size { get; protected set; }

        public EbmlNode (EbmlSig element, long size=0)
        {
            this.Size = size;
            this.Element = element;
        }
    }

    public class EbmlNodeLeaf : EbmlNode
    {
        public byte[] payload;
        public EbmlNodeLeaf (EbmlSig element, byte[] payload) : base (element, payload==null? 0 : payload.Length)
        {
            this.payload = payload;
        }

        public EbmlNodeLeaf (EbmlSig element, long size) : base (element, size)
        {
            this.payload = null;
        }
    }

    public class EbmlNodeCRC : EbmlNode
    {
        public long Start { get; private set; }
        public long Count { get; private set; }
        public uint StoredCRC32 { get; private set; }
        public uint ActualCRC32 { get; internal set; }

        public EbmlNodeCRC (EbmlSig element, byte[] payload, long start, long count) : base (element)
        {
            this.Start = start;
            this.Count = count;
            this.StoredCRC32 = ConvertTo.FromLit32ToUInt32 (payload, 0);
        }
    }

    public class EbmlNodeMaster : EbmlNode
    {
        private List<EbmlNode> nodes;
        public IList<EbmlNode> Nodes => nodes;

        public EbmlNodeMaster (EbmlSig element, long size) : base (element)
        {
            this.Size = size;
            this.nodes = new List<EbmlNode>();
        }

        public void AddMaster (EbmlSig element, long size)
        {
            var newNode = new EbmlNodeMaster (element, size);
            nodes.Add (newNode);
        }

        public void AddNode (EbmlNode node)
        {
            nodes.Add (node);
        }

        public String SeekForASCII (ulong sig32)
        {
            var node = Seek (sig32) as EbmlNodeLeaf;
            if (node == null || node.payload==null)
                return null;
            System.Diagnostics.Debug.Assert (node.Element.Type == EbmlType.Ascii);
            return Encoding.ASCII.GetString (node.payload, 0, node.payload.Length);
        }

        public long SeekForUnsigned (ulong sig32)
        {
            var node = Seek (sig32) as EbmlNodeLeaf;
            if (node == null || node.payload==null || node.payload.Length==0)
                return -1;
            System.Diagnostics.Debug.Assert (node.Element.Type == EbmlType.Unsigned);
            long result = node.payload[0];
            for (var bx = 1; bx < node.payload.Length; ++bx)
                result = result << 8 | node.payload[bx];
            return result;
        }

        public EbmlNode Seek (ulong sig32)
        {
            System.Diagnostics.Debug.Assert (this.Element.Type == EbmlType.Master);

            var stack = new Stack<EbmlStackItem>();

            for (EbmlNode current = this;;)
            {
                var top = new EbmlStackItem ((EbmlNodeMaster) current);

                for (;;)
                {
                    if (top.Index >= top.Node.nodes.Count)
                        if (stack.Count == 0)
                            return null;
                        else
                        {
                            top = stack.Pop();
                            continue;
                        }

                    current = top.Node.nodes[top.Index];
                    if (current.Element.Sig32 == sig32)
                        return current;

                    ++top.Index;
                    if (current.Element.Type == EbmlType.Master)
                    {
                        stack.Push (top);
                        break;
                    }
                }
            }
        }

        public IEnumerable<string> GetAsciis (string elementName)
        {
            var sig32 = MkvFormat.GetLeafSigVal (elementName);
            foreach (var node in GetNodes (elementName))
            {
                if (node is EbmlNodeLeaf leaf)
                    yield return Encoding.ASCII.GetString (leaf.payload, 0, leaf.payload.Length);
            }
        }

        public IEnumerable<Stack<EbmlStackItem>> GetNodeTraces (string elementName)
        {
            System.Diagnostics.Debug.Assert (this.Element.Type == EbmlType.Master);

            var stack = new Stack<EbmlStackItem>();

            for (EbmlNode current = this;;)
            {
                var top = new EbmlStackItem ((EbmlNodeMaster) current);
                stack.Push (top);

                for (;;)
                {
                    if (top.Index >= top.Node.nodes.Count)
                        if (stack.Count == 1)
                            yield break;
                        else
                        {
                            stack.Pop();
                            top = stack.Peek();
                            continue;
                        }

                    current = top.Node.nodes[top.Index];
                    if (current.Element.Name == elementName)
                        yield return stack;

                    ++top.Index;
                    if (current.Element.Type == EbmlType.Master)
                        break;
                }
            }
        }

        public IEnumerable<EbmlNode> GetNodes (string elementName)
        {
            System.Diagnostics.Debug.Assert (this.Element.Type == EbmlType.Master);

            var stack = new Stack<EbmlStackItem>();

            for (EbmlNode current = this;;)
            {
                var top = new EbmlStackItem ((EbmlNodeMaster) current);

                for (;;)
                {
                    if (top.Index >= top.Node.nodes.Count)
                        if (stack.Count == 0)
                            yield break;
                        else
                        {
                            top = stack.Pop();
                            continue;
                        }

                    current = top.Node.nodes[top.Index];
                    if (current.Element.Name == elementName)
                        yield return current;

                    ++top.Index;
                    if (current.Element.Type == EbmlType.Master)
                    {
                        stack.Push (top);
                        break;
                    }
                }
            }
        }
    }


    public class MkvFormat : FormatBase
    {
        public static string[] SNames => new string[] { "mkv", "mka", "webm" };
        public override string[] Names => SNames;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (hdr.Length >= 4 && hdr[0]==0x1A && hdr[1]==0x45 && hdr[2]==0xDF && hdr[3]==0xA3)
                return new Model (stream, path);
           return null;
        }


        public new class Model : FormatBase.Model
        {
            public new readonly MkvFormat Data;

            public Model (Stream stream, string path)
            {
                base._data = Data = new MkvFormat (this, stream, path);
                var bb = new byte[5];

                Data.fbs.Position = Data.ValidSize = 4;

                Data.root = ParseTree (rootSig);
                Data.layout.Add (Data.root);

                if (Data.Issues.HasError)
                    return;

                System.Diagnostics.Debug.Assert (Data.ValidSize == Data.fbs.Position);

                Data.fbs.Position = Data.ValidSize;
                long SegmentLength = ReadMTF (0x18, 0x53, 0x80, 0x67);
                if (SegmentLength < 0)
                { IssueModel.Add ("Missing root element.", Severity.Fatal); return; }

                System.Diagnostics.Debug.Assert (Data.ValidSize == Data.fbs.Position);

                long segmentStart = Data.ValidSize;

                var diff = Data.ValidSize + SegmentLength - Data.FileSize;
                if (diff > 0)
                    IssueModel.Add ($"File appears truncated by {diff} bytes.", Severity.Error);

                Data.segment = new EbmlNodeMaster (segmentSig, SegmentLength);
                Data.layout.Add (Data.segment);

                Data.fbs.Position = Data.ValidSize;

                while (Data.ValidSize < segmentStart + SegmentLength)
                {
                    Data.fbs.Position = Data.ValidSize;
                    int got = Data.fbs.Read (bb, 0, 4);
                    if (got < 3)
                        break;

                    if (bb[0] == voidSig.Sig32)
                    {
                        Data.fbs.Position = Data.ValidSize + 1;
                        var voidLenBuf = ReadMTF (Data.fbs, out long voidLen);
                        if (voidLen < 0)
                        { IssueModel.Add ("File truncated near void.", Severity.Fatal); return; }

                        Data.ValidSize += 1 + voidLenBuf.Length + voidLen;
                        var voidNode = new EbmlNodeLeaf (voidSig, voidLen);
                        Data.segment.AddNode (voidNode);
                        continue;
                    }

                    if (got < 4)
                        break;

                    foreach (var mstr in topSigs)
                    {
                        if (mstr.SigIsStartOf (bb))
                        {
                            Data.fbs.Position = Data.ValidSize = Data.ValidSize + 4;

                            if ((mstr.Flag & ParseFlag.PrunePayload) != 0)
                            {
                                var payloadLenBuf = ReadMTF (Data.fbs, out long payloadLen);
                                if (payloadLen < 0)
                                { IssueModel.Add ("File corrupt or truncated.", Severity.Fatal); return; }

                                Data.ValidSize += payloadLenBuf.Length + payloadLen;
                                Data.segment.AddMaster (mstr, payloadLenBuf.Length + payloadLen);
                            }
                            else
                            {
                                var newNode = ParseTree (mstr);
                                if (IssueModel.Data.HasFatal)
                                {
                                    if (newNode != null) Data.segment.AddNode (newNode);
                                    return;
                                }
                                Data.segment.AddNode (newNode);
                            }
                            goto NEXT_SEG;
                        }
                    }

                    string msg = $"Parse fail at 0x{Data.ValidSize:X} on [{bb[0]:X2}][{bb[1]:X2}][{bb[2]:X2}][{bb[3]:X2}].";
                    IssueModel.Add (msg, Severity.Fatal);
                    return;

                NEXT_SEG:;
                }

                Data.fbs.Position = Data.ValidSize;
                var got2 = Data.fbs.Read (bb, 0, 4);
                if (got2 == 4 && attachSig.SigIsStartOf (bb))
                {
                    // By spec, everything should be within a single Segment.
                    Data.HasMisplacedAttachment = true;
                    IssueModel.Add ($"Misplaced attachment at {Data.ValidSize:X}", Severity.Warning);
                }

                Data.fbs.Position = Data.ValidSize;
                CalcMark (true);
                return;
            }

            EbmlNodeMaster ParseTree (EbmlSig element)
            {
                string err = null;
                var contentLenBuf = ReadMTF (Data.fbs, out long contentLen);
                if (contentLen < 0)
                    return null;
                Data.ValidSize += contentLenBuf.Length;

                var newMaster = new EbmlNodeMaster (element, contentLenBuf.Length + contentLen);
                var buf = new byte[3];
                long stop = Data.ValidSize + contentLen;

                while (Data.ValidSize < stop)
                {
                    Data.fbs.Position = Data.ValidSize;
                    var got = Data.fbs.Read (buf, 0, 3);
                    if (got < 3)
                    { err = "File corrupt or truncated"; goto FATAL; }

                    EbmlNode newNode;

                    foreach (var item in masterSigs)
                        if (item.SigIsStartOf (buf))
                        {
                            Data.fbs.Position = Data.ValidSize = Data.ValidSize + item.Signature.Count;
                            newNode = ParseTree (item);
                            if (IssueModel.Data.HasFatal)
                            {
                                if (newNode != null) newMaster.AddNode (newNode);
                                return newMaster;
                            }
                            goto NEXT;
                        }

                    foreach (var item in leafSigs)
                    {
                        if (item.SigIsStartOf (buf))
                        {
                            Data.fbs.Position = Data.ValidSize = Data.ValidSize + item.Signature.Count;

                            byte[] payload = null;
                            byte[] payloadHdr = ReadMTF (Data.fbs, out long payloadLen);
                            if (payloadHdr == null)
                            { err = "File truncated or corrupt"; goto FATAL; }

                            if ((item.Flag & ParseFlag.Persist) != 0)
                            {
                                payload = new byte[payloadLen];
                                got = Data.fbs.Read (payload, 0, (int) payloadLen);
                            }

                            if (buf[0] != CrcSig.Sig32)
                                newNode = new EbmlNodeLeaf (item, payload);
                            else
                            {
                                ++Data.CrcCount;
                                long hashStart = Data.ValidSize + payloadHdr.Length + payloadLen;
                                long hashCount;
                                if (newMaster.Nodes.Count == 0)
                                    hashCount = contentLen - 5 - payloadHdr.Length;
                                else
                                {
                                    IssueModel.Add ("Misplaced CRC");
                                    hashCount = 0;
                                }
                                newNode = new EbmlNodeCRC (item, payload, hashStart, hashCount);
                            }

                            Data.ValidSize += payloadHdr.Length + payloadLen;
                            goto NEXT;
                        }
                    }
                    err = $"Unknown element [{buf[0]:X2}][{buf[1]:X2}][{buf[2]:X2}]";
                    goto FATAL;
                NEXT:
                    newMaster.AddNode (newNode);
                }
                if (Data.ValidSize == stop)
                    return newMaster;

                err = "Positional error";
            FATAL:
                err += String.Format (" at {0:X}.", Data.ValidSize);
                IssueModel.Add (err, Severity.Fatal);
                return newMaster;
            }

            long ReadMTF (byte m1, byte m2, byte m3, byte m4)
            {
                var buf = new byte[4];
                var got = Data.fbs.Read (buf, 0, 4);
                if (got != 4)
                    return -1;

                if (buf[0] != m1 || buf[1] != m2 || buf[2] != m3 || buf[3] != m4)
                    return -1;

                buf = ReadMTF (Data.fbs, out long result);
                if (buf != null)
                    Data.ValidSize += 4 + buf.Length;

                return result;
            }

            static byte[] ReadMTF (Stream stream, out long result)
            {
                result = -1;
                int b0 = stream.ReadByte();
                if (b0 <= 0)
                    return null;

                int len;
                if ((b0 & 0x80) != 0)
                {
                    result = b0 & 0x7F;
                    return new byte[] { (byte) b0 };
                }

                if ((b0 & 0x40) != 0)
                    len = 2;
                else if ((b0 & 0x20) != 0)
                    len = 3;
                else if ((b0 & 0x10) != 0)
                    len = 4;
                else if ((b0 & 0x08) != 0)
                    len = 5;
                else if ((b0 & 0x04) != 0)
                    len = 6;
                else if ((b0 & 0x02) != 0)
                    len = 7;
                else
                    len = 8;

                var buf = new byte[len];
                buf[0] = (byte) b0;

                int got = stream.Read (buf, 1, len-1);
                if (got != len-1)
                    return null;

                result = b0 & (0xFF >> len);
                for (int pos = 1; pos < buf.Length; ++pos)
                    result = (result << 8) + buf[pos];

                return buf;
            }

            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                if (Data.Issues.HasFatal)
                    return;

                if ((hashFlags & Hashes.Intrinsic) != 0 && Data.badCrcCount == null)
                {
                    Data.badCrcCount = 0;
                    foreach (var master in Data.layout)
                        foreach (var cx in master.GetNodeTraces ("CRC-32"))
                        {
                            var top = cx.Peek();
                            var node = top.Node.Nodes[top.Index] as EbmlNodeCRC;
                            if (node.Count > 0)
                            {
                                var hasher = new Crc32rHasher();
                                hasher.Append (Data.fbs, node.Start, node.Count);
                                var hash = hasher.GetHashAndReset();
                                node.ActualCRC32 = BitConverter.ToUInt32 (hash, 0);
                                if (node.StoredCRC32 != node.ActualCRC32)
                                    ++Data.badCrcCount;
                            }
                        }

                    if (Data.CrcCount > 0)
                        if (Data.badCrcCount == 0)
                            Data.CdIssue = IssueModel.Add ("CRC checks successful.", Severity.Noise, IssueTags.Success);
                        else
                            Data.CdIssue = IssueModel.Add ("CRC check failure.", Severity.Error, IssueTags.Failure);
                }

                base.CalcHashes (hashFlags, validationFlags);
            }
        }

        public static uint GetLeafSigVal (string elementName)
        {
            foreach (var it in leafSigs)
                if (it.Name == elementName)
                    return it.Sig32;
            return 0;
        }

        private static EbmlSig rootSig    = new EbmlSig (new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }, "EBML",    ParseFlag.L0);
        private static EbmlSig segmentSig = new EbmlSig (new byte[] { 0x18, 0x53, 0x80, 0x67 }, "Segment", ParseFlag.L0);
        private static EbmlSig attachSig  = new EbmlSig (new byte[] { 0x19, 0x41, 0xA4, 0x69 }, "Attachments");
        private static EbmlSig voidSig    = new EbmlSig (new byte[] { 0xEC }, EbmlType.Binary,  "Void");
        private static EbmlSig CrcSig     = new EbmlSig (new byte[] { 0xBF }, EbmlType.Binary,  "CRC-32", ParseFlag.Persist);

        private static EbmlSig[] topSigs =
        {
            new EbmlSig (new byte[] { 0x1F, 0x43, 0xB6, 0x75 }, "Cluster", ParseFlag.PrunePayload),
            new EbmlSig (new byte[] { 0x11, 0x4D, 0x9B, 0x74 }, "SeekHead"),
            new EbmlSig (new byte[] { 0x15, 0x49, 0xA9, 0x66 }, "Info"),
            new EbmlSig (new byte[] { 0x16, 0x54, 0xAE, 0x6B }, "Track"),
            new EbmlSig (new byte[] { 0x1C, 0x53, 0xBB, 0x6B }, "Cues"),
            new EbmlSig (new byte[] { 0x10, 0x43, 0xA7, 0x70 }, "Chapters"),
            attachSig,
            new EbmlSig (new byte[] { 0x12, 0x54, 0xC3, 0x67 }, "Tags")
        };

        private static readonly EbmlSig[] masterSigs =
        {
            // SeekHead:
            new EbmlSig (new byte[] { 0x4D, 0xBB }, "Seek"),

            // Segment:
            new EbmlSig (new byte[] { 0x69, 0x24 }, "ChapterTranslate"),

            // Cluster:
            new EbmlSig (new byte[] { 0x58, 0x54 }, "SilentTracks"),
            new EbmlSig (new byte[] { 0xA0 },       "BlockGroup"),
            new EbmlSig (new byte[] { 0x75, 0xA1 }, "BlockAdditions"),
            new EbmlSig (new byte[] { 0xA6 },       "BlockMore"),
            new EbmlSig (new byte[] { 0x8E },       "Slices"),
            new EbmlSig (new byte[] { 0xE8 },       "TimeSlice"),

            // Tracks:
            new EbmlSig (new byte[] { 0xAE },       "TrackEntry"),
            new EbmlSig (new byte[] { 0x55, 0xB0 }, "Colour"),
            new EbmlSig (new byte[] { 0x66, 0x24 }, "TrackTranslate"),
            new EbmlSig (new byte[] { 0xE0 },       "Video"),
            new EbmlSig (new byte[] { 0xE1 },       "Audio"),
            new EbmlSig (new byte[] { 0x6D, 0x80 }, "ContentEncodings"),
            new EbmlSig (new byte[] { 0x62, 0x40 }, "ContentEncoding"),
            new EbmlSig (new byte[] { 0x50, 0x34 }, "ContentCompression"),

            // Cues:
            new EbmlSig (new byte[] { 0xBB },       "CuePoint"),
            new EbmlSig (new byte[] { 0xB7 },       "CueTrackPositions"),

            // Chapters:
            new EbmlSig (new byte[] { 0x45, 0xB9 }, "EditionEntry"),
            new EbmlSig (new byte[] { 0x45, 0xDD }, "EditionFlagOrdered"),
            new EbmlSig (new byte[] { 0xB6 },       "ChapterAtom"),

            // Attachments:
            new EbmlSig (new byte[] { 0x46, 0x7E }, "FileDescription")
        };

        private static readonly EbmlSig[] leafSigs =
        {
            // EBML:
            new EbmlSig (new byte[] { 0x42, 0x86 }, EbmlType.Unsigned, "EBMLVersion",        ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x42, 0xF7 }, EbmlType.Unsigned, "EBMLReadVersion",    ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x42, 0xF2 }, EbmlType.Unsigned, "EBMLMaxIDLength",    ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x42, 0xF3 }, EbmlType.Unsigned, "EBMLMaxSizeLength",  ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x42, 0x82 }, EbmlType.Ascii,    "DocType",            ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x42, 0x87 }, EbmlType.Unsigned, "DocTypeVersion",     ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x42, 0x85 }, EbmlType.Unsigned, "DocTypeReadVersion", ParseFlag.Persist),

            voidSig,
            CrcSig,

            // SeekHead:
            new EbmlSig (new byte[] { 0x53, 0xAB }, EbmlType.Binary,   "SeekID"),
            new EbmlSig (new byte[] { 0x53, 0xAC }, EbmlType.Unsigned, "SeekPosition"),
            new EbmlSig (new byte[] { 0x2A, 0xD7, 0xB1 }, EbmlType.Unsigned, "TimecodeScale"),

            // Cluster:
            new EbmlSig (new byte[] { 0xE7 },       EbmlType.Unsigned, "Timecode"),
            new EbmlSig (new byte[] { 0x58, 0xD7 }, EbmlType.Unsigned, "SilentTrackNumber"),
            new EbmlSig (new byte[] { 0x9B },       EbmlType.Unsigned, "BlockDuration"),
            new EbmlSig (new byte[] { 0xA7 },       EbmlType.Unsigned, "Position"),
            new EbmlSig (new byte[] { 0xAB },       EbmlType.Unsigned, "PrevSize"),
            new EbmlSig (new byte[] { 0xA3 },       EbmlType.Binary,   "SimpleBlock"),
            new EbmlSig (new byte[] { 0xA1 },       EbmlType.Binary,   "Block"),
            new EbmlSig (new byte[] { 0xEE },       EbmlType.Unsigned, "BlockAddID"),
            new EbmlSig (new byte[] { 0xA5 },       EbmlType.Binary,   "BlockAdditional"),
            new EbmlSig (new byte[] { 0x9B },       EbmlType.Unsigned, "BlockDuration"),
            new EbmlSig (new byte[] { 0xFA },       EbmlType.Unsigned, "ReferencePriority"),
            new EbmlSig (new byte[] { 0xFB },       EbmlType.Signed,   "ReferenceBlock"),
            new EbmlSig (new byte[] { 0xA4 },       EbmlType.Binary,   "CodecState"),
            new EbmlSig (new byte[] { 0x75, 0xA2 }, EbmlType.Signed,   "DiscardPadding"),
            new EbmlSig (new byte[] { 0xCC },       EbmlType.Unsigned, "LaceNumber"),

            // Segment:
            new EbmlSig (new byte[] { 0x73, 0xA4 }, EbmlType.Binary,   "SegmentUID"),
            new EbmlSig (new byte[] { 0x73, 0x84 }, EbmlType.Utf8,     "SegmentFilename"),
            new EbmlSig (new byte[] { 0x44, 0x44 }, EbmlType.Binary,   "SegmentFamily"),
            new EbmlSig (new byte[] { 0x69, 0xFC }, EbmlType.Unsigned, "ChapterTranslateEditionUID"),
            new EbmlSig (new byte[] { 0x69, 0xBF }, EbmlType.Unsigned, "ChapterTranslateCodec"),
            new EbmlSig (new byte[] { 0x69, 0xA5 }, EbmlType.Binary,   "ChapterTranslateID"),
            new EbmlSig (new byte[] { 0x44, 0x89 }, EbmlType.Float,    "Duration"),
            new EbmlSig (new byte[] { 0x44, 0x61 }, EbmlType.Date,     "DateUTC"),
            new EbmlSig (new byte[] { 0x7B, 0xA9 }, EbmlType.Utf8,     "Title"),
            new EbmlSig (new byte[] { 0x4D, 0x80 }, EbmlType.Utf8,     "MuxingApp"),
            new EbmlSig (new byte[] { 0x57, 0x41 }, EbmlType.Utf8,     "WritingApp"),

            // Tracks:
            new EbmlSig (new byte[] { 0xD7       }, EbmlType.Unsigned, "TrackNumber",        ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x73, 0xC5 }, EbmlType.Unsigned, "TrackUID"),
            new EbmlSig (new byte[] { 0x83       }, EbmlType.Unsigned, "TrackType"),
            new EbmlSig (new byte[] { 0xB9       }, EbmlType.Unsigned, "FlagEnabled"),
            new EbmlSig (new byte[] { 0x88       }, EbmlType.Unsigned, "FlagDefault"),
            new EbmlSig (new byte[] { 0x55, 0xAA }, EbmlType.Unsigned, "FlagForced"),
            new EbmlSig (new byte[] { 0x9C       }, EbmlType.Unsigned, "FlagLacing"),
            new EbmlSig (new byte[] { 0x6D, 0xE7 }, EbmlType.Unsigned, "MinCache"),
            new EbmlSig (new byte[] { 0x6D, 0xF8 }, EbmlType.Unsigned, "MaxCache"),
            new EbmlSig (new byte[] { 0x23, 0xE3, 0x83 }, EbmlType.Unsigned, "DefaultDuration"),
            new EbmlSig (new byte[] { 0x23, 0x31, 0x4F }, EbmlType.Float, "TrackTimecodeScale"),
            new EbmlSig (new byte[] { 0x55, 0xEE }, EbmlType.Unsigned, "MaxBlockAdditionID"),
            new EbmlSig (new byte[] { 0x53, 0x6E }, EbmlType.Utf8,     "Name",              ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x22, 0xB5, 0x9C }, EbmlType.Ascii, "Language",       ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x22, 0xB5, 0x9D }, EbmlType.Ascii, "LanguageBcp47",  ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x86       }, EbmlType.Ascii,    "CodecID",           ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x63, 0xA2 }, EbmlType.Binary,   "CodecPrivate"),
            new EbmlSig (new byte[] { 0x25, 0x86, 0x88 }, EbmlType.Utf8, "CodecName"),
            new EbmlSig (new byte[] { 0x74, 0x46 }, EbmlType.Unsigned, "AttachmentLink"),
            new EbmlSig (new byte[] { 0xAA       }, EbmlType.Unsigned, "CodecDecodeAll"),
            new EbmlSig (new byte[] { 0x6F, 0xAB }, EbmlType.Unsigned, "TrackOverlay"),
            new EbmlSig (new byte[] { 0x56, 0xAA }, EbmlType.Unsigned, "CodecDelay"),
            new EbmlSig (new byte[] { 0x56, 0xBB }, EbmlType.Unsigned, "SeekPreRoll"),

            new EbmlSig (new byte[] { 0x66, 0xFC }, EbmlType.Unsigned, "TrackTranslateEditionUID"),
            new EbmlSig (new byte[] { 0x66, 0xBF }, EbmlType.Unsigned, "TrackTranslateCodec"),
            new EbmlSig (new byte[] { 0x66, 0xA5 }, EbmlType.Binary,   "TrackTranslateTrackID"),
            new EbmlSig (new byte[] { 0x9A       }, EbmlType.Unsigned, "FlagInterlaced"),
            new EbmlSig (new byte[] { 0x9D       }, EbmlType.Unsigned, "FieldOrder"),
            new EbmlSig (new byte[] { 0x53, 0xB8 }, EbmlType.Unsigned, "StereoMode"),
            new EbmlSig (new byte[] { 0x53, 0xC0 }, EbmlType.Unsigned, "AlphaMode"),
            new EbmlSig (new byte[] { 0xB0       }, EbmlType.Unsigned, "PixelWidth",        ParseFlag.Persist),
            new EbmlSig (new byte[] { 0xBA       }, EbmlType.Unsigned, "PixelHeight",       ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x54, 0xAA }, EbmlType.Unsigned, "PixelCropBottom"),
            new EbmlSig (new byte[] { 0x54, 0xBB }, EbmlType.Unsigned, "PixelCropTop"),
            new EbmlSig (new byte[] { 0x54, 0xCC }, EbmlType.Unsigned, "PixelCropLeft"),
            new EbmlSig (new byte[] { 0x54, 0xDD }, EbmlType.Unsigned, "PixelCropRight"),
            new EbmlSig (new byte[] { 0x54, 0xB0 }, EbmlType.Unsigned, "DisplayWidth",      ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x54, 0xBA }, EbmlType.Unsigned, "DisplayHeight",     ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x54, 0xB2 }, EbmlType.Unsigned, "DisplayUnit"),
            new EbmlSig (new byte[] { 0x54, 0xB3 }, EbmlType.Unsigned, "AspectRatioType"),
            new EbmlSig (new byte[] { 0x2E, 0xB5, 0x24 }, EbmlType.Binary, "ColourSpace"),
            new EbmlSig (new byte[] { 0x55, 0xB7 }, EbmlType.Unsigned, "ChromaSitingHorz"),
            new EbmlSig (new byte[] { 0x55, 0xB8 }, EbmlType.Unsigned, "ChromaSitingVert"),
            new EbmlSig (new byte[] { 0x55, 0xB9 }, EbmlType.Unsigned, "Range"),

            new EbmlSig (new byte[] { 0xB5       }, EbmlType.Float,    "SamplingFrequency", ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x78, 0xB5 }, EbmlType.Float,    "OutputSamplingFrequency"),
            new EbmlSig (new byte[] { 0x9F       }, EbmlType.Unsigned, "Channels",          ParseFlag.Persist),
            new EbmlSig (new byte[] { 0x62, 0x64 }, EbmlType.Unsigned, "BitDepth"),

            new EbmlSig (new byte[] { 0x50, 0x31 }, EbmlType.Unsigned, "ContentEncodingOrder"),
            new EbmlSig (new byte[] { 0x50, 0x32 }, EbmlType.Unsigned, "ContentEncodingScope"),
            new EbmlSig (new byte[] { 0x50, 0x33 }, EbmlType.Unsigned, "ContentEncodingType"),

            new EbmlSig (new byte[] { 0x42, 0x54 }, EbmlType.Unsigned, "ContentCompAlgo"),
            new EbmlSig (new byte[] { 0x42, 0x55 }, EbmlType.Binary,   "ContentCompSettings"),

            // Cluster:
            new EbmlSig (new byte[] { 0xE7       }, EbmlType.Unsigned, "Timecode"),
            new EbmlSig (new byte[] { 0xA3       }, EbmlType.Binary,   "SimpleBlock"),
            new EbmlSig (new byte[] { 0xA7       }, EbmlType.Unsigned, "Position"),
            new EbmlSig (new byte[] { 0xAB       }, EbmlType.Unsigned, "PrevSize"),
            new EbmlSig (new byte[] { 0xFB       }, EbmlType.Signed,   "ReferenceBlock"),

            // Cues:
            new EbmlSig (new byte[] { 0xB3       }, EbmlType.Unsigned, "CueTime"),
            new EbmlSig (new byte[] { 0xF7       }, EbmlType.Unsigned, "CueTrack"),
            new EbmlSig (new byte[] { 0xF1       }, EbmlType.Unsigned, "CueClusterPosition"),
            new EbmlSig (new byte[] { 0xF0       }, EbmlType.Unsigned, "CueRelativePosition"),
            new EbmlSig (new byte[] { 0xB2       }, EbmlType.Unsigned, "CueDuration"),

            // Chapters:
            new EbmlSig (new byte[] { 0x45, 0xBC }, EbmlType.Unsigned, "EditionUID"),
            new EbmlSig (new byte[] { 0x45, 0xBD }, EbmlType.Unsigned, "EditionFlagHidden"),
            new EbmlSig (new byte[] { 0x45, 0xDB }, EbmlType.Unsigned, "EditionFlagDefault"),
            new EbmlSig (new byte[] { 0x73, 0xC4 }, EbmlType.Unsigned, "ChapterUID"),
            new EbmlSig (new byte[] { 0x91       }, EbmlType.Unsigned, "ChapterTimeStart"),
            new EbmlSig (new byte[] { 0x92       }, EbmlType.Unsigned, "ChapterTimeEnd"),
            new EbmlSig (new byte[] { 0x98       }, EbmlType.Unsigned, "ChapterFlagHidden"),
            new EbmlSig (new byte[] { 0x45, 0x98 }, EbmlType.Unsigned, "ChapterFlagEnabled"),
            new EbmlSig (new byte[] { 0x80       }, EbmlType.Unsigned, "ChapterDisplay"),

            // Attachments:
            new EbmlSig (new byte[] { 0x46, 0x7E }, EbmlType.Utf8, "FileDescription"),
            new EbmlSig (new byte[] { 0x46, 0x6E }, EbmlType.Utf8, "FileName"),
            new EbmlSig (new byte[] { 0x73, 0x73 }, EbmlType.Utf8, "Tag")
        };

        public long EbmlVersion => root.SeekForUnsigned (0x4286);
        public long EbmlReadVersion => root.SeekForUnsigned (0x42F7);
        public long EbmlMaxIdLength => root.SeekForUnsigned (0x42F2);
        public long EbmlMaxSizeLength => root.SeekForUnsigned (0x42F3);
        public string DocType => root.SeekForASCII (0x4282);
        public long DocTypeVersion => root.SeekForUnsigned (0x4287);
        public long DocTypeReadVersion => root.SeekForUnsigned (0x4285);
        public string Codec => segment.SeekForASCII (0x86);

        private List<EbmlNodeMaster> layout = new List<EbmlNodeMaster>();
        private EbmlNodeMaster root = null, segment = null;
        public int CrcCount { get; private set; }
        private int? badCrcCount = null;
        public bool HasMisplacedAttachment { get; private set; }
        public string EbmlVersionText => EbmlVersion < 0 ? "?" : EbmlVersion.ToString();
        public string Codecs => String.Join (", ", segment.GetAsciis ("CodecID"));
        public int? GoodCrcCount => badCrcCount == null ? (int?) null : CrcCount - badCrcCount.Value;

        public override bool IsBadData
         => badCrcCount != null && badCrcCount.Value != 0;

        public Issue CdIssue { get; private set; }

        private MkvFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }

        private string layoutText = null;
        public String Layout
        {
            get
            {
                var sb = new StringBuilder();
                using (var it = segment.Nodes.GetEnumerator())
                {
                    var prev = String.Empty;
                    if (it.MoveNext())
                    {
                        int mult = 1;
                        var setSize = it.Current.Size;
                        prev = it.Current.Element.Name;

                        sb.Clear();
                        sb.Append ('|');
                        for (;;)
                        {
                            var ok = it.MoveNext();
                            if (ok && it.Current.Element.Name == prev)
                            {
                                ++mult;
                                setSize += it.Current.Size;
                            }
                            else
                            {
                                sb.Append (' ');
                                sb.Append (prev);
                                if (mult != 1)
                                { sb.Append ('*'); sb.Append (mult); }
                                sb.Append (" ("); sb.Append (setSize); sb.Append (") |");
                                if (! ok)
                                    break;
                                mult = 1;
                                setSize = it.Current.Size;
                                prev = it.Current.Element.Name;
                            }
                        }
                    }
                }

                if (HasMisplacedAttachment)
                {
                    sb.Append (" Misattachment (");
                    sb.Append (attachSig.Name);
                    sb.Append (") |");
                }

                layoutText = sb.ToString();
                return layoutText;
            }
        }

        public override void GetReportDetail (IList<string> report)
        {
            var sb = new StringBuilder();

            if (report.Count != 0)
                report.Add (String.Empty);

            var ver = EbmlVersion;
            var readVer = EbmlReadVersion;
            var maxIdLen = EbmlMaxIdLength;
            var maxSizeLen = EbmlMaxSizeLength;

            sb.Append ($"EBML v{EbmlVersionText}");
            sb.AppendLine (", EBML read v" + (readVer < 0? "?" : readVer.ToString()));
            sb.Append ("Max ID len=" + (maxIdLen < 0? "?" : maxIdLen.ToString()));
            sb.AppendLine (", max size len=" + (maxSizeLen < 0? "?" : maxSizeLen.ToString()));
            sb.Append ("Doc type " + DocType);
            sb.AppendLine (", Doc type v" + DocTypeVersion + ", Doc type read v" + DocTypeReadVersion);
            report.Add (sb.ToString());

            report.Add ("Codecs:");
            foreach (var cx in segment.GetAsciis ("CodecID"))
                report.Add ("  " + cx);

            report.Add (String.Empty);
            report.Add ("CRCs:");
            foreach (var master in layout)
                foreach (var trace in master.GetNodeTraces ("CRC-32"))
                {
                    sb.Clear();
                    var top = trace.Peek();
                    var node = top.Node.Nodes[top.Index] as EbmlNodeCRC;
                    sb.AppendFormat ($"  stored={node.StoredCRC32:X8}, actual={node.ActualCRC32:X8}, size={node.Count}: ");
                    sb.AppendEbmlStack (trace);
                    report.Add (sb.ToString());
                }

            report.Add (String.Empty);
            report.Add ($"Layout: {Layout}");
        }
    }
}
