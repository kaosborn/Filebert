using System;
using System.Collections.Generic;
using System.Windows.Controls;
using KaosFormat;
using KaosIssue;

namespace AppView
{
    // Implement IDiagsUi here
    public partial class WpfDiagsView
    {
        private int totalLinesReported = 0;
        private string shownDir = null, shownFile = null;
        private bool isDirShown = false, isFileShown = false;

        public string BrowseFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog() { Filter="Media files (*.*)|*.*" };
            dlg.ShowDialog();
            return dlg.FileName;
        }

        public IList<string> GetHeadings()
        {
            var result = new List<string>();
            var items = (ItemCollection) ((TabControl) infoTabs).Items;
            foreach (TabItem item in items)
                result.Add ((string) item.Header);
            return result;
        }

        public void FileProgress (string dirName, string fileName)
        {
            if (shownDir != dirName)
            {
                shownDir = dirName;
                isDirShown = false;
                shownFile = fileName;
                isFileShown = false;
            }
            else if (shownFile != fileName)
            {
                shownFile = fileName;
                isFileShown = false;
            }
        }

        public void SetText (string message)
        {
            consoleBox.Text = message;
            shownDir = null; shownFile = null;
            isDirShown = false; isFileShown = false;
            totalLinesReported = 0;
        }

        public void ShowLine (string message, Severity severity)
        {
            if (! Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke
                (
                    new Action<string,Severity> ((m, s) => ShowLine (m, s)),
                    new object[] { message, severity }
                );
                return;
            }

            if (! isFileShown && viewModel.Data.CurrentFile != null)
            {
                isFileShown = true;

                if (totalLinesReported != 0)
                    if (viewModel.Data.Scope == Granularity.Detail)
                        consoleBox.AppendText (Environment.NewLine + Environment.NewLine + KaosDiags.Diags.MinorSeparator + Environment.NewLine);
                    else if (! viewModel.Data.IsDigestForm)
                        consoleBox.AppendText (Environment.NewLine);

                if (! isDirShown)
                {
                    isDirShown = true;

                    if (viewModel.Data.IsDigestForm)
                    {
                        if (totalLinesReported != 0)
                            consoleBox.AppendText (Environment.NewLine);
                        consoleBox.AppendText ("; ");
                    }
                    consoleBox.AppendText (viewModel.Data.CurrentDirectory);
                    if (viewModel.Data.CurrentDirectory[viewModel.Data.CurrentDirectory.Length-1] != System.IO.Path.DirectorySeparatorChar)
                        consoleBox.AppendText (System.IO.Path.DirectorySeparatorChar.ToString());
                    consoleBox.AppendText (Environment.NewLine);
                }

                if (! viewModel.Data.IsDigestForm)
                {
                    consoleBox.AppendText (viewModel.Data.CurrentFile);
                    consoleBox.AppendText (Environment.NewLine);
                }
            }

            if (severity != Severity.NoIssue)
            {
                if (viewModel.Data.IsDigestForm)
                    consoleBox.AppendText ("; ");
                if (severity <= Severity.Advisory)
                    consoleBox.AppendText ("  ");
                else
                    consoleBox.AppendText (severity <= Severity.Warning ? "- Warning: " : "* Error: ");
            }
            consoleBox.AppendText (message);
            consoleBox.AppendText (Environment.NewLine);
            ++totalLinesReported;
        }

        public void ShowSummary (IList<string> rollups)
        {
            if (viewModel.Data.TotalFiles > 1)
            {
                if (totalLinesReported > 0)
                {
                    consoleBox.AppendText (Environment.NewLine);
                    if (viewModel.Data.IsDigestForm)
                        consoleBox.AppendText ("; ");
                    consoleBox.AppendText (KaosDiags.Diags.MajorSeparator);
                    consoleBox.AppendText (Environment.NewLine);
                }

                foreach (var lx in rollups)
                {
                    if (viewModel.Data.IsDigestForm)
                        consoleBox.AppendText ("; ");
                    consoleBox.AppendText (lx);
                    consoleBox.AppendText (Environment.NewLine);
                }
                consoleBox.AppendText (Environment.NewLine);
            }

            viewModel.ResetTotals();
        }
    }
}