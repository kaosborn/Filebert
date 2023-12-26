using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosFormat;
using KaosIssue;

namespace TestDiags
{
    [TestClass]
    public class TestSha256
    {
        [TestMethod]
        public void UnitSha256_OK3()
        {
            var fName1 = @"Targets\Hashes\OK03.sha256";

            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                Sha256Format.Model sha256Model = Sha256Format.CreateModel (fs, hdr, fName1);
                Sha256Format sha256 = sha256Model.Data;
                Assert.AreEqual (Severity.NoIssue, sha256.Issues.MaxSeverity);
                Assert.AreEqual (0, sha256.Issues.Items.Count);
                Assert.AreEqual (2, sha256.HashedFiles.Items.Count);

                sha256Model.CalcHashes (0, Validations.SHA256);

                Assert.AreEqual (1, sha256.Issues.Items.Count);
                Assert.AreEqual (Severity.Advisory, sha256.Issues.MaxSeverity);
            }
        }

        [TestMethod]
        public void UnitSha256_Empty()
        {
            var fName1 = @"Targets\Hashes\0bytes.sha256";

            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (0, got);

                Sha256Format.Model sha256Model = Sha256Format.CreateModel (fs, hdr, fName1);
                Sha256Format sha256 = sha256Model.Data;

                Assert.AreEqual (Severity.NoIssue, sha256.Issues.MaxSeverity);
                Assert.AreEqual (0, sha256.Issues.Items.Count);
                Assert.AreEqual (0, sha256.HashedFiles.Items.Count);

                sha256Model.CalcHashes (0, Validations.SHA256);

                Assert.AreEqual (1, sha256.Issues.Items.Count);
                Assert.AreEqual (Severity.Advisory, sha256.Issues.MaxSeverity);
            }
        }
    }
}
