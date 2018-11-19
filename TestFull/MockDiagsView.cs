using System;
using System.Collections.Generic;
using System.Text;
using KaosFormat;
using KaosIssue;
using AppViewModel;

namespace TestDiags
{
    public class MockDiagsView : IDiagsUi
    {
        private StringBuilder console = new StringBuilder();
        private DiagsPresenter.Model model;
        public DiagsPresenter ViewModel => model.Data;

        public MockDiagsView()
        {
            model = new DiagsPresenter.Model (this);
            model.Data.Scope = Granularity.Detail;
            model.Data.HashFlags = Hashes.Intrinsic;
        }

        public string BrowseFile()
         => throw new NotImplementedException();

        public void FileProgress (string dirName, string fileName)
        {
        }

        public void SetText (string message)
        {
            console.Clear();
            console.AppendLine (message);
        }

        public void ShowLine (string message, Severity severity)
         => console.AppendLine (message);

        public IList<string> GetHeadings()
         => new List<string> { "Console", ".flac", ".m3u", ".mp3", ".ogg" };

        public void ShowSummary (IList<string> rollups)
        { }
    }
}
