using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosIssue;
using KaosFormat;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtGif
    {
        [TestMethod]
        public void Gif_1()
        {
            var fName1 = @"Targets\Singles\pic300x301.gif";

            GifFormat gif;
            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                GifFormat.Model gifModel;
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                gifModel = GifFormat.CreateModel (fs, hdr, fName1);
                gif = gifModel.Data;
            }

            Assert.AreEqual (300, gif.Width);
            Assert.AreEqual (301, gif.Height);

            Assert.AreEqual (Severity.NoIssue, gif.Issues.MaxSeverity);
            Assert.AreEqual (0, gif.Issues.Items.Count);
        }
    }
}
