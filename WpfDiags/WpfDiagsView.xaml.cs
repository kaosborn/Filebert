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
        private DiagsPresenter viewModel;

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

            sb.Append (Environment.NewLine + "Usage:" + Environment.NewLine);
            sb.Append ($"{exe} [/R] [/s:<scope>] [/h:<hashes>] [/strict] [/flacrip] [/mp3rip] [/webcheck] [<fileOrFolderName>]");
            sb.Append (Environment.NewLine + Environment.NewLine);

            sb.Append ("Where <scope> from");
            foreach (var name in Enum.GetNames (typeof (Granularity)))
            { sb.Append (" "); sb.Append (name); }
            sb.Append (Environment.NewLine);

            sb.Append ("Where <hashes> from");
            foreach (var name in Enum.GetNames (typeof (Hashes)))
                if (name[0] != '_')
                { sb.Append (" "); sb.Append (name); }
            sb.Append (Environment.NewLine);

            sb.Append ("Where <fileOrFolderName> is a file or folder name without wildcards.\n");
            sb.Append (Environment.NewLine);

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
                    viewModel.IsRepairEnabled = true;
                    argOk = true;
                }
                else if (args[ix].StartsWith ("/s:"))
                {
                    argOk = Enum.TryParse<Granularity> (args[ix].Substring (3), true, out Granularity arg);
                    argOk = argOk && Enum.IsDefined (typeof (Granularity), arg);
                    if (argOk)
                        viewModel.Scope = arg;
                }
                if (args[ix].StartsWith ("/h:"))
                {
                    argOk = Enum.TryParse<Hashes> (args[ix].Substring (3), true, out Hashes arg);
                    argOk = argOk && arg == (arg & (Hashes._FlacMatch - 1));
                    if (argOk)
                        viewModel.HashFlags = arg;
                }
                else if (args[ix] == "/strict")
                {
                    viewModel.IsStrict = true;
                    argOk = true;
                }
                else if (args[ix].StartsWith ("/v:"))
                {
                    argOk = Enum.TryParse<Validations> (args[ix].Substring (3), true, out Validations arg);
                    if (argOk)
                        viewModel.ValidationFlags = arg;
                }
                else if (args[ix] == "/flacrip")
                {
                    viewModel.IsFlacRipCheckEnabled = true;
                    argOk = true;
                }
                else if (args[ix] == "/mp3rip")
                {
                    viewModel.IsMp3RipCheckEnabled = true;
                    argOk = true;
                }
                else if (args[ix] == "/webcheck")
                {
                    viewModel.IsEacWebCheckEnabled = true;
                    argOk = true;
                }
                else if (ix == args.Length - 1)
                {
                    var arg = args[ix].Trim (null);
                    argOk = arg.Length > 0 && (arg[0] != '/' || Path.DirectorySeparatorChar == '/');
                    if (argOk)
                        viewModel.Root = arg;
                }

                if (! argOk)
                {
                    consoleBox.Text = $"Invalid argument: {args[ix]}" + Environment.NewLine;
                    ShowUsage();
                    return (int) Severity.Error;
                }
            }

            return (int) Severity.NoIssue;
        }

        public void Window_Loaded (object sender, RoutedEventArgs ea)
        {
            Title = $"{ProductText} v{VersionText}";

            viewModel = new DiagsPresenter.Model (this).ViewModel;
            viewModel.Scope = Granularity.Lucid;
            viewModel.HashFlags = Hashes.Intrinsic;
            viewModel.ValidationFlags = Validations.Exists|Validations.MD5|Validations.SHA1|Validations.SHA256;
            ParseArgs();
            DataContext = viewModel;
        }
    }
}
