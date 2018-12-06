using System.Collections.Generic;
using KaosIssue;

namespace AppViewModel
{
    public interface IDiagsUi
    {
        string BrowseFile();
        void ShowLine (string message, Severity severity);
        void SetConsoleText (string message);
        IList<string> GetHeadings();
    }

    public interface IFileDragDropTarget
    {
        void OnFileDrop (string[] paths);
    }
}
