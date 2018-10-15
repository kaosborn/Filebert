﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using KaosIssue;
using KaosFormat;

namespace KaosDiags
{
    public enum Interaction { None, PromptToRepair, RepairLater };

    public delegate void MessageSendHandler (string message, Severity severity, Likeliness repairability);
    public delegate void ReportCloseHandler ();
    public delegate void FileVisitEventHandler (string dirName, string fileName);


    public partial class Diags : INotifyPropertyChanged
    {
        public FileFormat.Vector FileFormats { get; protected set; }

        public Func<string,bool?> QuestionAsk;
        public Func<string,string,char> InputChar;
        public Func<string,string,string,string> InputLine;
        public event MessageSendHandler MessageSend;
        public event ReportCloseHandler ReportClose;
        public event FileVisitEventHandler FileVisit;

        public string Product { get; set; }
        public string ProductVersion { get; set; }

        public string Root { get; set; }
        public string Filter { get; private set; }
        public string Exclusion { get; private set; }
        public Interaction Response { get; protected set; }
        public Granularity Scope { get; set; }
        public Hashes HashFlags { get; set; }
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
        public int TotalSignable { get; set; }
        public int ExpectedFiles { get; set; }

        protected Diags (Model model) : this()
         => this.FileFormats = model.FormatModel.Data;

        protected Diags()
         => this.QuestionAsk = QuestionAskDefault;

        public bool IsMd5CheckEnabled
        {
            get => (HashFlags & Hashes.PcmMD5) != 0;
            set
            {
                HashFlags = value ? HashFlags | Hashes.PcmMD5 : HashFlags & ~ Hashes.PcmMD5;
                RaisePropertyChangedEvent (nameof (IsMd5CheckEnabled));
            }
        }

        public bool IsRipCheckEnabled
        {
            get => (HashFlags & Hashes._LogCheck) != 0;
            set
            {
                HashFlags = value ? HashFlags | Hashes._LogCheck : HashFlags & ~Hashes._LogCheck;
                RaisePropertyChangedEvent (nameof (IsRipCheckEnabled));
            }
        }

        public bool IsWebCheckEnabled
        {
            get => (HashFlags & Hashes._WebCheck) != 0;
            set
            {
                HashFlags = value ? HashFlags | Hashes._WebCheck : HashFlags & ~Hashes._WebCheck;
                RaisePropertyChangedEvent (nameof (IsWebCheckEnabled));
            }
        }

        public bool IsFussy
        {
            get => (WarnEscalator & IssueTags.FussyWarn) != 0 && (ErrEscalator & IssueTags.FussyErr) != 0;
            set
            {
                WarnEscalator = value ? WarnEscalator | IssueTags.FussyWarn : WarnEscalator & ~(IssueTags.FussyWarn);
                ErrEscalator = value ? ErrEscalator | IssueTags.FussyErr : ErrEscalator & ~ IssueTags.FussyErr;
            }
        }

        public virtual bool IsRepairEnabled
        {
            get => Response != Interaction.None;
            set
            {
                Response = value ? Interaction.PromptToRepair : Interaction.None;
                RaisePropertyChangedEvent (nameof (IsRepairEnabled));
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
        protected void RaisePropertyChangedEvent (string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler (this, new PropertyChangedEventArgs (propertyName));
        }

        public IList<string> GetRollups (IList<string> rep, string verb)
        {
            var sb = new StringBuilder();

            // Get displayed length for right alignment.
            string fmt = "{0," + TotalFiles.ToString().Length + "}";

            if (TotalFiles != 1)
                rep.Add (String.Format (fmt + " total files " + verb, TotalFiles));

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
                rep.Add (sb.ToString());
            }

            if (TotalRepairable > 0)
            {
                sb.Clear();
                sb.AppendFormat (fmt + " file", TotalRepairable);
                if (TotalRepairable != 1)
                    sb.Append ('s');
                sb.Append (" with repairable issues");
                rep.Add (sb.ToString());
            }

            if (TotalRepairs > 0)
            {
                sb.Clear();
                sb.AppendFormat (fmt + " repair", TotalRepairs);
                if (TotalRepairs != 1)
                    sb.Append ('s');
                sb.Append (" made");
                rep.Add (sb.ToString());
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
                    rep.Add (sb.ToString());
                }
                else
                {
                    rep.Add (sb.ToString());
                    sb.Clear();
                    sb.AppendFormat (fmt + " file", TotalErrors);
                    if (TotalErrors != 1)
                        sb.Append ('s');
                    sb.Append (" with errors");
                    rep.Add (sb.ToString());
                }
            }

            return rep;
        }

        // Model should replace this default.
        public bool? QuestionAskDefault (string prompt)
         => null;

        public void OnMessageSend (string message, Severity severity=Severity.NoIssue, Likeliness repairability=Likeliness.None)
        {
            if (MessageSend != null)
                MessageSend (message, severity, repairability);
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
