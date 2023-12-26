using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using KaosFormat;

namespace TestDiags
{
    [TestClass]
    public class TestCue
    {
        [TestMethod]
        public void UnitCue_Rip1()
        {
            var targetDir = @"Targets\Rips\Tester - 2000 - FLAC Repairable\";
            var expectedNames = new string[]
            {
                "01 - 4 frames of silence.flac", "02 - 8 frames of silence.flac", "03 - 12 frames of silence.flac",
                "!Cue with 4 files.cue", "Cue with 3 files.cue", "Tester - 2000 - FLAC Silence.log"
            };

            var diagsModel = new KaosDiags.Diags.Model (targetDir);
            diagsModel.Data.IsRepairEnabled = true;
            diagsModel.Data.ValidationFlags = Validations.Exists;

            var formatModels = new List<FormatBase.Model>();
            int actualIx = 0;
            foreach (FormatBase.Model fmtModel in diagsModel.CheckRoot())
            {
                Assert.AreEqual (expectedNames[actualIx], fmtModel.Data.Name);
                formatModels.Add (fmtModel);
                ++actualIx;
            }

            Assert.AreEqual (expectedNames.Length, actualIx);

            var cue3 = (CueFormat.Model)formatModels[4];
            Assert.IsFalse (cue3.IssueModel.Data.HasError);
            Assert.AreEqual (1, cue3.IssueModel.Data.RepairableCount);

            var cue4 = (CueFormat.Model)formatModels[3];
            Assert.IsTrue (cue4.IssueModel.Data.HasError);
        }
    }
}
