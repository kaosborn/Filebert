using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosIssue;
using KaosFormat;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtFlac
    {
        [TestMethod]
        public void UnitFlac_1()
        {
            var fName1 = @"Targets\Singles\03-Silence.flac";

            FlacFormat flac;
            using (Stream s1 = new FileStream (fName1, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                s1.Read (hdr, 0, hdr.Length);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (s1, hdr, fName1);
                flac = flacModel.Data;

                var isBadHdr = flacModel.Data.IsBadHeader;
                Assert.IsFalse (isBadHdr);

                var isBadData = flacModel.Data.IsBadData;
                Assert.IsFalse (isBadData);
            }

            Assert.IsTrue (flac.Issues.MaxSeverity == Severity.NoIssue);
        }


        [TestMethod]
        public void UnitFlac_2()
        {
            var fName1 = @"Targets\Singles\04-BadCRC.flac";

            FlacFormat flac;
            using (Stream s1 = new FileStream (fName1, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                s1.Read (hdr, 0, hdr.Length);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (s1, hdr, fName1);
                flac = flacModel.Data;

                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);

                var isBadHdr = flacModel.Data.IsBadHeader;
                Assert.IsFalse (isBadHdr);

                var isBadData = flacModel.Data.IsBadData;
                Assert.IsTrue (isBadData);
            }

            Assert.IsTrue (flac.Issues.MaxSeverity == Severity.Error);
        }
    }
}
