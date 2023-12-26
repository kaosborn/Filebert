using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosFormat;
using KaosIssue;

namespace TestDiags
{
    [TestClass]
    public class TestSha1
    {
        [TestMethod]
        public void UnitSha1_OK()
        {
            var fName1 = @"Targets\Hashes\OK01.sha1";

            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                Sha1Format.Model sha1Model = Sha1Format.CreateModel (fs, hdr, fName1);
                Sha1Format sha1 = sha1Model.Data;

                Assert.AreEqual (Severity.NoIssue, sha1.Issues.MaxSeverity);
                Assert.AreEqual (0, sha1.Issues.Items.Count);
                Assert.AreEqual (2, sha1.HashedFiles.Items.Count);

                sha1Model.CalcHashes (0, Validations.SHA1);
                Assert.AreEqual (1, sha1.Issues.Items.Count);
                Assert.AreEqual (Severity.Advisory, sha1.Issues.MaxSeverity);
            }
        }


        [TestMethod]
        public void UnitSha1_Bad()
        {
            var fName1 = @"Targets\Hashes\Bad01.sha1";

            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                Sha1Format.Model sha1Model = Sha1Format.CreateModel (fs, hdr, fName1);
                Sha1Format sha1 = sha1Model.Data;

                Assert.AreEqual (Severity.NoIssue, sha1.Issues.MaxSeverity);
                Assert.AreEqual (0, sha1.Issues.Items.Count);
                Assert.AreEqual (2, sha1.HashedFiles.Items.Count);

                sha1Model.CalcHashes (0, Validations.SHA1);
                Assert.AreEqual (2, sha1.Issues.Items.Count);
                Assert.AreEqual (Severity.Error, sha1.Issues.MaxSeverity);
            }
        }
    }
}
