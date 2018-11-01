//
// Product: Filebert
// File:    ConDiagsView.cs
//
// Copyright © 2015-2018 github.com/kaosborn
// MIT License - Use and redistribute freely
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using KaosIssue;
using KaosFormat;
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
        private ConDiagsController controller;
        private Diags diags;

        private bool isProgressDirty = false;
        private int totalLinesReported = 0;
        private string curDir = null, curFile = null;
        private bool dirShown = false, fileShown = false;

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
            this.diags.ReportClose += Summarize;
            this.diags.FileVisit += FileProgress;
        }

        private void Logger (string message, Severity severity)
        {
            if (isProgressDirty)
            {
                Console.Error.Write (ProgressEraser);
                isProgressDirty = false;
            }

            if (! fileShown)
            {
                fileShown = true;

                if (totalLinesReported != 0)
                    if (diags.Scope == Granularity.Detail)
                    { Trace.WriteLine (String.Empty); Trace.WriteLine (Diags.MinorSeparator); }
                    else if (! diags.IsDigestForm)
                        Trace.WriteLine (String.Empty);

                if (! dirShown)
                {
                    dirShown = true;

                    if (diags.IsDigestForm)
                    {
                        if (totalLinesReported != 0)
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

            if (controller.NotifyEvery != 0 && diags.TotalFiles % controller.NotifyEvery == 0)
                WriteProgress();

            ++totalLinesReported;
        }

        private void Summarize()
        {
            if (controller.NotifyEvery != 0)
                Console.Error.Write (ProgressEraser);

            if (diags.TotalFiles > 1 || diags.Scope > Granularity.Detail)
            {
                if (totalLinesReported > 0)
                {
                    Trace.WriteLine (String.Empty);
                    if (diags.IsDigestForm)
                        Trace.Write ("; ");
                    Trace.WriteLine (Diags.MajorSeparator);
                }

                var rollups = diags.GetReportRollups ("checked");
                foreach (var lx in rollups)
                {
                    if (diags.IsDigestForm)
                        Trace.Write ("; ");
                    Trace.WriteLine (lx);
                }
            }
        }

        private void FileProgress (string dirName, string fileName)
        {
            if (curDir != dirName)
            {
                curDir = dirName;
                dirShown = false;
                curFile = fileName;
                fileShown = false;
            } else if (curFile != fileName)
            {
                curFile = fileName;
                fileShown = false;
            }
            else
                return;

            if (controller.NotifyEvery != 0 && diags.TotalFiles % controller.NotifyEvery == 0)
                WriteProgress();
        }

        private void WriteProgress()
        {
            Console.Error.Write ("Checked ");
            Console.Error.Write (diags.TotalFiles);
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
