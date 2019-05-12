//
// Product: Filebert
// File:    ConDiagsView.cs
//
// Copyright © 2015-2019 github.com/kaosborn
// MIT License - Use and redistribute freely
//

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using KaosIssue;
using KaosDiags;
using AppController;

namespace AppView
{
    public class ConDiagsViewFactory : IConDiagsViewFactory
    {
        public void Create (ConDiagsController controller, Diags diags)
        {
            var view = new ConDiagsView (controller, diags);
        }
    }

    class ConDiagsView
    {
        private readonly ConDiagsController controller;
        private readonly Diags diags;
        private bool isProgressDirty=false;
        public string ProgressEraser => "\r              \r";

        static int Main (string[] args)
        {
            var consoleWriter = new ConsoleTraceListener (false);
            Trace.Listeners.Add (consoleWriter);
            Trace.AutoFlush = true;

            var controller = new ConDiagsController (args, new ConDiagsViewFactory());
            return controller.Run();
        }

        public ConDiagsView (ConDiagsController controller, Diags diags)
        {
            this.controller = controller;
            this.diags = diags;
            this.diags.QuestionAsk = Question;
            this.diags.MessageSend += Logger;
            this.diags.PropertyChanged += NotifyPropertyChanged;
        }

        private void Logger (string message, Severity severity)
        {
            if (isProgressDirty)
            {
                Console.Error.Write (ProgressEraser);
                isProgressDirty = false;
            }

            if (! diags.IsFileShown && diags.CurrentFile != null)
            {
               diags.IsFileShown = true;

                if (diags.ConsoleLinesReported > 0)
                    if (diags.Scope == Granularity.Detail)
                    { Trace.WriteLine (String.Empty); Trace.WriteLine (Diags.MinorSeparator); }
                    else if (! diags.IsDigestForm)
                        Trace.WriteLine (String.Empty);

                if (! diags.IsDirShown)
                {
                    diags.IsDirShown = true;

                    if (diags.IsDigestForm)
                    {
                        if (diags.ConsoleLinesReported > 0)
                            Trace.WriteLine (String.Empty);
                        Trace.Write ("; ");
                    }

                    Trace.Write (diags.CurrentDirectory);
                    if (diags.CurrentDirectory[diags.CurrentDirectory.Length-1] != Path.DirectorySeparatorChar)
                        Trace.Write (Path.DirectorySeparatorChar);
                    Trace.WriteLine (String.Empty);
                }

                if (! diags.IsDigestForm)
                    Trace.WriteLine (diags.CurrentFile);
            }

            if (severity != Severity.NoIssue)
            {
                if (diags.IsDigestForm)
                    Trace.Write ("; ");
                if (severity <= Severity.Advisory)
                    Trace.Write ("  ");
                else
                    Trace.Write (severity <= Severity.Warning ? "- Warning: " : "* Error: ");
            }
            Trace.WriteLine (message);

            if (controller.NotifyEvery != 0)
                if (diags.CurrentFile != null && diags.ProgressCounter % controller.NotifyEvery == 0)
                    WriteProgress();
        }

        private void NotifyPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof (diags.ProgressCounter))
                if (controller.NotifyEvery != 0 && diags.ProgressCounter != null)
                    if (diags.ProgressCounter.Value % controller.NotifyEvery == 0)
                        WriteProgress();
        }

        private void WriteProgress()
        {
            Console.Error.Write ("Checked ");
            Console.Error.Write (diags.ProgressCounter);
            Console.Error.Write ('\r');
            isProgressDirty = true;
        }

        public bool? Question (string prompt)
        {
            for (;;)
            {
                if (prompt != null)
                    Trace.Write (prompt);

                string response = Console.ReadLine().ToLower();

                if (response == "n" || response == "no")
                    return false;
                if (response == "y" || response == "yes")
                    return true;
            }
        }
    }
}
