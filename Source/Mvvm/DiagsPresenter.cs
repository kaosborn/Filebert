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
            private readonly RelayCommand<object> navToFlac;
            public IDiagsUi Ui { get; private set; }
            public new DiagsPresenter Data => (DiagsPresenter) _data;

            public Model (IDiagsUi ui)
            {
                this.Ui = ui;
                this._data = new DiagsPresenter (this);

                foreach (string heading in Ui.GetHeadings())
                    Data.tabInfos.Add (new TabInfo.Model (heading, Data.tabInfos.Count));

                Data.TabApe = GetTabInfoData ("ape");
                Data.TabAsf = GetTabInfoData ("asf");
                Data.TabAvi = GetTabInfoData ("avi");
                Data.TabCue = GetTabInfoData ("cue");
                Data.TabFlac = GetTabInfoData ("flac");
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

                navToFlac = new RelayCommand<object> ((object obj) =>
                {
                    if (obj is LogTrack track)
                    {
                        int ix = Data.TabFlac.IndexOf (track.Match);
                        if (ix >= 0)
                        {
                            Data.TabFlac.Index = ix;
                            Data.CurrentTabNumber = Data.TabFlac.TabPosition;
                            Data.RaisePropertyChangedEvent (null);
                        }
                    }
                });

                base.Data.FileVisit += Ui.FileProgress;
                base.Data.MessageSend += Ui.ShowLine;
            }

            public TabInfo.Model GetTabInfoModel (string longName)
             => Data.tabInfos.FirstOrDefault (ti => ti.Data.LongName == longName);

            public TabInfo GetTabInfoData (string longName)
             => Data.tabInfos.FirstOrDefault (ti => ti.Data.LongName == longName)?.Data;

            public void GetFirst()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
                if (tInfo.Count > 0)
                {
                    tInfo.Index = 0;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetLast()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
                if (tInfo != null && tInfo.Count > 0)
                {
                    tInfo.Index = tInfo.Count - 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetPrev()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
                if (tInfo != null && tInfo.Index > 0)
                {
                    tInfo.Index = tInfo.Index - 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetNext()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
                if (tInfo != null && tInfo.Count > 0)
                {
                    tInfo.Index = tInfo.Index + 1;
                    Data.RaisePropertyChangedEvent (null);
                }
            }

            public void GetFirstRepair()
            {
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
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
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
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
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
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
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
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
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
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
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
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
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
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
                TabInfo tInfo = Data.tabInfos[Data.CurrentTabNumber].Data;
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
                            TabInfo.Model tInfo = GetTabInfoModel (parsing.Data.LongName);
                            if (tInfo != null)
                            {
                                if (newTabInfoIx < 0 || parsing is LogFormat.Model || parsing is LogEacFormat.Model)
                                { newTabInfoIx = tInfo.Data.TabPosition; newTabInfoFmtIx = tInfo.Data.Count; }

                                tInfo.Add (parsing);
                                if (parsing is LogFormat.Model log)
                                    log.SetNavCommand (navToFlac);
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
                    Ui.ShowLine (err, Severity.Error);

                Ui.ShowSummary (Data.GetReportRollups ("checked"));

                for (int ix = 1; ix < Data.tabInfos.Count; ++ix)
                {
                    var tInfo = Data.tabInfos[ix];
                    if (tInfo.Data.Index < 0 && tInfo.Data.Count > 0)
                    {
                        tInfo.Data.Index = 0;
                        Data.RaisePropertyChangedEvent (null);
                    }
                }

                Data.Progress = null;
                if (newTabInfoIx > 0)
                {
                    Data.tabInfos[newTabInfoIx].Data.Index = newTabInfoFmtIx;
                    Data.CurrentTabNumber = newTabInfoIx;
                }

                Data.IsBusy = false;
                ++Data.JobCounter;
            }
        }


        private List<TabInfo.Model> tabInfos = new List<TabInfo.Model>();

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

        private DiagsPresenter (DiagsPresenter.Model model) : base (model)
        {
            Scope = Granularity.Verbose;
            HashFlags = Hashes.Intrinsic;
            ValidationFlags = Validations.Exists;
            Response = Interaction.None;

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
            DoConsoleZoomMinus = new RelayCommand (() => model.Ui.ConsoleZoom (-1));
            DoConsoleZoomPlus = new RelayCommand (() => model.Ui.ConsoleZoom (+1));

            DoCopyLValueUpper = new RelayCommand<object> ((object obj) =>
            {
                if (obj is string tag)
                {
                    int eqPos = tag.IndexOf ('=');
                    if (eqPos > 0)
                        Clipboard.SetText (tag.Substring (0, eqPos).ToUpper());
                }
            });

            DoCopyRValue = new RelayCommand<object> ((object obj) =>
            {
                if (obj is string tag)
                {
                    int eqPos = tag.IndexOf ('=');
                    if (eqPos >= 0)
                        Clipboard.SetText (tag.Substring (eqPos+1));
                }
            });

            DoRepair = new RelayCommand<object> ((object obj) =>
            {
                TabInfo.Model tim = tabInfos[CurrentTabNumber];
                if (tim.Repair ((int) obj))
                    RaisePropertyChangedEvent (null);
            });
        }

        public void OnFileDrop (string[] paths)
        {
            if (paths.Length > 0)
                Root = paths[0];
        }
    }
}
