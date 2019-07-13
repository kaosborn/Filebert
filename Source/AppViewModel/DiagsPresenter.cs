using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KaosViewModel;
using KaosIssue;
using KaosFormat;
using KaosDiags;

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
            public DiagsPresenter ViewModel => (DiagsPresenter) _data;

            public Model (IDiagsUi ui)
            {
                Ui = ui;
                _data = new DiagsPresenter (this);

                foreach (string heading in Ui.GetHeadings())
                    Data.tiModels.Add (new TabInfo.Model (heading, Data.tiModels.Count));

                Data.TabAif = GetTabInfoData ("aif");
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

                base.Data.MessageSend += Ui.ShowLine;
                base.Data.PropertyChanged += Data.NotifyPropertyChanged;
            }

            public TabInfo.Model GetTabInfoModel (string longName)
             => Data.tiModels.FirstOrDefault (ti => ti.Data.LongName == longName);

            public TabInfo GetTabInfoData (string longName)
             => Data.tiModels.FirstOrDefault (ti => ti.Data.LongName == longName)?.Data;

            public bool SetFlacIndex (int index)
             => tabFlacModel.SetIndex (index);

            public void ShowContents()
            {
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
                FormatBase fmt = tiModel.GetFormatBase();
                if (Object.ReferenceEquals (fmt, Data.BaseFormat))
                    Data.BaseFormat = null;
                Data.BaseFormat = fmt;
            }

            public void GetFirst()
            {
                if (Data.tiModels[Data.CurrentTabNumber].SetIndex (0))
                    Data.RaisePropertyChanged (null);
            }

            public void GetLast()
            {
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
                if (tiModel.SetIndex (tiModel.Data.Count - 1))
                    Data.RaisePropertyChanged (null);
            }

            public void GetPrev()
            {
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
                if (tiModel.SetIndex (tiModel.Data.Index - 1))
                    Data.RaisePropertyChanged (null);
            }

            public void GetNext()
            {
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
                if (tiModel.SetIndex (tiModel.Data.Index + 1))
                    Data.RaisePropertyChanged (null);
            }

            public void GetFirstRepair()
            {
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
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
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
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
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
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
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
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
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
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
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
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
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
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
                TabInfo.Model tiModel = Data.tiModels[Data.CurrentTabNumber];
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
                if (! String.IsNullOrWhiteSpace (ViewModel.Root))
                {
                    var bg = new BackgroundWorker();
                    bg.DoWork += Job;
                    bg.RunWorkerCompleted += JobCompleted;
                    bg.RunWorkerAsync();
                }
            }

            int newTabInfoIx, newTabInfoFmtIx;
            void Job (object sender, DoWorkEventArgs jobArgs)
            {
                jobArgs.Result = (string) null;
                newTabInfoIx = newTabInfoFmtIx = -1;

                try
                {
                    foreach (FormatBase.Model parsing in CheckRoot (isTwoPass:true))
                        if (parsing != null)
                        {
                            TabInfo.Model tiModel = GetTabInfoModel (parsing.Data.FullName);
                            if (tiModel != null)
                            {
                                if (newTabInfoIx < 0 || parsing is LogFormat.Model || parsing is LogEacFormat.Model)
                                { newTabInfoIx = tiModel.Data.TabPosition; newTabInfoFmtIx = tiModel.Data.Count; }

                                tiModel.Add (parsing);
                            }
                        }
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException)
                { jobArgs.Result = ex.Message; }
            }

            void JobCompleted (object sender, RunWorkerCompletedEventArgs args)
            {
                SetCurrentFile (null, null);

                var err = (string) args.Result;
                if (err != null)
                    Ui.ShowLine (err, Severity.Error);

                ReportSummary ("checked");

                for (int ix = 1; ix < Data.tiModels.Count; ++ix)
                {
                    TabInfo.Model tiModel = Data.tiModels[ix];
                    if (tiModel.Data.Index < 0)
                        if (tiModel.SetIndex (0))
                            Data.RaisePropertyChanged (null);
                }

                if (newTabInfoIx > 0)
                {
                    Data.tiModels[newTabInfoIx].SetIndex (newTabInfoFmtIx);
                    Data.CurrentTabNumber = newTabInfoIx;
                }

                Data.ProgressCounter = null;
                ++Data.JobCounter;
            }
        }


        private readonly List<TabInfo.Model> tiModels = new List<TabInfo.Model>();

        public int JobCounter { get; private set; } = 0;  // For test.

        public TabInfo TabAif { get; private set; }
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

        private int tagHelpHits=0;
        public int TagHelpHits
        {
            get => tagHelpHits;
            set
            {
                tagHelpHits = value;
                RaisePropertyChanged (nameof (TagHelpHits));
            }
        }

        public FormatBase baseFormat=null;
        public FormatBase BaseFormat
        {
          get => baseFormat;
          private set { baseFormat = value; RaisePropertyChanged (nameof (BaseFormat)); }
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
                var ti = tiModels[CurrentTabNumber];
                return (ti.Data.Index+1).ToString() + " of " + ti.Data.Count + " ." + tiModels[CurrentTabNumber].Data.LongName;
            }
        }

        public bool TabHasErrors => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.ErrorCount > 0;
        public string TabErrorText
        {
            get
            {
                if (CurrentTabNumber == 0)
                    return null;
                var ti = tiModels[CurrentTabNumber];
                return ti.Data.ErrorCount.ToString() + " failed";
            }
        }

        public bool CanSeekFirst => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsFirstSeekable;
        public bool CanSeekLast => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsLastSeekable;
        public bool CanSeekPrev => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsPrevSeekable;
        public bool CanSeekNext => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsNextSeekable;
        public bool CanSeekFirstError => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsFirstErrorSeekable;
        public bool CanSeekLastError => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsLastErrorSeekable;
        public bool CanSeekPrevError => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsPrevErrorSeekable;
        public bool CanSeekNextError => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsNextErrorSeekable;
        public bool CanSeekFirstRepairable => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsFirstRepairableSeekable;
        public bool CanSeekLastRepairable => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsLastRepairableSeekable;
        public bool CanSeekPrevRepairable => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsPrevRepairableSeekable;
        public bool CanSeekNextRepairable => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.IsNextRepairableSeekable;

        public bool TabHasRepairables => CurrentTabNumber > 0 && tiModels[CurrentTabNumber].Data.RepairableCount > 0;
        public string TabRepairablesText
        {
            get
            {
                if (CurrentTabNumber == 0)
                    return null;
                var ti = tiModels[CurrentTabNumber];
                var result = ti.Data.RepairableCount.ToString();
                return result + (ti.Data.RepairableCount == 1 ? " repairable" : " repairables");
            }
        }

        public override bool IsRepairEnabled
        {
            get => Response != Interaction.None;
            set => SetResponse (value ? Interaction.RepairLater : Interaction.None);
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
                if (value < 6)
                    consoleZoom = 6;
                else if (value > 60)
                    consoleZoom = 60;
                else
                    consoleZoom = value;
                RaisePropertyChanged (nameof (ConsoleZoom));
            }
        }

        public ICommand DoBrowse { get; private set; }
        public ICommand DoCheck { get; private set; }
        public ICommand DoTagHelp { get; private set; }
        public ICommand NavContents { get; private set; }
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
            DoBrowse = new RelayCommand (() => model.Data.Root = model.Ui.BrowseFile());
            DoCheck = new RelayCommand (() => model.Parse());
            DoTagHelp = new RelayCommand (() => ++TagHelpHits);
            NavContents = new RelayCommand (() => model.ShowContents());
            NavFirst = new RelayCommand (() => model.GetFirst(), (object _) => model.Data.CanSeekFirst);
            NavLast = new RelayCommand (() => model.GetLast(), (object _) => model.Data.CanSeekLast);
            NavPrev = new RelayCommand (() => model.GetPrev(), (object _) => model.Data.CanSeekPrev);
            NavNext = new RelayCommand (() => model.GetNext(), (object _) => model.Data.CanSeekNext);
            NavFirstError = new RelayCommand (() => model.GetFirstBySeverity (Severity.Error), (object _) => model.Data.CanSeekFirstError);
            NavLastError = new RelayCommand (() => model.GetLastBySeverity (Severity.Error), (object _) => model.Data.CanSeekLastError);
            NavPrevError = new RelayCommand (() => model.GetPrevBySeverity (Severity.Error), (object _) => model.Data.CanSeekPrevError);
            NavNextError = new RelayCommand (() => model.GetNextBySeverity (Severity.Error), (object _) => model.Data.CanSeekNextError);
            NavFirstRepair = new RelayCommand (() => model.GetFirstRepair(), (object _) => model.Data.CanSeekFirstRepairable);
            NavLastRepair = new RelayCommand (() => model.GetLastRepair(), (object _) => model.Data.CanSeekLastRepairable);
            NavPrevRepair = new RelayCommand (() => model.GetPrevRepair(), (object _) => model.Data.CanSeekPrevRepairable);
            NavNextRepair = new RelayCommand (() => model.GetNextRepair(), (object _) => model.Data.CanSeekNextRepairable);
            DoConsoleClear = new RelayCommand (() => { model.Ui.SetConsoleText (""); ConsoleLinesReported = 0; });
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
                TabInfo.Model tiModel = tiModels[CurrentTabNumber];
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

        private void NotifyPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof (ProgressCounter))
                if (ProgressCounter == null)
                    ProgressPercent = 0;
                else if (ProgressTotal != 0)
                    ProgressPercent = 100 * ProgressCounter.Value / ProgressTotal;
        }
    }
}
