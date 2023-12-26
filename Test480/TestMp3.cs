using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosFormat;
using KaosIssue;

namespace TestDiags
{
    [TestClass]
    public class TestMp3
    {
        [TestMethod]
        public void UnitMp3_1()
        {
            var fileName1 = @"Targets\Singles\01-Phantom.mp3";

            using (Stream fs = new FileStream (fileName1, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                fs.Read (hdr, 0, hdr.Length);

                Mp3Format.Model mp3Model = Mp3Format.CreateModel (fs, hdr, fileName1);
                Mp3Format mp3 = mp3Model.Data;

                Assert.IsNotNull (mp3);
                Assert.AreEqual (Severity.Warning, mp3.Issues.MaxSeverity);
                Assert.AreEqual (2, mp3.Issues.Items.Count);
                Assert.IsTrue (mp3.HasId3v1Phantom);

                var repairMessage = mp3Model.RepairPhantomTag (true);
                Assert.IsNull (repairMessage);

                mp3Model.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.IsFalse (mp3.IsBadHeader);
                Assert.IsFalse (mp3.IsBadData);
            }
        }


        [TestMethod]
        public void UnitMp3_BadCRC()
        {
            var fName1 = @"Targets\Singles\02-WalkedOn.mp3";

            using (Stream fs = new FileStream (fName1, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                fs.Read (hdr, 0, hdr.Length);

                Mp3Format.Model mp3Model = Mp3Format.CreateModel (fs, hdr, fName1);

                mp3Model.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.IsTrue (mp3Model.Data.IsBadData);
                Assert.AreEqual (Severity.Error, mp3Model.Data.Issues.MaxSeverity);
            }
        }
    }
}
