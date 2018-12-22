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
        public DiagsPresenter ViewModel { get; private set; }

        public MockDiagsView()
        {
            ViewModel = new DiagsPresenter.Model (this).ViewModel;
            ViewModel.Scope = Granularity.Detail;
            ViewModel.HashFlags = Hashes.Intrinsic;
        }

        public string BrowseFile()
         => throw new NotImplementedException();

        public void SetConsoleText (string message)
        {
            console.Clear();
            console.AppendLine (message);
        }

        public void ShowLine (string message, Severity severity)
         => console.AppendLine (message);

        public IList<string> GetHeadings()
         => new List<string> { "Console", ".flac", ".m3u", ".mp3", ".ogg" };
    }
}
