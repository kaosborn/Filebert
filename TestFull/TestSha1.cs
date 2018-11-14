using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosFormat;
using KaosIssue;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtSha1
    {
        [TestMethod]
        public void Sha1_OK()
        {
            var fn = @"Targets\Hashes\OK01.sha1";
            using (var fs = new FileStream (fn, FileMode.Open, FileAccess.Read))
            {
                var file = new FileInfo (fn);
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                Sha1Format.Model sha1Model = Sha1Format.CreateModel (fs, hdr, fs.Name);
                Assert.IsNotNull (sha1Model);

                Sha1Format sha1 = sha1Model.Data;

                Assert.IsTrue (sha1.Issues.MaxSeverity == Severity.NoIssue);
                Assert.AreEqual (0, sha1.Issues.Items.Count);
                Assert.AreEqual (2, sha1.HashedFiles.Items.Count);

                sha1Model.CalcHashes (0, Validations.SHA1);
                Assert.AreEqual (1, sha1.Issues.Items.Count);
                Assert.AreEqual (Severity.Advisory, sha1.Issues.MaxSeverity);
            }
        }


        [TestMethod]
        public void Sha1_Bad()
        {
            var fn = @"Targets\Hashes\Bad01.sha1";
            using (var fs = new FileStream (fn, FileMode.Open, FileAccess.Read))
            {
                var file = new FileInfo (fn);
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                Sha1Format.Model sha1Model = Sha1Format.CreateModel (fs, hdr, fs.Name);
                Assert.IsNotNull (sha1Model);

                Sha1Format sha1 = sha1Model.Data;

                Assert.IsTrue (sha1.Issues.MaxSeverity == Severity.NoIssue);
                Assert.AreEqual (0, sha1.Issues.Items.Count);
                Assert.AreEqual (2, sha1.HashedFiles.Items.Count);

                sha1Model.CalcHashes (0, Validations.SHA1);
                Assert.AreEqual (2, sha1.Issues.Items.Count);
                Assert.AreEqual (Severity.Error, sha1.Issues.MaxSeverity);
            }
        }
    }
}
