using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosFormat;
using KaosIssue;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtM3u
    {
        [TestMethod]
        public void UnitM3u_1()
        {
            var fName1 = @"Targets\Hashes\OK02.m3u";
            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var info = new FileInfo (fName1);
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                M3uFormat.Model m3uModel = M3uFormat.CreateModel (fs, hdr, fName1);
                M3uFormat m3u = m3uModel.Data;

                Assert.AreEqual (Severity.NoIssue, m3u.Issues.MaxSeverity);
                Assert.AreEqual (0, m3u.Issues.Items.Count);
                Assert.AreEqual (3, m3u.Files.Items.Count);

                foreach (var item in m3u.Files.Items)
                    Assert.IsNull (item.IsFound);

                m3uModel.CalcHashes (Hashes.None, Validations.Exists);

                Assert.AreEqual (1, m3u.Issues.Items.Count);
                Assert.AreEqual (Severity.Advisory, m3u.Issues.MaxSeverity);
                foreach (var item in m3u.Files.Items)
                    Assert.IsTrue (item.IsFound.Value);
            }
        }

        [TestMethod]
        public void UnitM3u_2()
        {
            var fName1 = @"Targets\Hashes\Bad02.m3u";

            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                M3uFormat.Model m3uModel = M3uFormat.CreateModel (fs, hdr, fName1);
                M3uFormat m3u = m3uModel.Data;

                Assert.IsNotNull (m3uModel);
                Assert.AreEqual (Severity.NoIssue, m3u.Issues.MaxSeverity);
                Assert.AreEqual (0, m3u.Issues.Items.Count);
                Assert.AreEqual (3, m3u.Files.Items.Count);

                foreach (var item in m3u.Files.Items)
                    Assert.IsNull (item.IsFound);

                m3uModel.CalcHashes (Hashes.None, Validations.Exists);

                Assert.AreEqual (2, m3u.Issues.Items.Count);
                Assert.AreEqual (Severity.Error, m3u.Issues.MaxSeverity);
                Assert.IsTrue (m3u.Files.Items[0].IsFound.Value);
                Assert.IsFalse (m3u.Files.Items[1].IsFound.Value);
                Assert.IsTrue (m3u.Files.Items[2].IsFound.Value);
            }
        }
    }
}
