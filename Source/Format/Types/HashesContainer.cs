using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KaosIssue;
using KaosCrypto;

namespace KaosFormat
{
    public enum HashMode { Undefined, Text, Binary, Media, Meta };

    public abstract class HashesContainer : FormatBase
    {
        public new class Model : FormatBase.Model
        {
            public readonly HashedFile.Vector.Model HashedModel;
            public new HashesContainer Data => (HashesContainer) _data;

            protected Model (string rootPath, int hashLength)
             => HashedModel = new HashedFile.Vector.Model (rootPath, hashLength);

            protected void ParseHashes()
            {
                Data.fbs.Position = 0;
                TextReader tr = new StreamReader (Data.fbs, Data.encoding);

                for (int line = 1;; ++line)
                {
                    var lx = tr.ReadLine();
                    if (lx == null)
                        return;

                    lx = lx.TrimStart();
                    if (lx.Length == 0 || lx[0] == ';')
                        continue;

                    if (lx.Length < Data.HashedFiles.HashLength*2+3)
                    {
                        IssueModel.Add ($"Too short, line {line}.", Severity.Fatal);
                        return;
                    }

                    // Try typical format with hash first.
                    byte[] hash = null;
                    if (lx[Data.HashedFiles.HashLength*2]==' ')
                    {
                        var modeChar = lx[Data.HashedFiles.HashLength*2+1];

                        for (int modeIx = 1; modeIx < modeChars.Length; ++modeIx)
                            if (modeChar == modeChars[modeIx])
                            {
                                hash = ConvertTo.FromHexStringToBytes (lx, 0, Data.HashedFiles.HashLength);
                                if (hash != null)
                                {
                                    var targetName = lx.Substring (Data.HashedFiles.HashLength*2 + 2);
                                    HashedModel.Add (targetName, hash, (HashMode) modeIx);
                                    break;
                                }
                            }

                        if (hash != null)
                            continue;
                    }

                    // Fall back to layout with name followed by hash.
                    if (lx[lx.Length-Data.HashedFiles.HashLength*2-1]==' ')
                    {
                        hash = ConvertTo.FromHexStringToBytes (lx, lx.Length-Data.HashedFiles.HashLength*2, Data.HashedFiles.HashLength);
                        if (hash != null)
                        {
                            var targetName = lx.Substring (0, lx.Length-Data.HashedFiles.HashLength*2-1);
                            HashedModel.Add (targetName, hash, HashMode.Binary);
                            continue;
                        }
                    }

                    IssueModel.Add ($"Badly formed, line {line}.", Severity.Fatal);
                }
            }

            protected void ComputeContentHashes (CryptoHasher hasher, Hashes mediaHash=Hashes.None)
            {
                System.Diagnostics.Debug.Assert (Data.HashedFiles.HashLength == hasher.HashLength);

                for (int index = 0; index < Data.HashedFiles.Items.Count; ++index)
                {
                    HashedFile item = Data.HashedFiles.Items[index];
                    string err = null;
                    var targetName = Data.HashedFiles.GetPath (index);

                    try
                    {
                        using (var tfs = new FileStream (targetName, FileMode.Open, FileAccess.Read))
                        {
                            HashedModel.SetIsFound (index, true);
                            byte[] hash = null;

                            if (item.Mode == HashMode.Text || item.Mode == HashMode.Binary)
                            {
                                hasher.Append (tfs);
                                hash = hasher.GetHashAndReset();
                            }
                            else if (! (hasher is Sha1Hasher))
                                IssueModel.Add ("Nonfile hashes not supported for this type.");
                            else
                            {
                                var h0 = item.Mode == HashMode.Meta ? Hashes.MetaSHA1 : Hashes.MediaSHA1;
                                var fmtModel = FormatBase.Model.Create (tfs, targetName, h0);
                                if (fmtModel == null)
                                    IssueModel.Add ($"Unknown file format item #{index+1}.");
                                else
                                    hash = item.Mode == HashMode.Meta ? fmtModel.Data.MetaSHA1 : fmtModel.Data.MediaSHA1;
                            }

                            HashedModel.SetActualHash (index, hash);
                            if (item.IsMatch == false)
                                IssueModel.Add ($"{Data.HasherName} mismatch on '{item.FileName}'.");
                        }
                    }
                    catch (FileNotFoundException ex)
                    { err = ex.Message.TrimEnd (null); }
                    catch (IOException ex)
                    { err = ex.Message.TrimEnd (null); }
                    catch (UnauthorizedAccessException ex)
                    { err = ex.Message.TrimEnd (null); }

                    if (err != null)
                    {
                        HashedModel.SetIsFound (index, false);
                        IssueModel.Add (err);
                    }
                }

                string tx = $"{Data.HasherName} validation of {Data.HashedFiles.Items.Count} file";
                if (Data.HashedFiles.Items.Count != 1)
                    tx += "s";
                if (base.Data.Issues.MaxSeverity < Severity.Error)
                    tx += " successful.";
                else
                    tx += $" failed with {Data.HashedFiles.FoundCount} found and {Data.HashedFiles.MatchCount} matched.";

                IssueModel.Add (tx, Severity.Advisory);
            }
        }


        private static readonly string modeChars = "! *:?";
        private Encoding encoding;

        public HashedFile.Vector HashedFiles { get; private set; }
        public Validations Validation { get; protected set; }
        public string HasherName => Validation.ToString();

        protected HashesContainer (Model model, Stream stream, string path, Encoding encoding = null) : base (model, stream, path)
        {
            this.encoding = encoding ?? FormatBase.Cp1252;
            this.HashedFiles = model.HashedModel.Data;
        }

        public override void GetDetailsBody (IList<string> report, Granularity scope)
        {
            if (scope > Granularity.Detail)
                return;

            if (report.Count != 0)
                report.Add (String.Empty);

            report.Add ($"{HasherName} count = {HashedFiles.Items.Count}");

            foreach (HashedFile item in HashedFiles.Items)
                report.Add (item.StoredHashToHex + ' ' + modeChars[(int) item.Mode] + item.FileName);
        }
    }
}
