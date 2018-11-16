using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestDiags
{
    [TestClass]
    public class TestDiags
    {
        [TestMethod]
        public void UnitV_FrameworkProfile()
        {
            Assembly view = Assembly.GetAssembly (typeof (KaosDiags.Diags));
            TargetFrameworkAttribute vFramework = view.GetCustomAttributes(false).OfType<TargetFrameworkAttribute>().Single();

            // Current LCD target framework:
            Assert.AreEqual (".NETFramework,Version=v4.0,Profile=Client", vFramework.FrameworkName);
        }

        [TestMethod]
        public void UnitM_FormatList()
        {
            // Spin up model to initialize the class variables.
            KaosDiags.Diags model = new KaosDiags.Diags.Model(null).Data;
            string formatListText = KaosDiags.Diags.FormatListText;
            Assert.AreEqual ("ape, asf/wmv/wma, avi/divx, cue, db (Thumbs), flac, flv, gif, ico, jpg/jpeg, log (EAC), log (XLD), m3u, m3u8, m4a, md5, mkv/mka, mov/qt, mp3, mp4, mpg/mpeg/vob, ogg, png, sha1, sha1x, sha256, wav", formatListText);
        }

        [TestMethod]
        public void UnitVm_FormatList()
        {
            // Spin up viewModel to initialize the class variables.
            AppViewModel.DiagsPresenter viewModel = new MockDiagsView().ViewModel;
            string formatListText = KaosDiags.Diags.FormatListText;
            Assert.AreEqual ("ape, asf/wmv/wma, avi/divx, cue, db (Thumbs), flac, flv, gif, ico, jpg/jpeg, log (EAC), log (XLD), m3u, m3u8, m4a, md5, mkv/mka, mov/qt, mp3, mp4, mpg/mpeg/vob, ogg, png, sha1, sha1x, sha256, wav", formatListText);
        }
    }
}
