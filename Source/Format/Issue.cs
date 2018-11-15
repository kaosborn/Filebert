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

                public Issue Add (string message, Severity severity=Severity.Error, IssueTags tag=IssueTags.None,
                                  string prompt=null, Func<bool,string> repairer=null, bool isFinalRepairer=false)
                {
                    System.Diagnostics.Debug.Assert ((prompt==null) == (repairer==null));

                    if (repairer != null)
                    {
                        // Force Warning as minimum for repairables.
                        if (severity < Severity.Warning)
                            severity = Severity.Warning;
                        ++Data.RepairableCount;
                    }

                    var issue = new Issue (this, message, severity, tag, prompt, repairer, isFinalRepairer);
                    Data.items.Add (issue);

                    Severity level = issue.Level;
                    if (Data.MaxSeverity < level)
                    {
                        Data.MaxSeverity = level;
                        Data.Severest = issue;
                    }

                    Data.RaisePropertyChanged (nameof (FixedMessage));
                    return issue;
                }

                public void Escalate (IssueTags warnEscalator, IssueTags errEscalator)
                {
                    // Accumulate escalations.
                    Data.WarnEscalator |= warnEscalator;
                    Data.ErrEscalator |= errEscalator;

                    foreach (var issue in Data.items)
                    {
                        Severity level = issue.Level;
                        if (Data.MaxSeverity < level)
                        {
                            Data.MaxSeverity = level;
                            Data.Severest = issue;
                        }
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
                        foreach (var item in Data.items)
                            if (item.Repairer != null && item.IsRepairSuccessful == null)
                                item.RaisePropertyChanged (null);
                    }
                    return issue.RepairError;
                }
            }


            private ObservableCollection<Issue> items;
            public ReadOnlyObservableCollection<Issue> Items { get; private set; }

            public event PropertyChangedEventHandler PropertyChanged;
            public void RaisePropertyChanged (string propName)
            { if (PropertyChanged != null) PropertyChanged (this, new PropertyChangedEventArgs (propName)); }

            public IssueTags WarnEscalator { get; private set; }
            public IssueTags ErrEscalator { get; private set; }
            public Severity MaxSeverity { get; private set; }
            public Issue Severest { get; private set; }
            public int RepairableCount { get; private set; }

            public bool HasError => MaxSeverity >= Severity.Error;
            public bool HasFatal => MaxSeverity >= Severity.Fatal;

            private Vector (IssueTags warnEscalator = IssueTags.None, IssueTags errEscalator=IssueTags.None)
            {
                this.items = new ObservableCollection<Issue>();
                this.Items = new ReadOnlyObservableCollection<Issue> (this.items);
                this.MaxSeverity = Severity.NoIssue;
                this.WarnEscalator = warnEscalator;
                this.ErrEscalator = errEscalator;
            }
        }


        private readonly Vector.Model model;

        public int Index { get; private set; }
        public string Message { get; private set; }
        public Severity BaseLevel { get; private set; }
        public IssueTags Tag { get; private set; }
        public string RepairPrompt { get; private set; }
        public string RepairError { get; private set; }
        private Func<bool,string> Repairer { get; set; }
        public bool IsFinalRepairer { get; private set; }
        public bool? IsRepairSuccessful { get; private set; }
        public bool IsNoise => Level <= Severity.Noise;

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged (string propName)
        { if (PropertyChanged != null) PropertyChanged (this, new PropertyChangedEventArgs (propName)); }

        private Issue (Vector.Model owner, string message, Severity level=Severity.Advisory, IssueTags tag=IssueTags.None,
                      string prompt=null, Func<bool,string> repairer=null, bool isFinalRepairer=false)
        {
            this.model = owner;
            this.Index = owner.Data.Items.Count;
            this.Message = message;
            this.BaseLevel = level;
            this.Tag = tag;
            this.RepairPrompt = prompt;
            this.Repairer = repairer;
            this.IsFinalRepairer = isFinalRepairer;
        }

        public Severity Level
        {
            get
            {
                Severity result = BaseLevel;
                if (result < Severity.Error)
                    if ((Tag & model.Data.ErrEscalator) != 0)
                        result = Severity.Error;
                    else if ((Tag & model.Data.WarnEscalator) != 0)
                        result = Severity.Warning;
                return result;
            }
        }

        public string FixedMessage
        {
            get
            {
                string result = Message;
                if (Level >= Severity.Warning)
                    result = $"{Level}: {result}";
                if (IsRepairSuccessful == true)
                    result += " (repair successful)";
                else if (RepairError != null)
                    result += $" (repair failed: {RepairError})";
                return result;
            }
        }

        public bool Failure => (Tag & IssueTags.Failure) != 0;
        public bool Success => (Tag & IssueTags.Success) != 0;
        public bool HasRepairer => Repairer != null;
        public bool IsRepairable => model.Data.RepairableCount > 0 && Repairer != null && IsRepairSuccessful == null && Level < Severity.Error;
        public bool IsReportable (Granularity granularity) => (int) Level >= (int) granularity;
        public override string ToString() => FixedMessage;
    }
}
