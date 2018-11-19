using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosIssue;
using KaosFormat;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtWav
    {
        [TestMethod]
        public void UnitWav_1()
        {
            var fName1 = @"Targets\Singles\StereoSilence10.wav";

            WavFormat wav;
            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x2C];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                WavFormat.Model wavModel = WavFormat.CreateModel (fs, hdr, fName1);
                wav = wavModel.Data;
            }

            Assert.AreEqual (Severity.NoIssue, wav.Issues.MaxSeverity);
            Assert.AreEqual (0, wav.Issues.Items.Count);
            Assert.AreEqual (2, wav.ChannelCount);
            Assert.AreEqual (44100u, wav.SampleRate);
            Assert.IsTrue (wav.HasTags);
        }
    }
}
