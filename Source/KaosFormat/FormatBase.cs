using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using KaosIssue;
using KaosCrypto;

namespace KaosFormat
{
    public enum Likeliness
    { None, Possible, Probable }

    [Flags]
    public enum Hashes
    {
        None=0, Intrinsic=1,
        FileMD5=2, FileSHA1=4, FileSHA256=8,
        MetaSHA1=0x10, MediaSHA1=0x20,
        PcmMD5=0x100, PcmCRC32=0x200,
        _FlacMatch=0x10000, _FlacTags = 0x20000, _WebCheck=0x40000
    }

    [Flags]
    public enum Validations
    { None=0, Exists=1, MD5=2, SHA1=4, SHA256=8 };

    [DebuggerDisplay (@"\{{Name}}")]
    public abstract class FormatBase : INotifyPropertyChanged
    {
        public class Model
        {
            protected FormatBase _data;
            public FormatBase Data => _data;
            public Issue.Vector.Model IssueModel { get; private set; }

            public Model()
             => IssueModel = new Issue.Vector.Model();

            public static Model Create (Stream fs, string path, Hashes hashFlags)
            {
                var model = Create (fs, path, hashFlags, 0, null, out FileFormat actual);
                if (model != null)
                    model.CloseFile();
                return model;
            }

            /// <summary>Factory method for various file formats.</summary>
            public static Model Create (Stream fs0, string path,
                                        Hashes hashFlags, Validations validationFlags,
                                        string filter, out FileFormat actual)
            {
                bool isKnown = false;
                actual = null;

                FormatBase.Model model = null;
                var isMisname = false;
                var ext = System.IO.Path.GetExtension (path);
                if (ext.Length < 2)
                    return null;
                ext = ext.Substring(1).ToLower();

                var hdr = new byte[0x2C];
                fs0.Read (hdr, 0, hdr.Length);

                using (var scan = KaosDiags.Diags.FileFormats.Items.GetEnumerator())
                {
                    for (FileFormat other = null;;)
                    {
                        if (scan.MoveNext())
                        {
                            if (scan.Current.Names.Contains (ext))
                            {
                                isKnown = true;
                                if (scan.Current.Subname != null && scan.Current.Subname[0] == '*')
                                    other = scan.Current;
                                else
                                {
                                    model = scan.Current.ModelFactory (fs0, hdr, path);
                                    if (model != null)
                                    {
                                        actual = scan.Current;
                                        break;
                                    }
                                }
                            }
                            continue;
                        }

                        if (! isKnown && filter == null)
                            return null;

                        if (other != null)
                        {
                            actual = other;
                            if (other.Subname[0] == '*')
                                return null;
                            model = other.ModelFactory (fs0, hdr, path);
                            break;
                        }

                        scan.Reset();
                        do
                        {
                            if (! scan.MoveNext())
                            {
                                var result = new UnknownFormat.Model (fs0, path);
                                result.CalcHashes (hashFlags, validationFlags);
                                return result;
                            }
                            if (scan.Current.Names.Contains (ext))
                                continue;
                            model = scan.Current.ModelFactory (fs0, hdr, path);
                        }
                        while (model == null);

                        actual = scan.Current;
                        isKnown = true;
                        isMisname = true;
                        break;
                    }
                }

                FormatBase fmt = model.Data;
                if (! fmt.Issues.HasFatal)
                {
                    if (fmt.mediaPosition < 0)
                    {
                        fmt.mediaPosition = 0;
                        fmt.MediaCount = fmt.FileSize;
                    }

                    model.CalcHashes (hashFlags, validationFlags);

                    if (isMisname)
                    {
                        // This repair goes last because it must close the file.
                        ++actual.TotalMisnamed;
                        fmt.FfIssue = model.IssueModel.Add
                            ($"True file format is .{actual.PrimaryName}.", Severity.Warning, 0,
                              "Rename to extension of ." + actual.PrimaryName, model.RepairWrongExtension, isFinalRepairer:true);
                    }
                }

                return model;
            }

            public virtual void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                if (IssueModel.Data.HasFatal)
                    return;

                bool hitCache = Data.fBuf != null && Data.FileSize < Int32.MaxValue;

                if ((hashFlags & Hashes.FileMD5) != 0 && Data.fileMD5 == null)
                {
                    var hasher = new Md5Hasher();
                    if (hitCache)
                        hasher.Append (Data.fBuf, 0, Data.fBuf.Length);
                    else
                        hasher.Append (Data.fbs);
                    Data.fileMD5 = hasher.GetHashAndReset();
                }

                if ((hashFlags & Hashes.FileSHA1) != 0 && Data.fileSHA1 == null)
                {
                    var hasher = new Sha1Hasher();
                    if (hitCache)
                        hasher.Append (Data.fBuf, 0, Data.fBuf.Length);
                    else
                        hasher.Append (Data.fbs);
                    Data.fileSHA1 = hasher.GetHashAndReset();
                }

                if ((hashFlags & Hashes.FileSHA256) != 0 && Data.fileSHA256 == null)
                {
                    var hasher = new Sha256Hasher();
                    if (hitCache)
                        hasher.Append (Data.fBuf, 0, Data.fBuf.Length);
                    else
                        hasher.Append (Data.fbs);
                    Data.fileSHA256 = hasher.GetHashAndReset();
                }

                if ((hashFlags & Hashes.MediaSHA1) != 0 && Data.mediaSHA1 == null)
                    if (Data.MediaCount == Data.FileSize && Data.fileSHA1 != null)
                    {
                        System.Diagnostics.Debug.Assert (Data.mediaPosition == 0);
                        Data.mediaSHA1 = Data.fileSHA1;
                    }
                    else
                    {
                        var hasher = new Sha1Hasher();
                        if (hitCache)
                            hasher.Append (Data.fBuf, (int) Data.mediaPosition, (int) Data.MediaCount);
                        else
                            hasher.Append (Data.fbs, Data.mediaPosition, Data.MediaCount);
                        Data.mediaSHA1 = hasher.GetHashAndReset();
                    }

                if ((hashFlags & Hashes.MetaSHA1) != 0 && Data.metaSHA1 == null)
                {
                    var hasher = new Sha1Hasher();
                    var suffixPos = Data.mediaPosition + Data.MediaCount;

                    if (hitCache)
                        hasher.Append (Data.fBuf, 0, (int) Data.mediaPosition, (int) suffixPos, (int) (Data.FileSize - suffixPos));
                    else
                        hasher.Append (Data.fbs, 0, Data.mediaPosition, suffixPos, Data.FileSize - suffixPos);
                    Data.metaSHA1 = hasher.GetHashAndReset();
                }
            }

            protected void CalcMark (bool assumeProbable=false)
            {
                byte[] buf = null;

                long markSize = Data.FileSize - Data.ValidSize;
                if (markSize <= 0)
                    return;

                // 1000 is somewhat arbitrary here.
                if (markSize > 1000)
                    Data.Watermark = Likeliness.Possible;
                else
                {
                    Data.fbs.Position = Data.ValidSize;
                    buf = new byte[(int) markSize];
                    int got = Data.fbs.Read (buf, 0, (int) markSize);
                    if (got != markSize)
                    {
                        IssueModel.Add ("Read failure", Severity.Fatal);
                        return;
                    }

                    Data.excess = null;
                    Data.Watermark = Likeliness.Probable;
                    if (! assumeProbable)
                        for (int ix = 0; ix < buf.Length; ++ix)
                        {
                            // Fuzzy ID of watermark bytes by checking if ASCII text. Purpose is to
                            // exclude .AVI files that do not have correct encoded sizes - a common issue.
                            var bb = buf[ix];
                            if (bb > 127 || (bb < 32 && bb != 0 && bb != 9 && bb != 0x0A && bb != 0x0D))
                            {
                                Data.Watermark = Likeliness.Possible;
                                break;
                            }
                        }
                }

                if (Data.Watermark == Likeliness.Probable)
                {
                    Data.excess = buf;
                    var caption = Data.Watermark.ToString() + " watermark, size=" + Data.ExcessSize + ".";
                    var prompt = "Trim probable watermark of " + Data.ExcessSize + " byte" + (Data.ExcessSize!=1? "s" : "");
                    IssueModel.Add (caption, Severity.Warning, IssueTags.None, prompt, TrimWatermark);
                }
            }

            public string Rename (string newName)
            {
                string p1 = System.IO.Path.GetDirectoryName (Data.Path);
                string newPath = p1 + System.IO.Path.DirectorySeparatorChar + newName;

                try
                {
                    File.Move (Data.Path, newPath);
                    Data.Path = newPath;
                    Data.Name = newName;
                    Data.RaisePropertyChanged (nameof (Data.Path));
                    Data.RaisePropertyChanged (nameof (Data.Name));
                }
                catch (Exception ex)
                { return ex.Message.Trim (null); }

                return null;
            }

            /// <summary>Attempt to change this file's extension to Names[0].</summary>
            /// <returns>Error text if failure; null if success.</returns>
            public string RepairWrongExtension (bool isFinalRepair)
            {
                if (Data.Issues.HasFatal || Data.Names.Length == 0)
                    return "Invalid attempt";

                foreach (var vfn in Data.Names)
                    if (Data.NamedFormat == vfn)
                        return "Invalid attempt";

                CloseFile();
                string newPath = System.IO.Path.ChangeExtension (Data.Path, Data.Names[0]);
                try
                {
                    File.Move (Data.Path, newPath);
                    if (isFinalRepair)
                        CloseFile();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                { return ex.Message.TrimEnd (null); }

                Data.Path = newPath;
                Data.Name = System.IO.Path.GetFileName (newPath);
                Data.RaisePropertyChanged (null);
                return null;
            }

            protected void ResetFile()
            {
                Data.fileMD5 = null;
                Data.fileSHA1 = null;
                Data.mediaSHA1 = null;
                Data.metaSHA1 = null;
                Data.FileSize = Data.fbs==null ? 0 : Data.fbs.Length;
            }

            public string TrimWatermark (bool isFinalRepair)
            {
                if (Data.fbs == null || Data.Issues.MaxSeverity >= Severity.Error || Data.Watermark != Likeliness.Probable)
                    return "Invalid attempt";

                string err = TrimWatermarkUpdate();
                if (isFinalRepair)
                    CloseFile();

                return err;
            }

            private string TrimWatermarkUpdate()
            {
                string err = null;
                try
                {
                    TruncateExcess();
                    Data.Watermark = Likeliness.None;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                { err = ex.Message.TrimEnd (null); }
                return err;
            }

            protected void TruncateExcess()
            {
                Data.fbs.SetLength (Data.FileSize - Data.ExcessSize);
                Data.FileSize -= Data.ExcessSize;
                Data.excised = Data.excess;
                Data.excess = null;
            }

            public void CloseFile()
            {
                if (Data.fbs != null)
                {
                    Data.fbs.Dispose();
                    Data.fbs = null;
                }
            }
        }


        public static readonly Encoding Cp1252 = Encoding.GetEncoding (1252);

        protected Stream fbs;
        protected long mediaPosition = -1;
        protected byte[] fBuf;     // May cache entire file.
        protected byte[] excess;   // Contents of watermark or phantom tag.
        protected byte[] excised;  // Post-repair excess.

        public string Path { get; private set; }
        public string Name { get; private set; }
        public long FileSize { get; private set; }
        public long ValidSize { get; protected set; }
        public long MediaCount { get; protected set; }
        public Likeliness Watermark { get; protected set; }
        public Issue.Vector Issues { get; protected set; }
        public bool IsRepairable => fbs != null;
        public Issue FfIssue { get; private set; }  // Wrong file format.

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged (string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged (this, new PropertyChangedEventArgs (propName));
        }

        public long ExcessSize => excess == null ? 0 : excess.Length;

        public string NamedFormat => System.IO.Path.GetExtension (Path).Substring (1);

        protected FormatBase (Stream stream, string path)
        {
            this.fbs = stream;
            this.Name = System.IO.Path.GetFileName (path);
            this.Path = path;
            this.FileSize = stream.Length;
        }

        protected FormatBase (Model model, Stream stream, string path) : this (stream, path)
         => this.Issues = model.IssueModel.Data;

        public abstract string[] Names
        { get; }

        public virtual string Subname => null;
        public string FullName => Subname == null ? Names[0] : Names[0] + " (" + Subname + ')';

        public virtual bool IsBadHeader => false;
        public virtual bool IsBadData => false;

        protected byte[] metaSHA1 = null;
        public byte[] MetaSHA1 { get { var cp = new byte[metaSHA1.Length]; metaSHA1.CopyTo (cp, 0); return cp; } }
        public string MetaSHA1ToHex => metaSHA1==null ? null : ConvertTo.ToHexString (metaSHA1);
        public bool HasMetaSHA1 => metaSHA1 != null;

        protected byte[] mediaSHA1 = null;
        public byte[] MediaSHA1 { get { var cp = new byte[mediaSHA1.Length]; mediaSHA1.CopyTo (cp, 0); return cp; } }
        public string MediaSHA1ToHex => mediaSHA1==null ? null : ConvertTo.ToHexString (mediaSHA1);
        public bool HasMediaSHA1 => mediaSHA1 != null;

        private byte[] fileMD5 = null;
        public byte[] FileMD5 { get { var cp = new byte[fileMD5.Length]; fileMD5.CopyTo (cp, 0); return cp; } }
        public string FileMD5ToHex => fileMD5==null ? null : ConvertTo.ToHexString (fileMD5);
        public bool FileMD5Equals (byte[] a2) => fileMD5.SequenceEqual (a2);
        public bool HasFileMD5 => fileMD5 != null;

        protected byte[] fileSHA1 = null;
        public byte[] FileSHA1 { get { var cp = new byte[fileSHA1.Length]; fileSHA1.CopyTo (cp, 0); return cp; } }
        public string FileSHA1ToHex => fileSHA1==null ? null : ConvertTo.ToHexString (fileSHA1);
        public bool HasFileSHA1 => fileSHA1 != null;

        protected byte[] fileSHA256 = null;
        public string FileSHA256ToHex => fileSHA256==null ? null : ConvertTo.ToHexString (fileSHA256);
        public bool HasFileSHA256 => fileSHA256 != null;

        protected static bool StartsWith (byte[] target, byte[] other)
        {
            if (target.Length < other.Length)
                return false;

            for (int ix = 0; ix < other.Length; ++ix)
                if (target[ix] != other[ix])
                    return false;

            return true;
        }

        public IList<string> GetReportHeader (Granularity scope)
        {
            var report = new List<string>();
            if (scope > Granularity.Detail)
            {
                if (mediaSHA1 != null)
                    report.Add ($"{MediaSHA1ToHex} :{Name}");
                if (metaSHA1 != null)
                    report.Add ($"{MetaSHA1ToHex} ?{Name}");
                if (fileMD5 != null)
                    report.Add ($"{FileMD5ToHex} *{Name}");
                if (fileSHA1 != null)
                    report.Add ($"{FileSHA1ToHex} *{Name}");
                if (fileSHA256 != null)
                    report.Add ($"{FileSHA256ToHex} *{Name}");
                return report;
            }

            if (mediaSHA1 != null)
            {
                var ms1 = $"Media SHA1= {MediaSHA1ToHex}";
                if (scope <= Granularity.Detail && mediaPosition >= 0)
                    ms1 += $" ({mediaPosition:X4}-{mediaPosition+MediaCount-1:X4})";
                report.Add (ms1);
            }

            if (metaSHA1 != null)
                report.Add ($"Meta SHA1 = {MetaSHA1ToHex}");

            if (fileMD5 != null)
                report.Add ($"File MD5  = {FileMD5ToHex}");

            if (fileSHA1 != null)
                report.Add ($"File SHA1 = {FileSHA1ToHex}");

            if (fileSHA256 != null)
                report.Add ($"File SHA256={FileSHA256ToHex}");

            report.Add ($"File size = {FileSize}");

            return report;
        }

        public virtual void GetReportDetail (IList<string> report)
        {
            var sb = new StringBuilder ("(");
            using (var it = ((IEnumerable<string>) Names).GetEnumerator())
            {
                for (it.MoveNext();;)
                {
                    sb.Append ((string) it.Current);
                    if (! it.MoveNext())
                        break;
                    sb.Append ('/');
                }
            }
            sb.Append (')');
            report.Add (sb.ToString());
        }
    }
}
