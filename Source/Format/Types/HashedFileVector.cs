﻿using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace KaosFormat
{
    public class HashedFile: INotifyPropertyChanged
    {
        public class Vector
        {
            public class Model
            {
                public readonly Vector Data;

                public Model (string filePath, int hashLength)
                 => Data = new Vector (filePath, hashLength);

                public void Add (string name, byte[] storedHash, HashStyle hashStyle)
                 => Data.items.Add (new HashedFile (name, storedHash, hashStyle));

                public void AddActual (string name, byte[] storedActualHash, HashStyle hashStyle=HashStyle.Binary)
                {
                    var item = new HashedFile (name, storedActualHash, hashStyle, storedActualHash);
                    Data.items.Add (item);
                    item.IsMatch = storedActualHash==null? (bool?) null : true;
                }

                public void SetFileName (int index, string newName)
                {
                    HashedFile item = Data.items[index];
                    item.oldFileName = item.FileName;
                    item.FileName = newName;
                }

                public void SetIsFound (int index, bool newValue)
                {
                    HashedFile item = Data.items[index];
                    if (newValue != item.IsFound)
                    {
                        if (item.IsFound == true) --Data.FoundCount;
                        if (newValue == true) ++Data.FoundCount;
                        else if (newValue == false) item.actualHash = null;
                        item.IsFound = newValue;
                    }
                }

                public void SetIsMatch (int index, bool newValue)
                {
                    HashedFile item = Data.items[index];
                    if (newValue != item.IsMatch)
                    {
                        if (item.IsMatch == true) --Data.MatchCount;
                        if (newValue == true) ++Data.MatchCount;
                        item.IsMatch = newValue;
                    }
                }

                public void SetOldFileName (int index, string newOldName)
                {
                    HashedFile item = Data.items[index];
                    if (item.oldFileName != newOldName)
                    {
                        item.oldFileName = newOldName;
                        item.NotifyPropertyChanged (null);
                    }
                }

                public void SetActualHash (int index, byte[] newHash)
                {
                    HashedFile item = Data.items[index];
                    item.actualHash = newHash;
                    if (newHash == null)
                        SetIsMatch (index, false);
                    else
                        SetIsMatch (index, item.StoredHash != null && item.actualHash.SequenceEqual (item.StoredHash));
                }

                public void SetStoredHashToActual (int index)
                {
                    HashedFile item = Data.items[index];
                    if (item.actualHash == null)
                    {
                        if (item.storedHash != null)
                        {
                            item.IsMatch = item.actualHash != null? false : (bool?) null;
                            item.storedHash = null;
                            item.NotifyPropertyChanged (null);
                        }
                    }
                    else if (! item.actualHash.SequenceEqual (item.storedHash))
                    {
                        item.storedHash = new byte[item.actualHash.Length];
                        Array.Copy (item.actualHash, item.storedHash, item.storedHash.Length);
                        SetIsMatch (index, true);
                        item.NotifyPropertyChanged (null);
                    }
                }

                public void SetStoredHash (int index, byte[] newHash)
                {
                    HashedFile item = Data.items[index];
                    if (! newHash.SequenceEqual (item.storedHash))
                    {
                        item.storedHash = new byte[newHash.Length];
                        Array.Copy (newHash, item.storedHash, newHash.Length);
                        SetIsMatch (index, newHash.SequenceEqual (item.actualHash));
                        item.NotifyPropertyChanged (null);
                    }
                }
            }


            public int HashLength { get; private set; }
            public string BaseDir { get; private set; }
            public int FoundCount { get; private set; }
            public int MatchCount { get; private set; }

            public string GetPath (int index)
            {
                HashedFile item = items[index];
                if (item.IsRelative == true)
                    return BaseDir + item.FileName;
                else
                    return item.FileName;
            }

            private readonly ObservableCollection<HashedFile> items;
            public ReadOnlyObservableCollection<HashedFile> Items { get; private set; }

            public Vector (string baseDir, int hashLength)
            {
                this.items = new ObservableCollection<HashedFile>();
                this.Items = new ReadOnlyObservableCollection<HashedFile> (this.items);

                this.HashLength = hashLength;

                this.BaseDir = Path.GetDirectoryName (baseDir);
                if (! this.BaseDir.EndsWith (Path.DirectorySeparatorChar.ToString()))
                    this.BaseDir += Path.DirectorySeparatorChar;
            }

            public HashedFile LookupByExtension (string ext)
            {
                foreach (var item in items)
                    if (item.FileName.ToLower().EndsWith (ext))
                        return item;
                return null;
            }

            public int LookupIndexByExtension (string ext)
            {
                for (var ix = 0; ix < items.Count; ++ix)
                    if (items[ix].FileName.ToLower().EndsWith (ext))
                        return ix;
                return -1;
            }
        }


        private HashedFile (string name, byte[] storedHash, HashStyle style, byte[] actualHash = null)
        {
            this.storedHash = storedHash;
            this.actualHash = actualHash;
            this.FileName = name;
            this.IsMatch = null;
            this.IsFound = null;
            this.IsRelative = ! Path.IsPathRooted (name);
            this.Style = style;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged (string propName)
        { if (PropertyChanged != null) PropertyChanged (this, new PropertyChangedEventArgs (propName)); }

        private static readonly string[] ModeTexts = new string[] { "?", "Text", "Binary", "Media", "Meta" };
        private string oldFileName;
        private byte[] storedHash, actualHash;

        public bool IsRelative { get; private set; }
        public bool? IsFound { get; private set; }
        public bool? IsMatch { get; private set; }
        public HashStyle Style { get; private set; }
        public string ModeText => ModeTexts[(int) Style];

        public string FileName { get; private set; }
        public string OldFileName => oldFileName;

        public byte[] StoredHash
        {
            get
            {
                if (storedHash == null)
                    return null;
                var result = new byte[storedHash.Length];
                Array.Copy (storedHash, result, result.Length);
                return result;
            }
        }

        public byte[] ActualHash
        {
            get
            {
                if (actualHash == null)
                    return null;
                var result = new byte[actualHash.Length];
                Array.Copy (actualHash, result, result.Length);
                return result;
            }
        }

        public bool IsOriginalMatch => IsMatch == true && IsRelative && oldFileName == null;
        public bool IsRenamedMatch => IsMatch == true && IsRelative && oldFileName != null;
        public bool NotFoundOrNotMatch => IsFound == false || IsMatch == false;

        public string StoredHashToHex => StoredHash == null ? null : ConvertTo.ToHexString (StoredHash);
        public string ActualHashToHex => actualHash == null ? null : ConvertTo.ToHexString (actualHash);

        public override string ToString() => FileName;
    }
}
