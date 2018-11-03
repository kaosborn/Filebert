using System.Collections.Generic;
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

    public interface IFileDragDropTarget
    {
        void OnFileDrop (string[] paths);
    }
}
