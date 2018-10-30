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

                foreach (string heading in Ui.GetHeadings())
                    Data.tabInfos.Add (new TabInfo (heading, Data.tabInfos.Count));

                Data.TabAvi = GetTabInfo ("avi");
                Data.TabCue = GetTabInfo ("cue");
                Data.TabFlac = GetTabInfo ("flac");
                Data.TabGif = GetTabInfo ("gif");
                Data.TabJpg = GetTabInfo ("jpg");
                Data.TabLogEac = GetTabInfo ("log (EAC)");
                Data.TabM3u = GetTabInfo ("m3u");
                Data.TabM3u8 = GetTabInfo ("m3u8");
                Data.TabMd5 = GetTabInfo ("md5");
                Data.TabMkv = GetTabInfo ("mkv");
                Data.TabMov = GetTabInfo ("mov");
                Data.TabMpg = GetTabInfo ("mpg");
                Data.TabMp3 = GetTabInfo ("mp3");
                Data.TabMp4 = GetTabInfo ("mp4");
                Data.TabOgg = GetTabInfo ("ogg");
                Data.TabPng = GetTabInfo ("png");
                Data.TabSha1 = GetTabInfo ("sha1");
                Data.TabSha1x = GetTabInfo ("sha1x");
                Data.TabSha256 = GetTabInfo ("sha256");
                Data.TabWav = GetTabInfo ("wav");

                base.Data.FileVisit += Ui.FileProgress;
                base.Data.MessageSend += Ui.ShowLine;
            }

            public TabInfo GetTabInfo (string longName)
             => Data.tabInfos.First (ti => ti.LongName == longName);

            public void GetFirst()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null && tInfo.Count > 0)
                {
                    tInfo.Index = 0;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetLast()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null && tInfo.Count > 0)
                {
                    tInfo.Index = tInfo.Count - 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetPrev()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null && tInfo.Index > 0)
                {
                    tInfo.Index = tInfo.Index - 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetNext()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null && tInfo.Count > 0)
                {
                    tInfo.Index = tInfo.Index + 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetFirstRepair()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null)
                    for (int ix = 0; ix < tInfo.Count; ++ix)
                        if (tInfo.GetIsRepairable (ix))
                        {
                            tInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetLastRepair()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null)
                    for (int ix = tInfo.Count; --ix >= 0; )
                        if (tInfo.GetIsRepairable (ix))
                        {
                            tInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetPrevRepair()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null)
                    for (int ix = tInfo.Index; --ix >= 0; )
                        if (tInfo.GetIsRepairable (ix))
                        {
                            tInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetNextRepair()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null)
                    for (int ix = tInfo.Index; ++ix < tInfo.Count; )
                        if (tInfo.GetIsRepairable (ix))
                        {
                            tInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetFirstBySeverity (Severity badness)
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null)
                    for (int ix = 0; ix < tInfo.Count; ++ix)
                        if (tInfo.GetMaxSeverity (ix) >= badness)
                        {
                            tInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetLastBySeverity (Severity badness)
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null)
                    for (int ix = tInfo.Count; --ix >= 0;)
                        if (tInfo.GetMaxSeverity (ix) >= badness)
                        {
                            tInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetPrevBySeverity (Severity badness)
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null)
                    for (int ix = tInfo.Index; --ix >= 0; )
                        if (tInfo.GetMaxSeverity (ix) >= badness)
                        {
                            tInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void GetNextBySeverity (Severity badness)
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber];
                if (tInfo != null)
                    for (int ix = tInfo.Index; ++ix < tInfo.Count; )
                        if (tInfo.GetMaxSeverity (ix) >= badness)
                        {
                            tInfo.Index = ix;
                            Data.RaisePropertyChangedEvent (null);
                            break;
                        }
            }

            public void Parse()
            {
                Data.IsBusy = true;
                Data.Progress = "Starting...";
                Ui.FileProgress (null, null);

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
                            var tInfo = GetTabInfo (parsing.Data.LongName);
                            if (tInfo != null)
                            {
                                if (newTabInfoIx < 0 || parsing is LogEacFormat.Model)
                                { newTabInfoIx = tInfo.TabPosition; newTabInfoFmtIx = tInfo.Count; }
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

                ReportSummary();

                for (int ix = 1; ix < Data.tabInfos.Count; ++ix)
                {
                    var tInfo = Data.tabInfos[ix];
                    if (tInfo.Index < 0 && tInfo.Count > 0)
                    {
                        tInfo.Index = 0;
                        Data.RaisePropertyChangedEvent (null);
                    }
                }

                Data.Progress = null;
                if (newTabInfoIx > 0)
                {
                    Data.tabInfos[newTabInfoIx].Index = newTabInfoFmtIx;
                    Data.CurrentTabNumber = newTabInfoIx;
                }

                ++Data.JobCounter;
                Data.IsBusy = false;
            }
        }


        private List<TabInfo> tabInfos = new List<TabInfo>();

        public TabInfo TabAvi { get; private set; }
        public TabInfo TabCue { get; private set; }
        public TabInfo TabFlac { get; private set; }
        public TabInfo TabGif { get; private set; }
        public TabInfo TabJpg { get; private set; }
        public TabInfo TabLogEac { get; private set; }
        public TabInfo TabM3u { get; private set; }
        public TabInfo TabM3u8 { get; private set; }
        public TabInfo TabMd5 { get; private set; }
        public TabInfo TabMkv { get; private set; }
        public TabInfo TabMov { get; private set; }
        public TabInfo TabMpg { get; private set; }
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
                if (CurrentTabNumber == 0)
                    return null;
                var ti = tabInfos[CurrentTabNumber];
                return (ti.Index+1).ToString() + " of " + ti.Count + " ." + tabInfos[CurrentTabNumber].LongName;
            }
        }

        public bool TabHasErrors => CurrentTabNumber > 0 && tabInfos[CurrentTabNumber].ErrorCount > 0;
        public string TabErrorText
        {
            get
            {
                if (CurrentTabNumber == 0)
                    return null;
                var ti = tabInfos[CurrentTabNumber];
                var result = ti.ErrorCount.ToString();
                return result + (ti.ErrorCount == 1 ? " error" : " errors");
            }
        }

        public bool TabHasRepairables => CurrentTabNumber > 0 && tabInfos[CurrentTabNumber].RepairableCount > 0;
        public string TabRepairablesText
        {
            get
            {
                if (CurrentTabNumber == 0)
                    return null;
                var ti = tabInfos[CurrentTabNumber];
                var result = ti.RepairableCount.ToString();
                return result + (ti.RepairableCount == 1 ? " repairable" : " repairables");
            }
        }

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
