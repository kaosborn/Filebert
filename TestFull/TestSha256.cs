using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosFormat;
using KaosIssue;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtSha256
    {
        [TestMethod]
        public void Sha256_OK3()
        {
            var fn = @"Targets\Hashes\OK03.sha256";
            using (var fs = new FileStream (fn, FileMode.Open, FileAccess.Read))
            {
                var file = new FileInfo (fn);
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                Sha256Format.Model sha256Model = Sha256Format.CreateModel (fs, hdr, fs.Name);
                Assert.IsNotNull (sha256Model);

                Sha256Format sha256 = sha256Model.Data;

                Assert.IsTrue (sha256.Issues.MaxSeverity == Severity.NoIssue);
                Assert.AreEqual (0, sha256.Issues.Items.Count);
                Assert.AreEqual (2, sha256.HashedFiles.Items.Count);

                sha256Model.CalcHashes (0, Validations.SHA256);
                Assert.AreEqual (1, sha256.Issues.Items.Count);
                Assert.AreEqual (Severity.Advisory, sha256.Issues.MaxSeverity);
            }
        }

        [TestMethod]
        public void Sha256_Empty()
        {
            var fn = @"Targets\Hashes\0bytes.sha256";
            using (var fs = new FileStream (fn, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (0, got);

                Sha256Format.Model sha256Model = Sha256Format.CreateModel (fs, hdr, fs.Name);
                Assert.IsNotNull (sha256Model);

                Sha256Format sha256 = sha256Model.Data;

                Assert.IsTrue (sha256.Issues.MaxSeverity == Severity.NoIssue);
                Assert.AreEqual (0, sha256.Issues.Items.Count);

                Assert.AreEqual (0, sha256.HashedFiles.Items.Count);

                sha256Model.CalcHashes (0, Validations.SHA256);
                Assert.AreEqual (1, sha256.Issues.Items.Count);
                Assert.AreEqual (Severity.Advisory, sha256.Issues.MaxSeverity);
            }
        }
    }
}
