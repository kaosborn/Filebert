using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using KaosIssue;
using KaosFormat;

namespace KaosDiags
{
    public enum Interaction { None, PromptToRepair, RepairLater };

    public delegate void MessageSendHandler (string message, Severity severity);
    public delegate void ReportCloseHandler ();
    public delegate void FileVisitEventHandler (string dirName, string fileName);


    public partial class Diags : INotifyPropertyChanged
    {
        public static FileFormat.Vector FileFormats { get; private set; }

        public Func<string,bool?> QuestionAsk;
        public Func<string,string,char> InputChar;
        public Func<string,string,string,string> InputLine;
        public event MessageSendHandler MessageSend;
        public event ReportCloseHandler ReportClose;
        public event FileVisitEventHandler FileVisit;

        public string Product { get; set; }
        public string ProductVersion { get; set; }

        private string root;
        public string Root
        {
            get => root;
            set { root = value; RaisePropertyChanged (nameof (Root)); }
        }

        public string Filter { get; set; }
        public string Exclusion { get; set; }
        public Interaction Response { get; protected set; }
        public Granularity Scope { get; set; }
        public Validations ValidationFlags { get; set; }
        public IssueTags WarnEscalator { get; set; }
        public IssueTags ErrEscalator { get; set; }
        public Severity Result { get; private set; } = Severity.NoIssue;

        public string CurrentFile { get; private set; }
        public string CurrentDirectory { get; private set; }

        public int TotalFiles { get; set; }
        public int TotalRepairable { get; set; }
        public int TotalWarnings { get; set; }
        public int TotalErrors { get; set; }
        public int TotalRepairs { get; set; }

        public static string MinorSeparator => "---- ---- ---- ---- ---- ----";
        public static string MajorSeparator => "==== ==== ==== ==== ==== ====";

        protected Diags (Model model) : this()
         => FileFormats = model.FormatModel.Data;

        protected Diags()
         => this.QuestionAsk = QuestionAskDefault;

        public bool IsDigestForm => Scope != Granularity.Detail
                    && (hashFlags & (Hashes.FileMD5|Hashes.FileSHA1|Hashes.FileSHA256|Hashes.MetaSHA1|Hashes.MediaSHA1)) != 0;

        private Hashes hashFlags = 0;
        public Hashes HashFlags
        {
            get => hashFlags;
            set => hashFlags = value | (hashFlags & (Hashes) 0x7FFF0000);
        }

        public bool IsPcmMD5CheckEnabled
        {
            get => (hashFlags & Hashes.PcmMD5) != 0;
            set
            {
                hashFlags = value ? hashFlags | Hashes.PcmMD5 : hashFlags & ~ Hashes.PcmMD5;
                RaisePropertyChanged (nameof (IsPcmMD5CheckEnabled));
            }
        }

        public bool IsFlacRipCheckEnabled
        {
            get => (hashFlags & Hashes._FlacMatch) != 0;
            set
            {
                hashFlags = value ? hashFlags | Hashes._FlacMatch : hashFlags & ~Hashes._FlacMatch;
                RaisePropertyChanged (nameof (IsFlacRipCheckEnabled));
            }
        }

        public bool IsMp3RipCheckEnabled { get; set; }

        public bool IsFlacTagsCheckEnabled
        {
            get => (hashFlags & Hashes._FlacTags) != 0;
            set
            {
                hashFlags = value ? hashFlags | Hashes._FlacTags : hashFlags & ~Hashes._FlacTags;
                RaisePropertyChanged (nameof (IsFlacTagsCheckEnabled));
            }
        }

        public bool IsEacWebCheckEnabled
        {
            get => (hashFlags & Hashes._WebCheck) != 0;
            set
            {
                hashFlags = value ? hashFlags | Hashes._WebCheck : hashFlags & ~Hashes._WebCheck;
                RaisePropertyChanged (nameof (IsEacWebCheckEnabled));
            }
        }

        public bool IsStrict
        {
            get => (WarnEscalator & IssueTags.StrictWarn) != 0 && (ErrEscalator & IssueTags.StrictErr) != 0;
            set
            {
                WarnEscalator = value ? WarnEscalator | IssueTags.StrictWarn : WarnEscalator & ~(IssueTags.StrictWarn);
                ErrEscalator = value ? ErrEscalator | IssueTags.StrictErr : ErrEscalator & ~ IssueTags.StrictErr;
            }
        }

        public virtual bool IsRepairEnabled
        {
            get => Response != Interaction.None;
            set
            {
                Response = value ? Interaction.PromptToRepair : Interaction.None;
                RaisePropertyChanged (nameof (IsRepairEnabled));
            }
        }

        public string FormatListText
        {
            get
            {
                var sb = new StringBuilder();

                foreach (var item in FileFormats.Items)
                    if ((item.Subname == null || item.Subname[0] != '*'))
                    {
                        if (sb.Length != 0)
                            sb.Append (", ");
                        sb.Append (item.LongName);
                    }

                return sb.ToString();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged (string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler (this, new PropertyChangedEventArgs (propertyName));
        }

        public IList<string> GetReportRollups (string verb)
        {
            var report = new List<string>();
            var sb = new StringBuilder();

            // Get displayed length for right alignment.
            string fmt = "{0," + TotalFiles.ToString().Length + "}";

            if (TotalFiles != 1)
                report.Add (String.Format (fmt + " total files " + verb, TotalFiles));

            foreach (var item in FileFormats.Items)
            {
                string par = "";
                if (item.TotalHeaderErrors != 0)
                {
                    par = " (" + item.TotalHeaderErrors + " header CRC error";
                    if (item.TotalHeaderErrors > 1)
                        par += 's';
                }
                if (item.TotalDataErrors != 0)
                {
                    par += String.IsNullOrEmpty (par)? " (" : ", ";
                    par += item.TotalDataErrors + " data CRC error";
                    if (item.TotalDataErrors > 1)
                        par += 's';
                }
                if (item.TotalMisnamed != 0)
                    par += (String.IsNullOrEmpty (par)? " (" : ", ") + item.TotalMisnamed + " misnamed";
                if (item.TotalMissing != 0)
                    par += (String.IsNullOrEmpty (par)? " (" : ", ") + item.TotalMissing + " missing";
                if (item.TotalCreated != 0)
                    par += (String.IsNullOrEmpty (par)? " (" : ", ") + item.TotalCreated + " created";
                if (item.TotalConverted != 0)
                    par += (String.IsNullOrEmpty (par)? " (" : ", ") + item.TotalConverted + " converted";
                if (item.TotalSigned != 0)
                    par += (String.IsNullOrEmpty (par)? " (" : ", ") + item.TotalSigned + " signed";

                if (! String.IsNullOrEmpty (par))
                    par += ")";

                if (item.TrueTotal == 0 && String.IsNullOrEmpty (par))
                    continue;

                sb.Clear();
                sb.AppendFormat (fmt + " " + item.Names[0], item.TrueTotal);
                if (item.Subname != null)
                    sb.Append (" (" + item.Subname + ")");
                sb.Append (" file");
                if (item.TrueTotal != 1)
                    sb.Append ('s');

                sb.Append (par);
                report.Add (sb.ToString());
            }

            if (TotalRepairable > 0)
            {
                sb.Clear();
                sb.AppendFormat (fmt + " file", TotalRepairable);
                if (TotalRepairable != 1)
                    sb.Append ('s');
                sb.Append (" with repairable issues");
                report.Add (sb.ToString());
            }

            if (TotalRepairs > 0)
            {
                sb.Clear();
                sb.AppendFormat (fmt + " repair", TotalRepairs);
                if (TotalRepairs != 1)
                    sb.Append ('s');
                sb.Append (" made");
                report.Add (sb.ToString());
            }

            if (TotalFiles > 0)
            {
                sb.Clear();
                sb.AppendFormat (fmt + " file", TotalWarnings);
                if (TotalWarnings != 1)
                    sb.Append ('s');
                sb.Append (" with warnings");
                if (TotalErrors == 0 && TotalWarnings == 0)
                {
                    sb.Append (" or errors");
                    report.Add (sb.ToString());
                }
                else
                {
                    report.Add (sb.ToString());
                    sb.Clear();
                    sb.AppendFormat (fmt + " file", TotalErrors);
                    if (TotalErrors != 1)
                        sb.Append ('s');
                    sb.Append (" with errors");
                    report.Add (sb.ToString());
                }
            }

            return report;
        }

        // Model should replace this default.
        public bool? QuestionAskDefault (string prompt)
         => null;

        public void OnMessageSend (string message, Severity severity=Severity.NoIssue)
        {
            if (MessageSend != null)
                MessageSend (message, severity);
        }

        public void OnReportClose()
        {
            if (ReportClose != null)
                ReportClose();
        }

        public void OnFileVisit (string directoryName, string fileName)
        {
            if (FileVisit != null)
                FileVisit (directoryName, fileName);
        }
    }
}
