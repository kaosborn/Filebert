using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KaosIssue;
using KaosFormat;
using KaosDiags;
using KaosMvvm;

namespace AppViewModel
{
    // The ViewModel binding class of Model-View-ViewModel.
    public class DiagsPresenter : Diags, IFileDragDropTarget
    {
        // The ViewModel API of Model-View-ViewModel.
        public new class Model : Diags.Model
        {
            private readonly TabInfo.Model tabFlacModel;
            public IDiagsUi Ui { get; private set; }
            public new DiagsPresenter Data => (DiagsPresenter) _data;

            public Model (IDiagsUi ui)
            {
                Ui = ui;
                _data = new DiagsPresenter (this);

                foreach (string heading in Ui.GetHeadings())
                    Data.tabInfos.Add (new TabInfo.Model (heading, Data.tabInfos.Count));

                Data.TabApe = GetTabInfoData ("ape");
                Data.TabAsf = GetTabInfoData ("asf");
                Data.TabAvi = GetTabInfoData ("avi");
                Data.TabCue = GetTabInfoData ("cue");
                tabFlacModel = GetTabInfoModel ("flac");
                Data.TabFlac = tabFlacModel.Data;
                Data.TabFlv = GetTabInfoData ("flv");
                Data.TabGif = GetTabInfoData ("gif");
                Data.TabIco = GetTabInfoData ("ico");
                Data.TabJpg = GetTabInfoData ("jpg");
                Data.TabLogEac = GetTabInfoData ("log (EAC)");
                Data.TabLogXld = GetTabInfoData ("log (XLD)");
                Data.TabM3u = GetTabInfoData ("m3u");
                Data.TabM3u8 = GetTabInfoData ("m3u8");
                Data.TabM4a = GetTabInfoData ("m4a");
                Data.TabMd5 = GetTabInfoData ("md5");
                Data.TabMkv = GetTabInfoData ("mkv");
                Data.TabMov = GetTabInfoData ("mov");
                Data.TabMpg = GetTabInfoData ("mpg");
                Data.TabMp3 = GetTabInfoData ("mp3");
                Data.TabMp4 = GetTabInfoData ("mp4");
                Data.TabOgg = GetTabInfoData ("ogg");
                Data.TabPng = GetTabInfoData ("png");
                Data.TabSha1 = GetTabInfoData ("sha1");
                Data.TabSha1x = GetTabInfoData ("sha1x");
                Data.TabSha256 = GetTabInfoData ("sha256");
                Data.TabWav = GetTabInfoData ("wav");

                base.Data.FileVisit += Ui.FileProgress;
                base.Data.MessageSend += Ui.ShowLine;
            }

            public TabInfo.Model GetTabInfoModel (string longName)
             => Data.tabInfos.FirstOrDefault (ti => ti.Data.LongName == longName);

            public TabInfo GetTabInfoData (string longName)
             => Data.tabInfos.FirstOrDefault (ti => ti.Data.LongName == longName)?.Data;

            public bool SetFlacIndex (int index)
             => tabFlacModel.SetIndex (index);

            public void GetFirst()
            {
                if (Data.tabInfos[Data.CurrentTabNumber].SetIndex (0))
                    Data.RaisePropertyChanged (null);
            }

            public void GetLast()
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                if (tiModel.SetIndex (tiModel.Data.Count - 1))
                    Data.RaisePropertyChanged (null);
            }

            public void GetPrev()
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                if (tiModel.SetIndex (tiModel.Data.Index - 1))
                    Data.RaisePropertyChanged (null);
            }

            public void GetNext()
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                if (tiModel.SetIndex (tiModel.Data.Index + 1))
                    Data.RaisePropertyChanged (null);
            }

            public void GetFirstRepair()
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                for (int ix = 0; ix < tiModel.Data.Count; ++ix)
                    if (tiModel.Data.GetIsRepairable (ix))
                    {
                        if (tiModel.SetIndex (ix))
                            Data.RaisePropertyChanged (null);
                        break;
                    }
            }

            public void GetLastRepair()
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                for (int ix = tiModel.Data.Count; --ix >= 0; )
                    if (tiModel.Data.GetIsRepairable (ix))
                    {
                        if (tiModel.SetIndex (ix))
                            Data.RaisePropertyChanged (null);
                        break;
                    }
            }

            public void GetPrevRepair()
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                for (int ix = tiModel.Data.Index; --ix >= 0; )
                    if (tiModel.Data.GetIsRepairable (ix))
                    {
                        if (tiModel.SetIndex (ix))
                            Data.RaisePropertyChanged (null);
                        break;
                    }
            }

            public void GetNextRepair()
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                for (int ix = tiModel.Data.Index; ++ix < tiModel.Data.Count; )
                    if (tiModel.Data.GetIsRepairable (ix))
                    {
                        if (tiModel.SetIndex (ix))
                            Data.RaisePropertyChanged (null);
                        break;
                    }
            }

            public void GetFirstBySeverity (Severity badness)
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                for (int ix = 0; ix < tiModel.Data.Count; ++ix)
                    if (tiModel.Data.GetMaxSeverity (ix) >= badness)
                    {
                        if (tiModel.SetIndex (ix))
                            Data.RaisePropertyChanged (null);
                        break;
                    }
            }

            public void GetLastBySeverity (Severity badness)
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                for (int ix = tiModel.Data.Count; --ix >= 0; )
                    if (tiModel.Data.GetMaxSeverity (ix) >= badness)
                    {
                        if (tiModel.SetIndex (ix))
                            Data.RaisePropertyChanged (null);
                        break;
                    }
            }

            public void GetPrevBySeverity (Severity badness)
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                for (int ix = tiModel.Data.Index; --ix >= 0; )
                    if (tiModel.Data.GetMaxSeverity (ix) >= badness)
                    {
                        if (tiModel.SetIndex (ix))
                            Data.RaisePropertyChanged (null);
                        break;
                    }
            }

            public void GetNextBySeverity (Severity badness)
            {
                TabInfo.Model tiModel = Data.tabInfos[Data.CurrentTabNumber];
                for (int ix = tiModel.Data.Index; ++ix < tiModel.Data.Count; )
                    if (tiModel.Data.GetMaxSeverity (ix) >= badness)
                    {
                        if (tiModel.SetIndex (ix))
                            Data.RaisePropertyChanged (null);
                        break;
                    }
            }

            public void Parse()
            {
                Data.IsBusy = true;
                Data.ProgressCounter = 0;
                Data.ProgressText = "Starting...";
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
                    {
                        ++Data.ProgressCounter;
                        if (Data.ProgressTotal != 0)
                            Data.ProgressPercent = 100 * Data.ProgressCounter / Data.ProgressTotal;
                        if (parsing != null)
                        {
                            Data.ProgressText = parsing.Data.Name;
                            TabInfo.Model tiModel = GetTabInfoModel (parsing.Data.LongName);
                            if (tiModel != null)
                            {
                                if (newTabInfoIx < 0 || parsing is LogFormat.Model || parsing is LogEacFormat.Model)
                                { newTabInfoIx = tiModel.Data.TabPosition; newTabInfoFmtIx = tiModel.Data.Count; }

                                tiModel.Add (parsing);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException)
                { jobArgs.Result = ex.Message; }
            }

            void JobCompleted (object sender, RunWorkerCompletedEventArgs args)
            {
                var err = (string) args.Result;

                if (err != null)
                    Ui.ShowLine (err, Severity.Error);

                Ui.ShowSummary (Data.GetReportRollups ("checked"));

                for (int ix = 1; ix < Data.tabInfos.Count; ++ix)
                {
                    TabInfo.Model tiModel = Data.tabInfos[ix];
                    if (tiModel.Data.Index < 0)
                        if (tiModel.SetIndex (0))
                            Data.RaisePropertyChanged (null);
                }

                if (newTabInfoIx > 0)
                {
                    Data.tabInfos[newTabInfoIx].SetIndex (newTabInfoFmtIx);
                    Data.CurrentTabNumber = newTabInfoIx;
                }

                Data.ProgressPercent = 0;
                Data.ProgressText = null;
                Data.IsBusy = false;
                Data.ProgressTotal = 0;
                ++Data.JobCounter;
            }
        }


        private List<TabInfo.Model> tabInfos = new List<TabInfo.Model>();

        public int JobCounter { get; private set; } = 0;  // For test.

        public TabInfo TabApe { get; private set; }
        public TabInfo TabAsf { get; private set; }
        public TabInfo TabAvi { get; private set; }
        public TabInfo TabCue { get; private set; }
        public TabInfo TabFlac { get; private set; }
        public TabInfo TabFlv { get; private set; }
        public TabInfo TabGif { get; private set; }
        public TabInfo TabIco { get; private set; }
        public TabInfo TabJpg { get; private set; }
        public TabInfo TabLogEac { get; private set; }
        public TabInfo TabLogXld { get; private set; }
        public TabInfo TabM3u { get; private set; }
        public TabInfo TabM3u8 { get; private set; }
        public TabInfo TabM4a { get; private set; }
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
            set { isBusy = value; RaisePropertyChanged (nameof (IsBusy)); }
        }

        private double progressFactor = 0;
        public double ProgressFactor
        {
            get => progressFactor;
            private set { progressFactor = value; RaisePropertyChanged (nameof (ProgressFactor)); }
        }

        private int progressPercent = 0;
        public int ProgressPercent
        {
            get => progressPercent;
            set
            {
                if (progressPercent != value)
                {
                    progressPercent = value;
                    progressFactor = progressPercent / 100.0;
                    RaisePropertyChanged (nameof (ProgressPercent));
                    RaisePropertyChanged (nameof (progressFactor));
                }
            }
        }

        private string progressText = "Ready";
        public string ProgressText
        {
            get => progressText;
            private set { progressText = value?? "Ready"; RaisePropertyChanged (nameof (ProgressText)); }
        }

        private int currentTabNumber;
        public int CurrentTabNumber 
        {
            get => currentTabNumber;
            set { currentTabNumber = value; RaisePropertyChanged (null); }
        }

        public bool TabIsFormat => CurrentTabNumber != 0;
        public string CurrentTabText
        {
            get
            {
                if (CurrentTabNumber == 0)
                    return null;
                var ti = tabInfos[CurrentTabNumber];
                return (ti.Data.Index+1).ToString() + " of " + ti.Data.Count + " ." + tabInfos[CurrentTabNumber].Data.LongName;
            }
        }

        public bool TabHasErrors => CurrentTabNumber > 0 && tabInfos[CurrentTabNumber].Data.ErrorCount > 0;
        public string TabErrorText
        {
            get
            {
                if (CurrentTabNumber == 0)
                    return null;
                var ti = tabInfos[CurrentTabNumber];
                return ti.Data.ErrorCount.ToString() + " failed";
            }
        }

        public bool TabHasRepairables => CurrentTabNumber > 0 && tabInfos[CurrentTabNumber].Data.RepairableCount > 0;
        public string TabRepairablesText
        {
            get
            {
                if (CurrentTabNumber == 0)
                    return null;
                var ti = tabInfos[CurrentTabNumber];
                var result = ti.Data.RepairableCount.ToString();
                return result + (ti.Data.RepairableCount == 1 ? " repairable" : " repairables");
            }
        }

        public override bool IsRepairEnabled
        {
            get => Response != Interaction.None;
            set => Response = value ? Interaction.RepairLater : Interaction.None;
        }

        public Hashes HashToggle
        {
            get => HashFlags;
            set => HashFlags = value < 0 ? (HashFlags & (Hashes) value) : (HashFlags | (Hashes) value);
        }

        public Validations ValidationToggle
        {
            get => ValidationFlags;
            set => ValidationFlags = value < 0 ? (ValidationFlags & value) : (ValidationFlags | (Validations) value);
        }

        private int consoleZoom = 14;
        public int ConsoleZoom
        {
            get => consoleZoom;
            set
            {
                if (value >= 6 && value <= 60)
                { consoleZoom = value; RaisePropertyChanged (nameof (ConsoleZoom)); }
            }
        }

        public ICommand DoBrowse { get; private set; }
        public ICommand DoCheck { get; private set; }
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
        public ICommand DoCopyLValueUpper { get; private set; }
        public ICommand DoCopyRValue { get; private set; }
        public ICommand DoRepair { get; private set; }
        public ICommand NavToFlac { get; private set; }

        private DiagsPresenter (DiagsPresenter.Model model) : base (model)
        {
            Scope = Granularity.Verbose;
            Response = Interaction.None;
            HashFlags = Hashes.Intrinsic;
            ValidationFlags = Validations.Exists;

            DoBrowse = new RelayCommand (() => model.Data.Root = model.Ui.BrowseFile());
            DoCheck = new RelayCommand (() => model.Parse());
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
            DoConsoleZoomMinus = new RelayCommand (() => --ConsoleZoom);
            DoConsoleZoomPlus = new RelayCommand (() => ++ConsoleZoom);

            DoCopyLValueUpper = new RelayCommand<object>(
            (object obj) =>
            {
                if (obj is string tag)
                {
                    int eqPos = tag.IndexOf ('=');
                    if (eqPos > 0)
                        Clipboard.SetText (tag.Substring (0, eqPos).ToUpper());
                }
            });

            DoCopyRValue = new RelayCommand<object>(
            (object obj) =>
            {
                if (obj is string tag)
                {
                    int eqPos = tag.IndexOf ('=');
                    if (eqPos >= 0)
                        Clipboard.SetText (tag.Substring (eqPos + 1));
                }
            });

            DoRepair = new RelayCommand<object>(
            (object obj) =>
            {
                TabInfo.Model tiModel = tabInfos[CurrentTabNumber];
                if (tiModel.Repair ((int) obj))
                    RaisePropertyChanged (null);
            });

            NavToFlac = new RelayCommand<object>(
            (object obj) => { if (obj is LogTrack track)
                                if (model.SetFlacIndex (TabFlac.IndexOf (track.Match)))
                                { CurrentTabNumber = TabFlac.TabPosition; RaisePropertyChanged (null); }
                            },
            (object obj) => obj is LogTrack track && track.Match != null);
        }

        public void OnFileDrop (string[] paths)
        {
            if (paths.Length > 0)
                Root = paths[0];
        }
    }
}
