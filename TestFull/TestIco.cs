using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosIssue;
using KaosFormat;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtIco
    {
        [TestMethod]
        public void UnitIco_1()
        {
            var fName1 = @"Targets\Singles\Korean.ico";

            IcoFormat fmt;
            using (Stream fs = new FileStream (fName1, FileMode.Open, FileAccess.Read))
            {
                var hdr = new byte[0x20];
                var got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                IcoFormat.Model icoModel = IcoFormat.CreateModel (fs, hdr, fName1);
                fmt = icoModel.Data;

                Assert.IsNull (fmt.FileMD5ToHex);
                Assert.IsNull (fmt.FileSHA1ToHex);
                icoModel.CalcHashes (Hashes.FileMD5 | Hashes.FileSHA1, 0);
                Assert.IsNotNull (fmt.FileMD5ToHex);
                Assert.AreEqual ("aac4e9f76f7e67bc6cad5f8c356fd53cdf70135f", fmt.FileSHA1ToHex.ToLower());
            }

            Assert.IsNotNull (fmt);
            Assert.IsTrue (fmt.Issues.MaxSeverity == Severity.NoIssue);
            Assert.AreEqual (0, fmt.Issues.Items.Count);

            Assert.AreEqual (3, fmt.Count);
            foreach (var item in fmt.Icons)
            {
                Assert.AreEqual (16, item.Width);
                Assert.AreEqual (16, item.Height);
                Assert.IsFalse (item.IsPNG);
            }
        }


        [TestMethod]
        public void UnitIco_Misnamed()
        {
            var fName1 = @"Targets\Singles\DutchIco.jpeg";

            var dummy = new KaosDiags.Diags.Model(null);

            FormatBase fmt;
            using (Stream s1 = new FileStream (fName1, FileMode.Open))
            {
                FormatBase.Model fmtModel = FormatBase.Model.Create (s1, fName1, 0, 0, null, out bool isKnown, out FileFormat actual);
                fmt = fmtModel.Data;

                Assert.IsInstanceOfType (fmtModel, typeof (IcoFormat.Model));
                Assert.AreEqual ("ico", actual.PrimaryName);

                Assert.IsTrue (fmt.Issues.MaxSeverity == Severity.Warning);
                Assert.AreEqual (1, fmt.Issues.Items.Count);
                Assert.AreEqual (1, fmt.Issues.RepairableCount);

                string err = fmtModel.IssueModel.Repair (index:0);
                Assert.IsNull (err);
            }

            Assert.IsTrue (fmt.Issues.MaxSeverity == Severity.Warning);
            Assert.AreEqual (1, fmt.Issues.Items.Count);
            Assert.AreEqual (0, fmt.Issues.RepairableCount);
        }
    }
}
