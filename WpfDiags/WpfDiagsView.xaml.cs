using System;
using System.IO;
using System.Reflection;
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

        public static string ProductText
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                object[] attributes = assembly.GetCustomAttributes (typeof (AssemblyProductAttribute), false);
                return attributes.Length == 0 ? String.Empty : ((AssemblyProductAttribute) attributes[0]).Product;
            }
        }

        public static string VersionText
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string result = assembly.GetName().Version.ToString();
                if (result.Length > 3 && result.EndsWith (".0"))
                    result = result.Substring (0, result.Length-2);
                return result;
            }
        }

        public WpfDiagsView (string[] args)
        {
            this.args = args;
            InitializeComponent();
        }

        private void ShowUsage()
        {
            var sb = new StringBuilder();
            string exe = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            sb.Append ($"\nUsage:\n{exe} [/R] [/g:<granularity>] [/h:<hashes>] [/fussy] [/ripcheck] [/webcheck] [<fileOrFolderName>]\n\n");

            sb.Append ("Where <granularity> from");
            foreach (var name in Enum.GetNames (typeof (Granularity)))
            { sb.Append (" "); sb.Append (name); }
            sb.Append ("\n");

            sb.Append ("Where <hashes> from");
            foreach (var name in Enum.GetNames (typeof (Hashes)))
                if (name[0] != '_')
                { sb.Append (" "); sb.Append (name); }
            sb.Append ("\n");

            sb.Append ("Where <fileOrFolderName> is a file or directory name without wildcards.\n");
            sb.Append ("\n");

            consoleBox.Text += sb.ToString();
        }

        private int ParseArgs()
        {
            if (args.Length > 0 && (args[0]=="/?" || args[0]=="/help" || args[0]=="-?" || args[0]=="-help"))
            {
                ShowUsage();
                return (int) Severity.Noise;
            }

            for (int ix = 0; ix < args.Length; ++ix)
            {
                bool argOk = false;

                if (args[ix] == "/R")
                {
                    viewModel.Data.IsRepairEnabled = true;
                    argOk = true;
                }
                else if (args[ix].StartsWith ("/g:"))
                {
                    argOk = Enum.TryParse<Granularity> (args[ix].Substring (3), true, out Granularity arg);
                    argOk = argOk && Enum.IsDefined (typeof (Granularity), arg);
                    if (argOk)
                        viewModel.Data.Scope = arg;
                }
                if (args[ix].StartsWith ("/h:"))
                {
                    argOk = Enum.TryParse<Hashes> (args[ix].Substring (3), true, out Hashes arg);
                    argOk = argOk && arg == (arg & (Hashes._LogCheck - 1));
                    if (argOk)
                        viewModel.Data.HashFlags = arg;
                }
                else if (args[ix] == "/fussy")
                {
                    viewModel.Data.IsFussy = true;
                    argOk = true;
                }
                else if (args[ix].StartsWith ("/v:"))
                {
                    argOk = Enum.TryParse<Validations> (args[ix].Substring (3), true, out Validations arg);
                    if (argOk)
                        viewModel.Data.ValidationFlags = arg;
                }
                else if (args[ix] == "/ripcheck")
                {
                    viewModel.Data.IsRipCheckEnabled = true;
                    argOk = true;
                }
                else if (args[ix] == "/webcheck")
                {
                    viewModel.Data.IsWebCheckEnabled = true;
                    argOk = true;
                }
                else if (ix == args.Length - 1)
                {
                    var arg = args[ix].Trim (null);
                    argOk = arg.Length > 0 && (arg[0] != '/' || Path.DirectorySeparatorChar == '/');
                    if (argOk)
                        viewModel.Data.Root = arg;
                }

                if (! argOk)
                {
                    consoleBox.Text = $"Invalid argument: {args[ix]}\n";
                    ShowUsage();
                    return (int) Severity.Error;
                }
            }

            return (int) Severity.NoIssue;
        }

        public void Window_Loaded (object sender, RoutedEventArgs ea)
        {
            Title = $"{ProductText} v{VersionText}";
            viewModel = new DiagsPresenter.Model (this);
            viewModel.Data.Scope = Granularity.Advisory;
            viewModel.Data.HashFlags = Hashes.Intrinsic;
            viewModel.Data.ValidationFlags = Validations.Exists|Validations.MD5|Validations.SHA1|Validations.SHA256;
            ParseArgs();
            DataContext = viewModel.Data;
        }
    }
}
