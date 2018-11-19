using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using KaosFormat;
using KaosIssue;

namespace TestDiags
{
    [TestClass]
    public class UnitFmtLogEac
    {
        [TestMethod]
        public void UnitLogEac_OK1()
        {
            var dName1 = @"Targets\Rips\Tester - 2000 - FLAC Silence OK1\";
            var fName1 = dName1 + "Tester - 2000 - FLAC Silence OK1.log";

            LogEacFormat.Model logModel;
            LogEacFormat log;

            using (Stream fs = new FileStream (fName1, FileMode.Open))
            {
                var hdr = new byte[0x28];
                fs.Read (hdr, 0, hdr.Length);
                logModel = LogEacFormat.CreateModel (fs, hdr, fName1);
                log = logModel.Data;
            }

            Assert.AreEqual (3, log.Tracks.Items.Count);

            for (var ix = 0; ix < log.Tracks.Items.Count; ++ix)
            {
                LogEacTrack track = log.Tracks.Items[ix];

                Assert.AreEqual (ix + 1, track.Number);
                Assert.IsTrue (track.HasOk);
                Assert.AreEqual (track.TestCRC, track.CopyCRC);
            }

            var dInfo = new DirectoryInfo (dName1);
            var flacInfos = dInfo.GetFiles ("*.flac");

            Assert.AreEqual (3, flacInfos.Length);

            var flacs = new List<FlacFormat>();
            foreach (var flacInfo in flacInfos)
            {
                Stream fs = new FileStream (flacInfo.FullName, FileMode.Open, FileAccess.Read);
                var hdr = new byte[0x28];
                fs.Read (hdr, 0, hdr.Length);

                FlacFormat.Model flacModel = FlacFormat.CreateModel (fs, hdr, flacInfo.FullName);
                Assert.IsNotNull (flacModel);

                flacModel.CalcHashes (Hashes.Intrinsic|Hashes.PcmCRC32, Validations.None);
                flacs.Add (flacModel.Data);
            }

            logModel.ValidateRip (flacs, checkTags:true);

            for (var ix = 0; ix < log.Tracks.Items.Count; ++ix)
            {
                var track = log.Tracks.Items[ix];
                var flac = track.Match as FlacFormat;
                var trackNumTag = flac.GetTagValue ("TRACKNUMBER");

                Assert.IsNotNull (flac);
                Assert.AreEqual (1, flac.Issues.Items.Count);
                Assert.AreEqual (Severity.Noise, flac.Issues.MaxSeverity);
                Assert.AreEqual ((ix+1).ToString(), trackNumTag);
                Assert.IsFalse (flac.IsBadHeader);
                Assert.IsFalse (flac.IsBadData);
            }

            Assert.AreEqual (0x6522DF69u, ((FlacFormat) log.Tracks.Items[0].Match).ActualPcmCRC32);
            Assert.AreEqual (0x003E740Du, ((FlacFormat) log.Tracks.Items[1].Match).ActualPcmCRC32);
            Assert.AreEqual (0xFAB5205Fu, ((FlacFormat) log.Tracks.Items[2].Match).ActualPcmCRC32);
        }

        [TestMethod]
        public void UnitLogEac_Bad1()
        {
            var dName1 = @"Targets\Rips\Tester - 2000 - FLAC Silence Bad1\";
            var fName1 = dName1 + "Tester - 2000 - FLAC Silence Bad1.log";

            LogEacFormat.Model logModel;
            LogEacFormat log;
            using (Stream fs = new FileStream (fName1, FileMode.Open))
            {
                var hdr = new byte[0x28];
                fs.Read (hdr, 0, hdr.Length);
                logModel = LogEacFormat.CreateModel (fs, hdr, fName1);
                log = logModel.Data;
            }

            Assert.AreEqual (3, log.Tracks.Items.Count);

            for (var ix = 0; ix < log.Tracks.Items.Count; ++ix)
            {
                LogEacTrack track = log.Tracks.Items[ix];

                Assert.AreEqual (ix + 1, track.Number);
                Assert.IsTrue (track.HasOk);
                Assert.AreEqual (track.TestCRC, track.CopyCRC);
            }

            var flacs = new List<FlacFormat>();
            var dInfo1 = new DirectoryInfo (dName1);

            FileInfo[] flacInfos = dInfo1.GetFiles ("*.flac");
            foreach (var flacInfo in flacInfos)
                using (Stream fs = new FileStream (flacInfo.FullName, FileMode.Open, FileAccess.Read))
                {
                    var hdr = new byte[0x28];
                    fs.Read (hdr, 0, hdr.Length);
                    var flacModel = FlacFormat.CreateModel (fs, hdr, flacInfo.FullName);

                    Assert.IsNotNull (flacModel);
                    Assert.IsTrue (flacModel.Data.Issues.MaxSeverity < Severity.Warning);

                    flacModel.CalcHashes (Hashes.Intrinsic|Hashes.PcmCRC32, Validations.None);
                    flacs.Add (flacModel.Data);
                }

            logModel.ValidateRip (flacs, checkTags:false);

            Assert.AreEqual (3, flacInfos.Length);
            Assert.AreEqual (3, flacs.Count);
            Assert.AreEqual (Severity.Error, log.Issues.MaxSeverity);
        }

        [TestMethod]
        public void UnitLogEac_StrictWeb()
        {
            var fName1 = @"Targets\Singles\Nightmare.log";
            var fName2 = @"Targets\Singles\EAC1NoHashOrCT.log";
            var model = new KaosDiags.Diags.Model (null);

            // Uncomment next line for EAC log self-hash validation. Requires the interweb!
            // model.Data.HashFlags |= Hashes._WebCheck;

            //log1 has a self-hash
            Stream s1 = new FileStream (fName1, FileMode.Open);
            var h1 = new byte[0x2C];
            s1.Read (h1, 0, h1.Length);
            var log1Model = LogEacFormat.CreateModel (s1, h1, fName1);
            log1Model.CalcHashes (model.Data.HashFlags, Validations.None);
            model.Data.ErrEscalator = IssueTags.StrictErr;
            log1Model.IssueModel.Escalate (model.Data.WarnEscalator, model.Data.ErrEscalator);
            var b1 = log1Model.Data;

            Assert.AreEqual (Severity.Error, b1.Issues.MaxSeverity);
            if ((model.Data.HashFlags & Hashes._WebCheck) != 0)
                Assert.IsTrue (b1.ShIssue.Success == true);
            else
                Assert.IsNull (b1.ShIssue);

            // log2 has no self-hash
            Stream s2 = new FileStream (fName2, FileMode.Open);
            var h2 = new byte[0x2C];
            s2.Read (h2, 0, h1.Length);
            var log2Model = LogEacFormat.CreateModel (s2, h2, fName2);
            log2Model.CalcHashes (model.Data.HashFlags, 0);
            model.Data.ErrEscalator = IssueTags.StrictErr;
            log2Model.IssueModel.Escalate (model.Data.WarnEscalator, model.Data.ErrEscalator);
            var b2 = log2Model.Data;

            Assert.IsTrue (b2.Issues.HasError);
            if ((model.Data.HashFlags & Hashes._WebCheck) != 0)
                Assert.IsTrue (b2.ShIssue.Success == false);
            else
                Assert.IsNull (b2.ShIssue);
        }
    }
}
