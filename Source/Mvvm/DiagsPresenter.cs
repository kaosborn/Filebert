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
        public string ContextName { get; private set; }
        public Severity MaxSeverity { get; private set; }
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
        }

        public TabInfo SetContextName (string identifier)
        {
            ContextName = identifier;
            return this;
        }
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

                Data.TabAvi = Data.tabInfo["avi"].SetContextName (nameof (Data.TabAvi));
                Data.TabCue = Data.tabInfo["cue"].SetContextName (nameof (Data.TabCue));
                Data.TabFlac = Data.tabInfo["flac"].SetContextName (nameof (Data.TabFlac));
                Data.TabLogEac = Data.tabInfo["log (EAC)"].SetContextName (nameof (Data.TabLogEac));
                Data.TabM3u = Data.tabInfo["m3u"].SetContextName (nameof (Data.TabM3u));
                Data.TabM3u8 = Data.tabInfo["m3u8"].SetContextName (nameof (Data.TabM3u8));
                Data.TabMd5 = Data.tabInfo["md5"].SetContextName (nameof (Data.TabMd5));
                Data.TabMkv = Data.tabInfo["mkv"].SetContextName (nameof (Data.TabMkv));
                Data.TabMp3 = Data.tabInfo["mp3"].SetContextName (nameof (Data.TabMp3));
                Data.TabMp4 = Data.tabInfo["mp4"].SetContextName (nameof (Data.TabMp4));
                Data.TabOgg = Data.tabInfo["ogg"].SetContextName (nameof (Data.TabOgg));
                Data.TabPng = Data.tabInfo["png"].SetContextName (nameof (Data.TabPng));
                Data.TabSha1 = Data.tabInfo["sha1"].SetContextName (nameof (Data.TabSha1));
                Data.TabSha1x = Data.tabInfo["sha1x"].SetContextName (nameof (Data.TabSha1x));
                Data.TabSha256 = Data.tabInfo["sha256"].SetContextName (nameof (Data.TabSha256));
                Data.TabWav = Data.tabInfo["wav"].SetContextName (nameof (Data.TabWav));

                base.Data.FileVisit += Ui.FileProgress;
                base.Data.MessageSend += Ui.ShowLine;
            }

            public void GetFirst()
            {
                TabInfo ti = Data.CurrentTabFormatInfo;
                if (ti != null && ti.Count > 0)
                {
                    ti.Index = 0;
                    Data.RaisePropertyChangedEvent (ti.ContextName);
                }
            }

            public void GetNext()
            {
                TabInfo ti = Data.CurrentTabFormatInfo;
                if (ti != null && ti.Count > 0)
                {
                    ti.Index = ti.Index + 1;
                    Data.RaisePropertyChangedEvent (ti.ContextName);
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
                        Data.RaisePropertyChangedEvent (ti.ContextName);
                    }
                }

                Data.Progress = null;
                if (newTabInfoIx >= 0)
                {
                    Data.CurrentTabNumber = newTabInfoIx+1;
                    Data.tabInfo.Values[newTabInfoIx].Index = newTabInfoFmtIx;
                    Data.RaisePropertyChangedEvent (null);
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

        public int CurrentTabNumber { get; set; }
        public int JobCounter { get; private set; } = 0;  // For unit tests.

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
        public ICommand NavNext { get; private set; }
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
            NavNext = new RelayCommand (() => model.GetNext());
            DoConsoleClear = new RelayCommand (() => model.Ui.SetText (""));
            DoConsoleZoomMinus = new RelayCommand (() => model.Ui.ConsoleZoom (-1));
            DoConsoleZoomPlus = new RelayCommand (() => model.Ui.ConsoleZoom (+1));
        }
    }
}
