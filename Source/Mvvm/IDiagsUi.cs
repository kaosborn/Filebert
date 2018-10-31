using System.Collections.Generic;
using KaosFormat;
using KaosIssue;

namespace AppViewModel
{
    public interface IDiagsUi
    {
        string BrowseFile();
        void FileProgress (string dirName, string fileName);
        void ShowLine (string message, Severity severity);
        void ShowSummary (IList<string> rollups);
        void SetText (string message);
        void ConsoleZoom (int delta);
        IList<string> GetHeadings();
    }
}
