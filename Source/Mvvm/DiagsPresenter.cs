using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using KaosIssue;
using KaosFormat;
using KaosDiags;
using KaosMvvm;

namespace AppViewModel
{
    public class TabInfo
    {
        private List<FormatBase> parsings;
        public int TabPosition { get; private set; }
        public Severity MaxSeverity { get; private set; }
        public int ErrorCount { get; private set; }
        public int RepairableCount { get; private set; }
        public int Count => parsings.Count;
        public FormatBase Current => index < 0 ? null : parsings[index];
        public bool HasError => MaxSeverity >= Severity.Error;

        private int index = -1;
        public int Index
        {
            get => index;
            set
            {
                if (parsings.Count > 0 && value >= 0 && value < parsings.Count)
                    index = value;
            }
        }

        public TabInfo (int tabPosition)
        {
            TabPosition = tabPosition;
            parsings = new List<FormatBase>();
        }

        public void Add (FormatBase fmt)
        {
            parsings.Add (fmt);
            if (MaxSeverity < fmt.Issues.MaxSeverity)
                MaxSeverity = fmt.Issues.MaxSeverity;
            if (fmt.IsRepairable)
                ++RepairableCount;
            if (fmt.Issues.MaxSeverity >= Severity.Error)
                ++ErrorCount;
        }

        public bool GetIsRepairable (int ix)
         => parsings[ix].IsRepairable;

        public Severity GetMaxSeverity (int ix)
         => parsings[ix].Issues.MaxSeverity;
    }


    // The ViewModel binding class of Model-View-ViewModel.
    public class DiagsPresenter : Diags
    {
        // The ViewModel API of Model-View-ViewModel.
        public new class Model : Diags.Model
        {
            public IDiagsUi Ui { get; private set; }
            public new DiagsPresenter Data => (DiagsPresenter) _data;

            public Model (IDiagsUi ui)
            {
                this.Ui = ui;
                this._data = new DiagsPresenter (this);

                int ix = 0;
                foreach (string tabHeader in Ui.GetHeadings())
                {
                    if (tabHeader.StartsWith ("."))
                        Data.AddTabInfo (tabHeader.Substring (1), ix);
                    ++ix;
                }

                Data.TabAvi = Data.tabInfo["avi"];
                Data.TabCue = Data.tabInfo["cue"];
                Data.TabFlac = Data.tabInfo["flac"];
                Data.TabLogEac = Data.tabInfo["log (EAC)"];
                Data.TabM3u = Data.tabInfo["m3u"];
                Data.TabM3u8 = Data.tabInfo["m3u8"];
                Data.TabMd5 = Data.tabInfo["md5"];
                Data.TabMkv = Data.tabInfo["mkv"];
                Data.TabMp3 = Data.tabInfo["mp3"];
                Data.TabMp4 = Data.tabInfo["mp4"];
                Data.TabOgg = Data.tabInfo["ogg"];
                Data.TabPng = Data.tabInfo["png"];
                Data.TabSha1 = Data.tabInfo["sha1"];
                Data.TabSha1x = Data.tabInfo["sha1x"];
                Data.TabSha256 = Data.tabInfo["sha256"];
                Data.TabWav = Data.tabInfo["wav"];

                base.Data.FileVisit += Ui.FileProgress;
                base.Data.MessageSend += Ui.ShowLine;
            }

            public void GetFirst()
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null && tabInfo.Count > 0)
                {
                    tabInfo.Index = 0;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetLast()
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null && tabInfo.Count > 0)
                {
                    tabInfo.Index = tabInfo.Count - 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetPrev()
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null && tabInfo.Index > 0)
                {
                    tabInfo.Index = tabInfo.Index - 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetNext()
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null && tabInfo.Count > 0)
                {
                    tabInfo.Index = tabInfo.Index + 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetFirstRepair()
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null)
                    for (int ix = 0; ix < tabInfo.Count; ++ix)
                        if (tabInfo.GetIsRepairable (ix))
                        {
                            tabInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetLastRepair()
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null)
                    for (int ix = tabInfo.Count; --ix >= 0; )
                        if (tabInfo.GetIsRepairable (ix))
                        {
                            tabInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetPrevRepair()
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null)
                    for (int ix = tabInfo.Index; --ix >= 0; )
                        if (tabInfo.GetIsRepairable (ix))
                        {
                            tabInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetNextRepair()
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null)
                    for (int ix = tabInfo.Index; ++ix < tabInfo.Count; )
                        if (tabInfo.GetIsRepairable (ix))
                        {
                            tabInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetFirstBySeverity (Severity badness)
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null)
                    for (int ix = 0; ix < tabInfo.Count; ++ix)
                        if (tabInfo.GetMaxSeverity (ix) >= badness)
                        {
                            tabInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetLastBySeverity (Severity badness)
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null)
                    for (int ix = tabInfo.Count; --ix >= 0;)
                        if (tabInfo.GetMaxSeverity (ix) >= badness)
                        {
                            tabInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetPrevBySeverity (Severity badness)
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null)
                    for (int ix = tabInfo.Index; --ix >= 0; )
                        if (tabInfo.GetMaxSeverity (ix) >= badness)
                        {
                            tabInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetNextBySeverity (Severity badness)
            {
                TabInfo tabInfo = Data.CurrentTabFormatInfo;
                if (tabInfo != null)
                    for (int ix = tabInfo.Index; ++ix < tabInfo.Count; )
                        if (tabInfo.GetMaxSeverity (ix) >= badness)
                        {
                            tabInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void Parse()
            {
                Data.Progress = "Starting...";
                Data.IsBusy = true;
                var bg = new BackgroundWorker();
                bg.DoWork += Job;
                bg.RunWorkerCompleted += JobCompleted;
                bg.RunWorkerAsync();
            }

            int newTabInfoIx, newTabInfoFmtIx;
            void Job (object sender, DoWorkEventArgs jobArgs)
            {
                jobArgs.Result = (string) null;
                newTabInfoIx = newTabInfoFmtIx = -1;

                try
                {
                    foreach (FormatBase.Model parsing in CheckRoot())
                        if (parsing != null)
                        {
                            Data.Progress = parsing.Data.Name;
                            if (Data.tabInfo.TryGetValue (parsing.Data.LongName, out TabInfo tInfo))
                            {
                                if (newTabInfoIx < 0 || parsing is LogEacFormat.Model)
                                { newTabInfoIx = tInfo.TabPosition-1; newTabInfoFmtIx = tInfo.Count; }
                                tInfo.Add (parsing.Data);
                            }
                    }
                }
                catch (IOException ex)
                { jobArgs.Result = ex.Message; }
                catch (ArgumentException ex)
                { jobArgs.Result = ex.Message; }
            }

            void JobCompleted (object sender, RunWorkerCompletedEventArgs args)
            {
                var err = (string) args.Result;

                if (err != null)
                    Ui.ShowLine (err, Severity.Error, Likeliness.None);

                for (int ix = 0; ix < Data.tabInfo.Count; ++ix)
                {
                    var ti = Data.tabInfo.Values[ix];
                    if (ti.Index < 0 && ti.Count > 0)
                    {
                        ti.Index = 0;
                        Data.RaisePropertyChangedEvent (null);
                    }
                }

                Data.Progress = null;
                if (newTabInfoIx >= 0)
                {
                    Data.tabInfo.Values[newTabInfoIx].Index = newTabInfoFmtIx;
                    Data.CurrentTabNumber = newTabInfoIx+1;
                }

                ++Data.JobCounter;
                Data.IsBusy = false;
            }
        }


        private SortedList<string,TabInfo> tabInfo = new SortedList<string,TabInfo>();

        public TabInfo TabAvi { get; private set; }
        public TabInfo TabCue { get; private set; }
        public TabInfo TabFlac { get; private set; }
        public TabInfo TabLogEac { get; private set; }
        public TabInfo TabM3u { get; private set; }
        public TabInfo TabM3u8 { get; private set; }
        public TabInfo TabMd5 { get; private set; }
        public TabInfo TabMkv { get; private set; }
        public TabInfo TabMp3 { get; private set; }
        public TabInfo TabMp4 { get; private set; }
        public TabInfo TabOgg { get; private set; }
        public TabInfo TabPng { get; private set; }
        public TabInfo TabSha1 { get; private set; }
        public TabInfo TabSha1x { get; private set; }
        public TabInfo TabSha256 { get; private set; }
        public TabInfo TabWav { get; private set; }

        private bool isBusy = false;
        public bool IsBusy
        {
            get => isBusy;
            set
            {
                isBusy = value;
                RaisePropertyChangedEvent (nameof (IsBusy));
            }
        }

        private int currentTabNumber;
        public int CurrentTabNumber 
        {
            get => currentTabNumber;
            set
            {
                currentTabNumber = value;
                RaisePropertyChangedEvent (null);
            }
        }

        public int JobCounter { get; private set; } = 0;  // For unit tests.

        public bool TabIsFormat => CurrentTabNumber != 0;
        public string CurrentTabText
        {
            get
            {
                var ix = CurrentTabNumber - 1;
                if (ix < 0)
                    return null;
                var ti = tabInfo.Values[ix];
                return (ti.Index+1).ToString() + " of " + ti.Count + " ." + tabInfo.Keys.ElementAt (ix);
            }
        }

        public bool TabHasErrors => CurrentTabNumber > 0 && tabInfo.Values[CurrentTabNumber-1].ErrorCount > 0;
        public string TabErrorText
        {
            get
            {
                var ix = CurrentTabNumber - 1;
                if (ix < 0)
                    return null;
                var ti = tabInfo.Values[ix];
                var result = ti.ErrorCount.ToString();
                return result + (ti.ErrorCount == 1 ? " error" : " errors");
            }
        }

        public bool TabHasRepairables => CurrentTabNumber > 0 && tabInfo.Values[CurrentTabNumber-1].RepairableCount > 0;
        public string TabRepairablesText
        {
            get
            {
                var ix = CurrentTabNumber - 1;
                if (ix < 0)
                    return null;
                var ti = tabInfo.Values[ix];
                var result = ti.RepairableCount.ToString();
                return result + (ti.RepairableCount == 1 ? " repairable" : " repairables");
            }
        }

        private TabInfo CurrentTabFormatInfo
         => tabInfo.Values.FirstOrDefault (v => v.TabPosition == CurrentTabNumber);

        private void AddTabInfo (string formatName, int tabPosition)
         => tabInfo.Add (formatName, new TabInfo (tabPosition));

        private string progress = "Ready";
        public string Progress
        {
            get => progress;
            private set
            {
                progress = value?? "Ready";
                RaisePropertyChangedEvent (nameof (Progress));
            }
        }

        public override bool IsRepairEnabled
        {
            get => Response != Interaction.None;
            set => Response = value ? Interaction.RepairLater : Interaction.None;
        }

        public Hashes HashToggle
        {
            get { return HashFlags; }
            set { HashFlags = value < 0 ? (HashFlags & (Hashes) value) : (HashFlags | (Hashes) value); }
        }

        public Validations ValidationToggle
        {
            get { return ValidationFlags; }
            set { ValidationFlags = value < 0 ? (ValidationFlags & value) : (ValidationFlags | (Validations) value); }
        }

        public ICommand DoBrowse { get; private set; }
        public ICommand DoParse { get; private set; }
        public ICommand NavFirst { get; private set; }
        public ICommand NavLast { get; private set; }
        public ICommand NavPrev { get; private set; }
        public ICommand NavNext { get; private set; }
        public ICommand NavFirstError { get; private set; }
        public ICommand NavLastError { get; private set; }
        public ICommand NavPrevError { get; private set; }
        public ICommand NavNextError { get; private set; }
        public ICommand NavFirstRepair { get; private set; }
        public ICommand NavLastRepair { get; private set; }
        public ICommand NavPrevRepair { get; private set; }
        public ICommand NavNextRepair { get; private set; }
        public ICommand DoConsoleClear { get; private set; }
        public ICommand DoConsoleZoomMinus { get; private set; }
        public ICommand DoConsoleZoomPlus { get; private set; }

        private DiagsPresenter (DiagsPresenter.Model model) : base (model)
        {
            Scope = Granularity.Verbose;
            HashFlags = Hashes.Intrinsic;
            ValidationFlags = Validations.Exists;
            Response = Interaction.None;

            DoBrowse = new RelayCommand (() => model.Data.Root = model.Ui.BrowseFile());
            DoParse = new RelayCommand (() => model.Parse());
            NavFirst = new RelayCommand (() => model.GetFirst());
            NavLast = new RelayCommand (() => model.GetLast());
            NavPrev = new RelayCommand (() => model.GetPrev());
            NavNext = new RelayCommand (() => model.GetNext());
            NavFirstError = new RelayCommand (() => model.GetFirstBySeverity (Severity.Error));
            NavLastError = new RelayCommand (() => model.GetLastBySeverity (Severity.Error));
            NavPrevError = new RelayCommand (() => model.GetPrevBySeverity (Severity.Error));
            NavNextError = new RelayCommand (() => model.GetNextBySeverity (Severity.Error));
            NavFirstRepair = new RelayCommand (() => model.GetFirstRepair());
            NavLastRepair = new RelayCommand (() => model.GetLastRepair());
            NavPrevRepair = new RelayCommand (() => model.GetPrevRepair());
            NavNextRepair = new RelayCommand (() => model.GetNextRepair());
            DoConsoleClear = new RelayCommand (() => model.Ui.SetText (""));
            DoConsoleZoomMinus = new RelayCommand (() => model.Ui.ConsoleZoom (-1));
            DoConsoleZoomPlus = new RelayCommand (() => model.Ui.ConsoleZoom (+1));
        }
    }
}
