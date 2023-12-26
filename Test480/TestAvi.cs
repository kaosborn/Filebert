using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosIssue;
using KaosFormat;

namespace TestDiags
{
    [TestClass]
    public class TestAvi
    {
        [TestMethod]
        public void UnitAvi_1()
        {
            var fName1 = @"Targets\Singles\MarkedCrimeBlimp.avi";

            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.ReadWrite))
            {
                var hdr = new byte[0x2C];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                AviFormat.Model aviModel = AviFormat.CreateModel (fs, hdr, fName1);
                AviFormat avi = aviModel.Data;
                long fileSize = avi.FileSize;

                Assert.AreEqual (Severity.Warning, avi.Issues.MaxSeverity);
                Assert.AreEqual (1, avi.Issues.Items.Count);
                Assert.AreEqual (1, avi.Issues.RepairableCount);
                Assert.IsTrue (avi.Issues.Items[0].IsRepairable);
                Assert.AreEqual (5, avi.ExcessSize);
                Assert.AreEqual (Likeliness.Probable, avi.Watermark);

                string err = aviModel.IssueModel.Repair (0);
                Assert.IsNull (err);
                Assert.AreEqual (fileSize - 5, avi.FileSize);
            }
        }
    }
}
