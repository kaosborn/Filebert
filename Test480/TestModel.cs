﻿using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestDiags
{
    [TestClass]
    public class TestModel
    {
        public string ExpectedTypes => "aif/aiff/aifc, ape, asf/wmv/wma, avi/divx, cue, db (Thumbs), flac, flv, gif, ico, jpg/jpeg, log (EAC), log (XLD), m3u, m3u8, m4a, md5, mkv/mka/webm, mov/qt, mp3, mp4, mpg/mpeg/vob, ogg/ogm/ogv, png, sha1, sha1x, sha256, wav";

        [TestMethod]
        public void UnitV_FrameworkProfile()
        {
            // Check for least common denominator full framework:
            Assembly view = Assembly.GetAssembly (typeof (KaosDiags.Diags));
            TargetFrameworkAttribute vFramework = view.GetCustomAttributes(false).OfType<TargetFrameworkAttribute>().Single();
            Assert.AreEqual (".NETFramework,Version=v4.8", vFramework.FrameworkName);
        }

        [TestMethod]
        public void UnitM_FormatList()
        {
            // Spin up a Diags model to initialize the class variables.
            KaosDiags.Diags diags = new KaosDiags.Diags.Model(null).Data;
            string formatListText = KaosDiags.Diags.FormatListText;
            Assert.AreEqual (ExpectedTypes, formatListText);
        }

        [TestMethod]
        public void UnitVM_FormatList()
        {
            // Spin up a mocked view to initialize the class variables.
            AppViewModel.DiagsPresenter viewModel = new MockDiagsView().ViewModel;
            string formatListText = KaosDiags.Diags.FormatListText;
            Assert.AreEqual (ExpectedTypes, formatListText);
        }
    }
}
