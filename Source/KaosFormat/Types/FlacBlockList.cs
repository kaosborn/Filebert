﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace KaosFormat
{
    public enum FlacBlockType
    { StreamInfo, Padding, Application, SeekTable, Tags, CueSheet, Picture, Invalid=127 }

    // FLAC lifted this list from the ID3v2 spec.
    public enum PicType
    {
        Other,
        Icon32, Icon, Front, Back, Leaflet, Disc, Lead, Artist, Conductor, Band,
        Composer, Lyricist, Location, Recording, Performance, Capture, Fish, Illustration, Logo, PublisherLogo
    };


    public abstract class FlacBlockItem
    {
        public int Position { get; private set; }
        public int Size { get; private set; }
        public int NextPosition => Position + Size;
        public string Name => BlockType.ToString();

        public FlacBlockItem (int position, int size)
        { this.Position = position; this.Size = size; }

        public abstract FlacBlockType BlockType
        { get; }

        public override string ToString()
         => BlockType.ToString();
    }


    public class FlacPadBlock : FlacBlockItem
    {
        public FlacPadBlock (int position, int size) : base (position, size)
        { }

        public override FlacBlockType BlockType => FlacBlockType.Padding;
    }


    public class FlacAppBlock : FlacBlockItem
    {
        public int ApplicationId { get; private set; }

        public FlacAppBlock (int position, int size, int appId) : base (position, size)
         => this.ApplicationId = appId;

        public override FlacBlockType BlockType => FlacBlockType.Application;
    }


    public class FlacSeekTableBlock : FlacBlockItem
    {
        private byte[] table;

        public FlacSeekTableBlock (int position, int size, byte[] table) : base (position, size)
         => this.table = table;

        public override FlacBlockType BlockType => FlacBlockType.SeekTable;

        public override string ToString()
        {
            string result = base.ToString();
            int kk = table.Length / 18;
            result += $" ({kk})";
            return result;
        }
    }


    public class FlacTagsBlock : FlacBlockItem
    {
        public int StoredTagCount { get; private set; }
        private byte[] tagData;
        public string Vendor { get; private set; }
        private readonly List<string> lines;
        public ReadOnlyCollection<string> Lines { get; private set; }

        public string TagName (int index)
        {
            string lx = lines[index];
            int eqPos = lx.IndexOf ('=');
            return lines[index].Substring (0, eqPos);
        }

        public string TagValue (int index)
        {
            string lx = lines[index];
            int eqPos = lx.IndexOf ('=');
            return lines[index].Substring (eqPos+1);
        }

        public string[] GetTagValues (string tagName)
        {
            int tt = 0, tn = 0;
            for (int ii = 0; ii < lines.Count; ++ii)
                if (TagName (ii).ToLower() == tagName.ToLower())
                    ++tt;

            var result = new string[tt];
            for (int ii = 0; ii < lines.Count; ++ii)
                if (TagName (ii).ToLower() == tagName.ToLower())
                {
                    result[tn] = TagValue (ii);
                    ++tn;
                }
            return result;
        }

        public string GetTagValuesAppended (string tagName)
        {
            string result = null;
            for (int ii = 0; ii < lines.Count; ++ii)
                if (TagName (ii).ToLower() == tagName.ToLower())
                    if (result == null)
                        result = TagValue (ii);
                    else
                        result += @"\\" + TagValue (ii);
            return result;
        }

        public FlacTagsBlock (int position, int size, byte[] rawTagData) : base (position, size)
        {
            this.lines = new List<string>();
            this.Lines = new ReadOnlyCollection<string> (this.lines);

            this.tagData = rawTagData;

            int len = ConvertTo.FromLit32ToInt32 (tagData, 0);
            Vendor = Encoding.UTF8.GetString (tagData, 4, len);
            int pos = len + 8;
            StoredTagCount = ConvertTo.FromLit32ToInt32 (tagData, pos - 4);

            for (var tn = 1; tn <= StoredTagCount; ++tn)
            {
                if (pos > tagData.Length)
                    break;

                len = ConvertTo.FromLit32ToInt32 (tagData, pos);
                pos += 4;
                lines.Add (Encoding.UTF8.GetString (tagData, pos, len));
                pos += len;
            }
            System.Diagnostics.Debug.Assert (pos == tagData.Length);
        }

        public override FlacBlockType BlockType => FlacBlockType.Tags;
    }


    public class FlacCuesheetBlock : FlacBlockItem
    {
        public bool IsCD { get; private set; }
        public int TrackCount { get; private set; }

        public FlacCuesheetBlock (int position, int size, bool isCD, int trackCount) : base (position, size)
        {
            this.IsCD = isCD;
            this.TrackCount = trackCount;
        }

        public override FlacBlockType BlockType => FlacBlockType.CueSheet;

        public override string ToString()
        {
            string result = base.ToString();
            if (IsCD)
                result += "-CD";
            return result;
        }
    }


    public class FlacPicBlock : FlacBlockItem
    {
        public PicType PicType { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public FlacPicBlock (int position, int size, PicType picType, int width, int height) : base (position, size)
        {
            this.PicType = picType;
            this.Width = width;
            this.Height = height;
        }

        public override FlacBlockType BlockType => FlacBlockType.Picture;

        public override string ToString()
         => base.ToString() + " (" + PicType + "-" + Width + "x" + Height + ")";
    }


    public class FlacBlockList
    {
        private readonly List<FlacBlockItem> items;
        public ReadOnlyCollection<FlacBlockItem> Items { get; private set; }

        public FlacPadBlock PadBlock { get; private set; } = null;
        public FlacPicBlock PicBlock { get; private set; } = null;
        public FlacTagsBlock Tags { get; private set; }

        public FlacBlockList()
        {
            this.items = new List<FlacBlockItem>();
            this.Items = new ReadOnlyCollection<FlacBlockItem> (this.items);
        }

        public void AddPad (int position, int size)
        {
            var block = new FlacPadBlock (position, size);
            items.Add (block);
            if (PadBlock == null)
                PadBlock = block;
        }

        public void AddApp (int position, int size, int appId)
        {
            var block = new FlacAppBlock (position, size, appId);
            items.Add (block);
        }

        public void AddSeekTable (int position, int size, byte[] table)
        {
            var block = new FlacSeekTableBlock (position, size, table);
            items.Add (block);
        }

        public void AddTags (int position, int size, byte[] rawTags)
        {
            var block = new FlacTagsBlock (position, size, rawTags);
            items.Add (block);
            if (Tags == null)
                Tags = block;
        }

        public void AddCuesheet (int position, int size, bool isCD, int trackCount)
        {
            var block = new FlacCuesheetBlock (position, size, isCD, trackCount);
            items.Add (block);
        }

        public void AddPic (int position, int size, PicType picType, int width, int height)
        {
            var block = new FlacPicBlock (position, size, picType, width, height);
            items.Add (block);
            if (PicBlock == null)
                PicBlock = block;
        }
    }
}
