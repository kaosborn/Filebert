using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KaosIssue
{
    public enum Severity
    { NoIssue, Noise, Trivia, Advisory, Warning, Error, Fatal };

    public enum Granularity
    { Detail=Severity.Noise, Verbose, Lucid, Terse, Quiet };

    [Flags]
    public enum IssueTags
    {
        None=0, HasId3v1=1, HasId3v24=2, Mp3HasApe=4, Substandard=8, Overstandard=0x10,
        BadTag=0x20, StrictWarn=0x100, StrictErr=0x200,
        Success=0x01000000, Failure=0x02000000
    }

    public class Issue : INotifyPropertyChanged
    {
        public class Vector : INotifyPropertyChanged
        {
            public class Model
            {
                public readonly Vector Data;

                public Model (IssueTags warnEscalator=IssueTags.None, IssueTags errEscalator=IssueTags.None)
                 => Data = new Issue.Vector (warnEscalator, errEscalator);

                public Issue Add (string message, Severity baseLevel=Severity.Error, IssueTags tags=IssueTags.None,
                                  string prompt=null, Func<bool,string> repairer=null, bool isFinalRepairer=false)
                {
                    System.Diagnostics.Debug.Assert ((prompt==null) == (repairer==null));

                    if (repairer != null)
                    {
                        // Force Warning as minimum for repairables.
                        if (baseLevel < Severity.Warning)
                            baseLevel = Severity.Warning;
                        ++Data.RepairableCount;
                    }

                    var issue = new Issue (Data, message, baseLevel, tags, prompt, repairer, isFinalRepairer);
                    Data.items.Add (issue);

                    Severity level = Data.GetLevel (issue.BaseLevel, issue.Tag);
                    if (Data.MaxSeverity < level)
                        Data.MaxSeverity = level;

                    Data.RaisePropertyChanged (nameof (LongMessage));
                    return issue;
                }

                public void Escalate (IssueTags warnEscalator, IssueTags errEscalator)
                {
                    // Accumulate escalations.
                    Data.WarnEscalator |= warnEscalator;
                    Data.ErrEscalator |= errEscalator;

                    foreach (var issue in Data.items)
                    {
                        Severity level = Data.GetLevel (issue.BaseLevel, issue.Tag);
                        if (Data.MaxSeverity < level)
                            Data.MaxSeverity = level;
                    }
                }

                public bool RepairerEquals (int index, Func<bool,string> other)
                 => Data.items[index].Repairer == other;

                public string Repair (int index)
                {
                    var issue = Data.items[index];
                    if (! issue.IsRepairable)
                        return "Error: Not repairable.";

                    issue.RepairError = issue.Repairer (Data.RepairableCount <= 1 || issue.IsFinalRepairer);
                    issue.IsRepairSuccessful = issue.RepairError == null;
                    issue.RaisePropertyChanged (null);
                    if (! issue.IsFinalRepairer)
                        --Data.RepairableCount;
                    else
                    {
                        Data.RepairableCount = 0;
                        foreach (Issue item in Data.items)
                            if (item.Repairer != null && item.IsRepairSuccessful == null)
                                item.RaisePropertyChanged (null);
                    }
                    return issue.RepairError;
                }
            }


            private readonly ObservableCollection<Issue> items;
            public ReadOnlyObservableCollection<Issue> Items { get; private set; }

            public event PropertyChangedEventHandler PropertyChanged;
            public void RaisePropertyChanged (string propName)
            { if (PropertyChanged != null) PropertyChanged (this, new PropertyChangedEventArgs (propName)); }

            public IssueTags WarnEscalator { get; private set; }
            public IssueTags ErrEscalator { get; private set; }
            public Severity MaxSeverity { get; private set; }
            public int RepairableCount { get; private set; }

            public Severity GetLevel (Severity baseLevel, IssueTags tags)
            {
                if (baseLevel < Severity.Warning)
                {
                    if ((tags & ErrEscalator) != 0)
                        return Severity.Error;
                    if ((tags & WarnEscalator) != 0)
                        return Severity.Warning;
                }
                else if (baseLevel == Severity.Warning && (tags & (ErrEscalator)) != 0)
                    return Severity.Error;
                return baseLevel;
            }

            public Severity MaxLevelWhereAny (IssueTags tags)
            {
                var result = Severity.NoIssue;
                foreach (Issue issue in items)
                    if ((issue.Tag & tags) != 0)
                    {
                        Severity level = GetLevel (issue.BaseLevel, issue.Tag);
                        if (result < level)
                            result = level;
                    }
                return result;
            }

            public bool HasError => MaxSeverity >= Severity.Error;
            public bool HasFatal => MaxSeverity >= Severity.Fatal;

            private Vector (IssueTags warnEscalator=IssueTags.None, IssueTags errEscalator=IssueTags.None)
            {
                this.items = new ObservableCollection<Issue>();
                this.Items = new ReadOnlyObservableCollection<Issue> (this.items);
                this.MaxSeverity = Severity.NoIssue;
                this.WarnEscalator = warnEscalator;
                this.ErrEscalator = errEscalator;
            }
        }


        private readonly Vector vector;

        public int Index { get; private set; }
        public string Message { get; private set; }
        public Severity BaseLevel { get; private set; }
        public IssueTags Tag { get; private set; }
        public string RepairPrompt { get; private set; }
        public string RepairError { get; private set; }
        private Func<bool,string> Repairer { get; set; }
        public bool IsFinalRepairer { get; private set; }
        public bool? IsRepairSuccessful { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged (string propName)
        { if (PropertyChanged != null) PropertyChanged (this, new PropertyChangedEventArgs (propName)); }

        private Issue (Vector owner, string message, Severity level=Severity.Advisory, IssueTags tag=IssueTags.None,
                      string prompt=null, Func<bool,string> repairer=null, bool isFinalRepairer=false)
        {
            this.vector = owner;
            this.Index = owner.Items.Count;
            this.Message = message;
            this.BaseLevel = level;
            this.Tag = tag;
            this.RepairPrompt = prompt;
            this.Repairer = repairer;
            this.IsFinalRepairer = isFinalRepairer;
        }

        public Severity Level => vector.GetLevel (BaseLevel, Tag);

        public string LongMessage
        {
            get
            {
                string result = Message;
                Severity level = vector.GetLevel (BaseLevel, Tag);
                if (level >= Severity.Warning)
                    if (level >= Severity.Error)
                        result = "Error: " + result;
                    else
                    {
                        result = "Warning: " + result;
                        if (Repairer != null)
                            if (IsRepairSuccessful == null)
                                result += " [repairable]";
                            else if (IsRepairSuccessful.Value == true)
                                result += " [repaired!]";
                            else if (RepairError != null)
                                result += $" [repair failed: {RepairError}]";
                    }
                return result;
            }
        }

        public bool Failure => (Tag & IssueTags.Failure) != 0;
        public bool Success => (Tag & IssueTags.Success) != 0;
        public bool HasRepairer => Repairer != null;
        public bool IsRepairable => vector.RepairableCount > 0 && Repairer != null && IsRepairSuccessful == null && vector.GetLevel (BaseLevel, Tag) < Severity.Error;
        public bool IsNoise => vector.GetLevel (BaseLevel, Tag) <= Severity.Noise;
        public bool IsReportable (Granularity granularity) => (int) vector.GetLevel (BaseLevel, Tag) >= (int) granularity;
        public string RepairQuestion => RepairPrompt + "?";
        public override string ToString() => LongMessage;
    }
}
