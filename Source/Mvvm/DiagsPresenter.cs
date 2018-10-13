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
        public int Count => parsings.Count;
        public FormatBase Current => parsings[index];

        private int index = -1;
        public int Index
        {
            get => index;
            set { if (parsings.Count > 0 && value >= 0 && value < parsings.Count) index = value; }
        }

        public TabInfo (int tabPosition)
        { this.TabPosition = tabPosition; this.parsings = new List<FormatBase>(); }

        public void Add (FormatBase fmt)
         => parsings.Add (fmt);
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

                base.Data.FileVisit += Ui.FileProgress;
                base.Data.MessageSend += Ui.ShowLine;
            }

            public void GetFirst()
            {
                TabInfo ti = Data.CurrentTabFormatInfo;
                if (ti != null && ti.Count > 0)
                {
                    ti.Index = 0;
                    RefreshTab (ti.Current);
                }
            }

            public void GetNext()
            {
                TabInfo ti = Data.CurrentTabFormatInfo;
                if (ti != null && ti.Count > 0)
                {
                    ti.Index = ti.Index + 1;
                    RefreshTab (ti.Current);
                }
            }

            public void RefreshTab (FormatBase fmt)
            {
                if (fmt is FlacFormat flac)
                    Data.Flac = flac;
                else if (fmt is LogEacFormat logEac)
                    Data.LogEac = logEac;
                else if (fmt is M3uFormat m3u)
                    Data.M3u = m3u;
                else if (fmt is Mp3Format mp3)
                    Data.Mp3 = mp3;
                else if (fmt is OggFormat ogg)
                    Data.Ogg = ogg;
                else if (fmt is Sha1Format sha1)
                    Data.Sha1 = sha1;
                else if (fmt is Md5Format md5)
                    Data.Md5 = md5;

                Data.RaisePropertyChangedEvent (null);
            }

            public void Parse()
            {
                var bg = new BackgroundWorker();
                bg.DoWork += Job;
                bg.RunWorkerCompleted += JobCompleted;
                bg.RunWorkerAsync();
            }

            int newTabNumber;
            void Job (object sender, DoWorkEventArgs jobArgs)
            {
                jobArgs.Result = (string) null;
                newTabNumber = -1;

                try
                {
                    foreach (FormatBase.Model parsing in CheckRoot())
                        if (parsing != null)
                            if (Data.tabInfo.TryGetValue (parsing.Data.LongName, out TabInfo tInfo))
                            {
                                if (newTabNumber < 0)
                                    newTabNumber = tInfo.TabPosition;
                                tInfo.Add (parsing.Data);
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
                        RefreshTab (Data.tabInfo.Values[ix].Current);
                    }
                }

                if (Data.CurrentTabNumber < 1)
                {
                    Data.CurrentTabNumber = newTabNumber;
                    Data.RaisePropertyChangedEvent (null);
                }

                ++Data.JobCounter;
            }
        }


        private SortedList<string,TabInfo> tabInfo = new SortedList<string,TabInfo>();

        public FlacFormat Flac { get; private set; }
        public LogEacFormat LogEac { get; private set; }
        public M3uFormat M3u { get; private set; }
        public Md5Format Md5 { get; private set; }
        public Mp3Format Mp3 { get; private set; }
        public OggFormat Ogg { get; private set; }
        public Sha1Format Sha1 { get; private set; }

        public int CurrentTabNumber { get; set; }
        public int JobCounter { get; private set; } = 0;  // For unit tests.

        private TabInfo CurrentTabFormatInfo
         => tabInfo.Values.FirstOrDefault (v => v.TabPosition == CurrentTabNumber);

        private void AddTabInfo (string formatName, int tabPosition)
         => tabInfo.Add (formatName, new TabInfo (tabPosition));

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
            Response = Interaction.RepairLater;

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
