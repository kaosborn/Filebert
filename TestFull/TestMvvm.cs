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
        public void UnitMvvm_M3u()
        {
            DiagsPresenter vm = new MockDiagsView().ViewModel;
            int tries = 2400;
            int jobCounter;

            vm.Root = @"Targets\Hashes\Bad02.m3u";
            vm.DoCheck.Execute (null);
            for (jobCounter = vm.JobCounter; jobCounter == vm.JobCounter && tries >= 0; --tries)
                Thread.Sleep (50);

            var m3u = (M3uFormat) vm.TabM3u.Current;
            Assert.IsNotNull (m3u);
            Assert.AreEqual (2, m3u.Files.FoundCount);
            Assert.AreEqual (3, m3u.Files.Items.Count);

            vm.Root = @"Targets\Hashes\OK02.m3u";
            vm.DoCheck.Execute (null);
            for (jobCounter = vm.JobCounter; jobCounter == vm.JobCounter && tries >= 0; --tries)
                Thread.Sleep (50);

            m3u = (M3uFormat) vm.TabM3u.Current;
            Assert.AreEqual ("OK02.m3u", m3u.Name, tries.ToString());
            Assert.AreEqual (3, m3u.Files.Items.Count);
            Assert.AreEqual (3, m3u.Files.FoundCount);

            vm.CurrentTabNumber = vm.TabM3u.TabPosition;
            vm.NavFirst.Execute (null);
            Assert.AreEqual ("Bad02.m3u", vm.TabM3u.Current.Name);
        }

        [TestMethod]
        public void UnitMvvm_Mp3()
        {
            DiagsPresenter vm = new MockDiagsView().ViewModel;
            int tries = 1200;

            vm.Root = @"Targets\Singles\02-WalkedOn.mp3";
            vm.DoCheck.Execute (null);
            for (int jobCounter = vm.JobCounter; jobCounter == vm.JobCounter && tries >= 0; --tries)
                Thread.Sleep (50);

            var mp3 = (Mp3Format) vm.TabMp3.Current;
            Assert.IsFalse (mp3.HasId3v1Phantom);
            Assert.IsTrue (mp3.IsBadData);
            Assert.AreEqual (Severity.Error, mp3.Issues.MaxSeverity);
        }
    }
}
