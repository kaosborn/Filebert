using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using KaosFormat;
using KaosIssue;
using AppViewModel;

namespace TestDiags
{
    [TestClass]
    public class UnitMvvm
    {
        [TestMethod]
        public void MvvmM3u()
        {
            DiagsPresenter mv = new MockDiagsView().ViewModel;
            int tries = 2400;
            int jobCounter;

            mv.Root = @"Targets\Hashes\Bad02.m3u";
            mv.DoCheck.Execute (null);
            for (jobCounter = mv.JobCounter; jobCounter == mv.JobCounter && tries >= 0; --tries)
                Thread.Sleep (50);

            var m3u = (M3uFormat) mv.TabM3u.Current;
            Assert.IsNotNull (m3u);
            Assert.AreEqual (2, m3u.Files.FoundCount);
            Assert.AreEqual (3, m3u.Files.Items.Count);

            mv.Root = @"Targets\Hashes\OK02.m3u";
            mv.DoCheck.Execute (null);
            for (jobCounter = mv.JobCounter; jobCounter == mv.JobCounter && tries >= 0; --tries)
                Thread.Sleep (60);

            m3u = (M3uFormat) mv.TabM3u.Current;
            Assert.AreEqual ("OK02.m3u", m3u.Name, tries.ToString());
            Assert.AreEqual (3, m3u.Files.Items.Count);
            Assert.AreEqual (3, m3u.Files.FoundCount);

            mv.CurrentTabNumber = 1;
            mv.NavFirst.Execute (null);
            Assert.AreEqual ("Bad02.m3u", mv.TabM3u.Current.Name);
        }

        [TestMethod]
        public void MvvmMp3()
        {
            DiagsPresenter mv = new MockDiagsView().ViewModel;
            int tries = 1200;

            mv.Root = @"Targets\Singles\02-WalkedOn.mp3";
            mv.DoCheck.Execute (null);
            for (int jobCounter = mv.JobCounter; jobCounter == mv.JobCounter && tries >= 0; --tries)
                Thread.Sleep (50);

            var mp3 = (Mp3Format) mv.TabMp3.Current;
            Assert.IsFalse (mp3.HasId3v1Phantom);
            Assert.IsTrue (mp3.IsBadData);
            Assert.AreEqual (Severity.Error, mp3.Issues.MaxSeverity);
        }
    }
}
