using System;
using System.Text;
using System.Windows;
using KaosIssue;
using KaosFormat;
using AppViewModel;

namespace AppView
{
    public partial class WpfDiagsView : Window, IDiagsUi
    {
        private readonly string[] args;
        private DiagsPresenter.Model viewModel;

        public WpfDiagsView (string[] args)
        {
            this.args = args;
            InitializeComponent();
        }

        private void ShowUsage()
        {
            var sb = new StringBuilder();
            string exe = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            sb.Append ($"\nUsage:\n  {exe} [/g:granularity] [/h:hashes] [/fussy] [/R] [<fileOrFolderName>]\n");

            sb.Append ("\nWhere <hashes> from");
            foreach (var name in Enum.GetNames (typeof (Hashes)))
                if (name[0] != '_')
                { sb.Append (" "); sb.Append (name); }
            sb.Append ("\n");

            sb.Append ("\nWhere <granularity> from");
            foreach (var name in Enum.GetNames (typeof (Granularity)))
                { sb.Append (" "); sb.Append (name); }
            sb.Append ("\n");

            sb.Append ("\nWhere <fileOrFolderName> is a file or directory name without wildcards.\n");
            sb.Append ("\n");

            consoleBox.Text += sb.ToString();
        }

        private int ParseArgs()
        {
            if (args.Length > 0 && (args[0]=="/?" || args[0]=="/help" || args[0]=="-help"))
            {
                ShowUsage();
                return 1;
            }

            for (int ix = 0; ix < args.Length; ++ix)
            {
                bool argOk = false;

                if (args[ix].StartsWith ("/g:"))
                {
                    var arg = Granularity.Detail;
                    argOk = Enum.TryParse<Granularity> (args[ix].Substring (3), true, out arg);
                    argOk = argOk && Enum.IsDefined (typeof (Granularity), arg);
                    if (argOk)
                        viewModel.Data.Scope = arg;
                }
                if (args[ix].StartsWith ("/h:"))
                {
                    var arg = Hashes.None;
                    argOk = Enum.TryParse<Hashes> (args[ix].Substring (3), true, out arg);
                    argOk = argOk && arg == (arg & (Hashes._LogCheck - 1));
                    if (argOk)
                        viewModel.Data.HashFlags = arg;
                }
                else if (args[ix] == @"/fussy")
                {
                    viewModel.Data.IsFussy = true;
                    argOk = true;
                }
                else if (args[ix] == @"/R")
                {
                    viewModel.Data.IsRepairEnabled = true;
                    argOk = true;
                }
                else if (! args[ix].StartsWith ("/") && ix == args.Length-1)
                {
                    viewModel.Data.Root = args[ix];
                    argOk = true;
                }

                if (! argOk)
                {
                    consoleBox.Text = "Invalid argument: " + args[ix] + "\n";
                    ShowUsage();
                    return 1;
                }
            }
            return 0;
        }

        public void Window_Loaded (object sender, RoutedEventArgs ea)
        {
            viewModel = new DiagsPresenter.Model (this);
            viewModel.Data.Scope = Granularity.Detail;
            viewModel.Data.HashFlags = Hashes.Intrinsic;

            ParseArgs();
            DataContext = viewModel.Data;
        }
    }
}
