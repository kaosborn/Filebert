using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using KaosIssue;
using KaosFormat;

namespace TestDiags
{
    [TestClass]
    public class TestFlac
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

            Assert.AreEqual (Severity.NoIssue, flac.Issues.MaxSeverity);
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

        [TestMethod]
        public void UnitFlac_OverPad()
        {
            var fn = @"Targets\Singles\05-OverPad.flac";

            FlacFormat flac;
            using (Stream s1 = new FileStream (fn, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                s1.Read (hdr, 0, hdr.Length);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (s1, hdr, fn);
                flac = flacModel.Data;

                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);

                Assert.AreEqual (Severity.Warning, flac.Issues.MaxSeverity);
                Assert.AreEqual (1, flac.Issues.RepairableCount);

                string repairErr = flacModel.RepairArtPadBloat (true);
                Assert.IsNull (repairErr);
            }
        }

        [TestMethod]
        public void UnitFlac_OverArt()
        {
            var fn = @"Targets\Singles\06-OverArt.flac";

            FlacFormat flac;
            using (Stream s1 = new FileStream (fn, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                s1.Read (hdr, 0, hdr.Length);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (s1, hdr, fn);
                flac = flacModel.Data;

                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);

                Assert.AreEqual (Severity.Warning, flac.Issues.MaxSeverity);
                Assert.AreEqual (1, flac.Issues.RepairableCount);

                string repairErr = flacModel.RepairArtPadBloat (true);
                Assert.IsNull (repairErr);
            }
        }

        [TestMethod]
        public void UnitFlac_OverArtPad()
        {
            var fn = @"Targets\Singles\07-OverPadArt.flac";

            FlacFormat flac;
            using (Stream s1 = new FileStream (fn, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                s1.Read (hdr, 0, hdr.Length);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (s1, hdr, fn);
                flac = flacModel.Data;

                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);

                Assert.AreEqual (Severity.Warning, flac.Issues.MaxSeverity);
                Assert.AreEqual (1, flac.Issues.RepairableCount);

                string repairErr = flacModel.RepairArtPadBloat (true);
                Assert.IsNull (repairErr);
            }
        }

        [TestMethod]
        public void UnitFlac_NoPadArt()
        {
            var fn = @"Targets\Singles\08-NoPadArt.flac";

            FlacFormat flac;
            using (Stream s1 = new FileStream (fn, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                s1.Read (hdr, 0, hdr.Length);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (s1, hdr, fn);
                flac = flacModel.Data;

                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);

                Assert.AreEqual (Severity.Noise, flac.Issues.MaxSeverity);
                Assert.AreEqual (0, flac.Issues.RepairableCount);
            }
        }
    }
}
