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
        private bool isImmediate=false;

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
            sb.Append ($"{exe} [/s:<scope>] [/h:<hashes>] [/v:<validations>] [/R] [/flacrip] [/mp3rip] [/webcheck] [/strict] [/f:<wildcard>] [/go] [<fileOrFolder>]");
            sb.Append (Environment.NewLine + Environment.NewLine);

            sb.Append ("where <scope> is one from");
            foreach (var name in Enum.GetNames (typeof (Granularity)))
            { sb.Append (" "); sb.Append (name); }
            sb.Append (Environment.NewLine);

            sb.Append ("where <hashes> is list from");
            foreach (var name in Enum.GetNames (typeof (Hashes)))
                if (name[0] != '_')
                { sb.Append (" "); sb.Append (name); }
            sb.Append (Environment.NewLine);

            sb.Append ("where <validations> is list from");
            foreach (var name in Enum.GetNames (typeof (Validations)))
            { sb.Append (" "); sb.Append (name); }
            sb.Append (Environment.NewLine);

            sb.Append ("where <fileOrFolder> is a file or folder name without wildcards.");
            sb.Append (Environment.NewLine);

            sb.Append (Environment.NewLine);
            sb.Append ("Examples:");
            sb.Append (Environment.NewLine);
            sb.Append (Environment.NewLine);

            sb.Append ("Use /h:0 to disable all hash calculations including CRCs.");
            sb.Append (Environment.NewLine);
            sb.Append ("Use /h:Intrinsic,FileSHA1 to generate file SHA1 and intrinsic hashes.");
            sb.Append (Environment.NewLine);

            sb.Append (Environment.NewLine);
            sb.Append ("Use /f:* to diagnose files with any extension.");
            sb.Append (Environment.NewLine);
            sb.Append ("Use /f:*.log to check only files with the .log extension.");
            sb.Append (Environment.NewLine);

            sb.Append (Environment.NewLine);
            sb.Append ("Use /v:0 to parse digest files without performing hash validations.");
            sb.Append (Environment.NewLine);
            sb.Append ("Use /v:exists,md5,sha1,sha256 to validate all playlists and digests.");
            sb.Append (Environment.NewLine);

            sb.Append (Environment.NewLine);
            sb.Append ("Use /go T:\\MyFile.ogg to immediately diagnose the supplied file.");
            sb.Append (Environment.NewLine);

            sb.Append (Environment.NewLine);
            sb.Append ("Use /? to show this help.");
            sb.Append (Environment.NewLine);
            sb.Append (Environment.NewLine);

            consoleBox.Text += sb.ToString();
        }

        private int ParseArgs()
        {
            for (int ix = 0; ix < args.Length; ++ix)
            {
                bool argOk = false;

                if (args[ix] == "/?")
                {
                    ShowUsage();
                    argOk = true;
                }
                else if (args[ix] == "/R")
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
                else if (args[ix] == "/go")
                    argOk = isImmediate = true;
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

            var presenterModel = new DiagsPresenter.Model (this);
            viewModel = presenterModel.ViewModel;
            ParseArgs();

            DataContext = viewModel;
            if (isImmediate)
                presenterModel.Parse();
        }
    }
}
