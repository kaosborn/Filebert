using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosFormat;
using KaosIssue;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtPng
    {
        [TestMethod]
        public void Png_1()
        {
            var fName1 = @"Targets\Singles\Tile1.png";

            PngFormat png;
            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                var pngModel = new PngFormat.Model (fs, fName1);
                png = pngModel.Data;

                pngModel.CalcHashes (Hashes.Intrinsic, Validations.None);
            }


            Assert.AreEqual (Severity.Noise, png.Issues.MaxSeverity);
            Assert.AreEqual (1, png.Issues.Items.Count);

            Assert.AreEqual (19, png.Width);
            Assert.AreEqual (16, png.Height);
            Assert.AreEqual (0, png.BadCrcCount);

            foreach (var chunk in png.Chunks.Items)
            {
                Assert.AreEqual (chunk.StoredCRC, chunk.ActualCRC);
            }
        }
    }
}
