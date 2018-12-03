using System;
using System.Collections.Generic;
using System.Windows.Controls;
using KaosIssue;

namespace AppView
{
    // Implement IDiagsUi here
    public partial class WpfDiagsView
    {
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

        public void SetText (string multiline)
         => consoleBox.Text = multiline;

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

            if (! viewModel.Data.IsFileShown && viewModel.Data.CurrentFile != null)
            {
                viewModel.Data.IsFileShown = true;

                if (viewModel.Data.ConsoleLinesReported > 0)
                    if (viewModel.Data.Scope == Granularity.Detail)
                        consoleBox.AppendText (Environment.NewLine + KaosDiags.Diags.MinorSeparator + Environment.NewLine);
                    else if (! viewModel.Data.IsDigestForm)
                        consoleBox.AppendText (Environment.NewLine);

                if (! viewModel.Data.IsDirShown)
                {
                    viewModel.Data.IsDirShown = true;

                    if (viewModel.Data.IsDigestForm)
                    {
                        if (viewModel.Data.ConsoleLinesReported > 0)
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
        }
    }
}
