//
// Product: Filebert
// File:    ConDiagsController.cs
//
// Copyright © 2015-2019 github.com/kaosborn
// MIT License - Use and redistribute freely
//

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using KaosIssue;
using KaosFormat;
using KaosDiags;

namespace AppController
{
    public interface IConDiagsViewFactory
    {
        void Create (ConDiagsController controller, Diags diags);
    }


    public class ConDiagsController
    {
        private readonly IConDiagsViewFactory viewFactory;
        private readonly string[] args;
        private Diags.Model model;
        private bool waitForKeyPress = false;
        private string mirrorName = null;
        public int NotifyEvery { get; private set; } = 1;

        public ConDiagsController (string[] args, IConDiagsViewFactory viewFactory)
        {
            this.args = args;
            this.viewFactory = viewFactory;
        }

        public int Run()
        {
            model = new Diags.Model (null);

            int exitCode = ParseArgs();
            if (exitCode == 0)
            {
                if (model.Data.Scope == Granularity.Detail)
                    NotifyEvery = 0;

                if (mirrorName != null)
                    try
                    {
                        var mirrorWriter = new TextWriterTraceListener (mirrorName);
                        mirrorWriter.WriteLine (String.Empty);
                        mirrorWriter.WriteLine (Diags.MajorSeparator);
                        mirrorWriter.WriteLine (DateTime.Now);
                        Trace.Listeners.Add (mirrorWriter);
                    }
                    catch (Exception)
                    { Console.Error.WriteLine ("Ignoring malformed <mirror>"); }

                if (model.Data.Scope <= Granularity.Verbose)
                {
                    if (model.Data.IsDigestForm)
                        Trace.Write ("; ");
                    Trace.WriteLine ($"{ProductText} v{VersionText}");
                    Trace.WriteLine (String.Empty);
                }

                viewFactory.Create (this, model.Data);
                exitCode = (int) Severity.NoIssue;
                string err = null;
#if ! DEBUG
                try
                {
#endif
                    foreach (FormatBase.Model fmtModel in model.CheckRoot())
                    { }
                    model.ReportSummary ("checked");
                    exitCode = (int) model.Data.Result;
#if ! DEBUG
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException)
                { err = ex.Message; }
#endif

                if (err != null)
                {
                    exitCode = (int) Severity.Fatal;
                    Console.Error.WriteLine ("* Error: " + err);
                }
            }

            if (waitForKeyPress)
            {
                Console.WriteLine();
                Console.Write ("Press the escape key to escape...");
                while (Console.ReadKey().Key != ConsoleKey.Escape)
                { }
            }

            return exitCode;
        }

        private int ParseArgs()
        {
            if (args.Length==0 || args[0]=="/?" || args[0]=="/help" || args[0]=="-?" || args[0]=="-help")
            {
                ShowUsage();
                return (int) Severity.Noise;
            }

            for (int ix = 0; ix < args.Length; ++ix)
            {
                bool argOk = false;

                if (args[ix] == "/R")
                {
                    model.Data.IsRepairEnabled = true;
                    argOk = true;
                }
                else if (args[ix].StartsWith ("/f:"))
                {
                    model.Data.Filter = args[ix].Substring (3);
                    argOk = true;
                }
                else if (args[ix].StartsWith ("/s:"))
                {
                    argOk = Enum.TryParse<Granularity> (args[ix].Substring (3), true, out Granularity arg);
                    argOk = argOk && Enum.IsDefined (typeof (Granularity), arg);
                    if (argOk)
                        model.Data.Scope = arg;
                }
                else if (args[ix].StartsWith ("/h:"))
                {
                    argOk = Enum.TryParse<Hashes> (args[ix].Substring (3), true, out Hashes arg);
                    argOk = argOk && arg == (arg & (Hashes._FlacMatch - 1));
                    if (argOk)
                        model.Data.HashFlags = arg;
                }
                else if (args[ix].StartsWith ("/out:"))
                {
                    var arg = args[ix].Substring (5).Trim(null);
                    argOk = arg.Length > 0;
                    if (argOk)
                        mirrorName = arg;
                }
                else if (args[ix].StartsWith ("/strict"))
                {
                    model.Data.IsStrict = true;
                    argOk = true;
                }
                else if (args[ix].StartsWith ("/v:"))
                {
                    argOk = Enum.TryParse<Validations> (args[ix].Substring (3), true, out Validations arg);
                    if (argOk)
                        model.Data.ValidationFlags = arg;
                }
                else if (args[ix].StartsWith ("/w:"))
                {
                    argOk = Enum.TryParse<IssueTags> (args[ix].Substring (3), true, out IssueTags arg);
                    if (argOk)
                        model.Data.WarnEscalator = arg;
                }
                else if (args[ix].StartsWith ("/e:"))
                {
                    argOk = Enum.TryParse<IssueTags> (args[ix].Substring (3), true, out IssueTags arg);
                    if (argOk)
                        model.Data.ErrEscalator = arg;
                }
                else if (args[ix] == "/flacrip")
                {
                    model.Data.IsFlacRipCheckEnabled = true;
                    argOk = true;
                }
                else if (args[ix] == "/mp3rip")
                {
                    model.Data.IsMp3RipCheckEnabled = true;
                    argOk = true;
                }
                else if (args[ix] == "/tags")
                    argOk = model.Data.IsFlacTagsCheckEnabled = true;
                else if (args[ix] == "/webcheck")
                {
                    model.Data.IsEacWebCheckEnabled = true;
                    argOk = true;
                }
                else if (args[ix].StartsWith ("/p:"))
                {
                    argOk = int.TryParse (args[ix].Substring (3), out int arg);
                    if (argOk)
                        NotifyEvery = arg;
                }
                else if (args[ix].StartsWith ("/x:"))
                {
                    var arg = args[ix].Substring (3);
                    argOk = ! String.IsNullOrWhiteSpace (arg);
                    if (argOk)
                        model.Data.Exclusion = arg;
                }
                else if (args[ix] == "/k")
                {
                    waitForKeyPress = true;
                    argOk = true;
                }
                else if (ix == args.Length - 1)
                {
                    var arg = args[ix].Trim(null);
                    argOk = arg.Length > 0 && (arg[0] != '/' || Path.DirectorySeparatorChar == '/');
                    if (argOk)
                        model.Data.Root = arg;
                }

                if (! argOk)
                {
                    Console.Error.WriteLine ("Invalid argument: " + args[ix]);
                    return (int) Severity.Fatal;
                }
            }

            if (String.IsNullOrEmpty (model.Data.Root))
            {
                Console.Error.WriteLine ("Missing <fileOrFolderName>");
                return (int) Severity.Fatal;
            }

            return (int) Severity.NoIssue;
        }

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

        private void ShowUsage()
        {
            string exe = Process.GetCurrentProcess().ProcessName;

            Console.WriteLine ($"{ProductText} v{VersionText}");
            Console.WriteLine ();
            Console.WriteLine ("Usage:");
            Console.WriteLine ($"{exe} [/s:<scope>] [/h:<hashes>] [/v:<validations>] [/R] [/flacrip] [/mp3rip] [/tags] [/webcheck] [/strict] [/f:<wildcard>] [/x:<exclusion>] [/out:<mirror>] [/p:<counter>] [/k] <fileOrFolder>");

            Console.WriteLine();
            Console.Write ("where <scope> is one from");
            foreach (var name in Enum.GetNames (typeof (Granularity)))
                Console.Write (" " + name);
            Console.WriteLine();

            Console.Write ("where <hashes> is list from");
            foreach (var name in Enum.GetNames (typeof (Hashes)))
                if (name[0] != '_')
                    Console.Write (" " + name);
            Console.WriteLine();

            Console.Write ("where <validations> is list from");
            foreach (var name in Enum.GetNames (typeof (Validations)))
                Console.Write (" " + name);
            Console.WriteLine ();

            Console.WriteLine ("where <fileOrFolder> is a file or folder name without wildcards.");

            Console.WriteLine ();
            Console.WriteLine ("Examples:");

            Console.WriteLine ();
            Console.WriteLine ("Use /f:* to diagnose files with any extension.");
            Console.WriteLine ("Use /f:*.log to check only files with the .log extension.");

            Console.WriteLine ();
            Console.WriteLine ("Use /h:0 to disable all hash calculations including CRCs.");
            Console.WriteLine ("Use /h:Intrinsic,FileSHA1 to generate file SHA1 and intrinsic hashes.");

            Console.WriteLine ();
            Console.WriteLine ("Use /k to wait for keypress before exiting.");

            Console.WriteLine ();
            Console.WriteLine ("Use /out:results.txt to mirror output to results.txt.");

            Console.WriteLine ();
            Console.WriteLine ("Use /p:0 to suppress the progress counter.");

            Console.WriteLine ();
            Console.WriteLine ("Use /s:detail to display maximum diagnostics.");

            Console.WriteLine ();
            Console.WriteLine ("Use /v:0 to parse digest files and perform no validations.");
            Console.WriteLine ("Use /v:exists,md5,sha1,sha256 to validate all playlists and digests.");

            Console.WriteLine ();
            Console.WriteLine ("Use /x:@ to ignore all paths that include the at sign.");

            Console.WriteLine ();
            Console.WriteLine ("Description:");
            Console.WriteLine ();

            foreach (var line in helpText)
                Console.WriteLine (line);

            Console.WriteLine ();
            Console.WriteLine ("The following file extensions are known:");
            Console.WriteLine (Diags.FormatListText);
        }

        private static readonly string[] helpText =
{
"This program performs diagnostics on the supplied file or on all eligible",
"files in the supplied directory and its subdirectories. Diagnostics may be",
"extensive or just a simple magic number test. The most extensive checks",
"are performed on .mp3 and .flac files which include CRC verification.",
"",
"Some issues may be repaired. No repairs will be made unless the /R switch is",
"given *and* each repair is confirmed. These are the repairable issues:",
"1. A phantom .mp3 ID3v1 tag.",
"2. Old EAC bug that sometimes created an .mp3 with a bad ID3v2 tag size.",
"3. End-of-file watermarks.",
"4. Bad file references in .cue files.",
"5. Incorrect extensions."
};
    }
}
