using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestDiags
{
    [TestClass]
    public class TestDiags
    {
        [TestMethod]
        public void UnitDiags_FrameworkProfile()
        {
            var assembly = System.Reflection.Assembly.GetAssembly (typeof (KaosDiags.Diags));
            var att = assembly.GetCustomAttributes(false).OfType<System.Runtime.Versioning.TargetFrameworkAttribute>().Single();

            // The View assembly should match.
            Assert.AreEqual (".NETFramework,Version=v4.0,Profile=Client", att.FrameworkName);
        }

        [TestMethod]
        public void UnitDiags_FormatList()
        {
            // Spin up a model to initialize the class variables.
            KaosDiags.Diags diags = new KaosDiags.Diags.Model(null).Data;
            string formatListText = KaosDiags.Diags.FormatListText;
            Assert.AreEqual ("ape, asf/wmv/wma, avi/divx, cue, db (Thumbs), flac, flv, gif, ico, jpg/jpeg, log (EAC), log (XLD), m3u, m3u8, m4a, md5, mkv/mka, mov/qt, mp3, mp4, mpg/mpeg/vob, ogg, png, sha1, sha1x, sha256, wav", formatListText);
        }

        [TestMethod]
        public void UnitVm_FormatList()
        {
            // Spin up a viewModel to initialize the class variables.
            AppViewModel.DiagsPresenter viewModel = new MockDiagsView().ViewModel;
            string formatListText = KaosDiags.Diags.FormatListText;
            Assert.AreEqual ("ape, asf/wmv/wma, avi/divx, cue, db (Thumbs), flac, flv, gif, ico, jpg/jpeg, log (EAC), log (XLD), m3u, m3u8, m4a, md5, mkv/mka, mov/qt, mp3, mp4, mpg/mpeg/vob, ogg, png, sha1, sha1x, sha256, wav", formatListText);
        }
    }
}
