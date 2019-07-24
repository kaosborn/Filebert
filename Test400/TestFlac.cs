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
        public void UnitFlac_BadCRC()
        {
            var f4 = @"Targets\Singles\04-BadCRC.flac";

            using (Stream fs = new FileStream (f4, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f4);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);

                bool isBadHdr = flacModel.Data.IsBadHeader;
                bool isBadData = flacModel.Data.IsBadData;

                Assert.IsFalse (isBadHdr);
                Assert.IsTrue (isBadData);
                Assert.AreEqual (Severity.Error, flacModel.Data.Issues.MaxSeverity);
            }
        }

        [TestMethod]
        public void UnitFlac_NoPadNoArt()
        {
            var f5 = @"Targets\Singles\05-NoPadNoArt.flac";

            using (Stream fs = new FileStream (f5, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f5);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.AreEqual (Severity.Noise, flacModel.Data.Issues.MaxSeverity);
                Assert.AreEqual (0, flacModel.Data.Issues.RepairableCount);
            }
        }

        [TestMethod]
        public void UnitFlac_ZeroPadHugeArt()
        {
            var f6 = @"Targets\Singles\06-ZeroPadHugeArt.flac";

            using (Stream fs = new FileStream (f6, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f6);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.AreEqual (Severity.Trivia, flacModel.Data.Issues.MaxSeverity);
                Assert.AreEqual (0, flacModel.Data.Issues.RepairableCount);

                string err = flacModel.RepairArtPadBloat (isFinalRepair:true);
                Assert.IsNotNull (err);
            }
        }

        [TestMethod]
        public void UnitFlac_ExcessPadNoArt()
        {
            var f7 = @"Targets\Singles\07-ExcessPadNoArt.flac";

            using (Stream fs = new FileStream (f7, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f7);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.AreEqual (Severity.Warning, flacModel.Data.Issues.MaxSeverity);
                Assert.AreEqual (1, flacModel.Data.Issues.RepairableCount);

                string err = flacModel.RepairArtPadBloat (isFinalRepair:true);
                Assert.IsNull (err);
            }

            using (Stream fs = new FileStream (f7, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f7);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.AreEqual (Severity.Noise, flacModel.Data.Issues.MaxSeverity);
                Assert.AreEqual (0, flacModel.Data.Issues.RepairableCount);
            }
        }

        [TestMethod]
        public void UnitFlac_FencePadHasArt()
        {
            var f8 = @"Targets\Singles\08-FencePadHasArt.flac";

            using (Stream fs = new FileStream (f8, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f8);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.AreEqual (Severity.Warning, flacModel.Data.Issues.MaxSeverity);
                Assert.AreEqual (1, flacModel.Data.Issues.RepairableCount);

                string err = flacModel.RepairArtPadBloat (isFinalRepair:true);
                Assert.IsNull (err);
            }

            using (Stream fs = new FileStream (f8, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f8);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.AreEqual (Severity.Noise, flacModel.Data.Issues.MaxSeverity);
                Assert.AreEqual (0, flacModel.Data.Issues.RepairableCount);
            }
        }

        [TestMethod]
        public void UnitFlac_ExcessPadHasArt()
        {
            var f9 = @"Targets\Singles\09-ExcessPadHasArt.flac";

            using (Stream fs = new FileStream (f9, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f9);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.AreEqual (Severity.Warning, flacModel.Data.Issues.MaxSeverity);
                Assert.AreEqual (1, flacModel.Data.Issues.RepairableCount);

                string err = flacModel.RepairArtPadBloat (isFinalRepair:true);
                Assert.IsNull (err);
            }

            using (Stream fs = new FileStream (f9, FileMode.Open))
            {
                var hdr = new byte[0x2C];
                int got = fs.Read (hdr, 0, hdr.Length);
                Assert.AreEqual (hdr.Length, got);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, f9);
                flacModel.CalcHashes (Hashes.Intrinsic, Validations.None);
                Assert.AreEqual (Severity.Noise, flacModel.Data.Issues.MaxSeverity);
                Assert.AreEqual (0, flacModel.Data.Issues.RepairableCount);
            }
        }
    }
}
