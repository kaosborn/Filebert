using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using KaosFormat;

namespace TestDiags
{
    [TestClass]
    public class TestExtensions
    {
        [TestMethod]
        public void Unit_ToBitString_2A()
        {
            int val = unchecked ((int) 0xFFFF1E96);

            string result10 = ConvertTo.ToBitString (val, 10);
            string result16 = ConvertTo.ToBitString (val, 16);
            string result32 = ConvertTo.ToBitString (val, 32);

            Assert.AreEqual ("10 1001 0110", result10);
            Assert.AreEqual ("0001 1110 1001 0110", result16);
            Assert.AreEqual ("1111 1111 1111 1111 0001 1110 1001 0110", result32);
        }

        [TestMethod]
        public void Unit_ToBitString_2B()
        {
            var buf = new byte[] { 0x12, 0x34, 0x5E, 0x78 };
            var result = ConvertTo.ToBitString (buf, 3);
            Assert.AreEqual ("0001 0010 0011 0100 0101 1110", result);
        }

        [TestMethod]
        public void Unit_ToHexString_1()
        {
            var buf = new byte[] { 0x12, 0xA4, 0xCD, 0x45 };
            var result = ConvertTo.ToHexString (buf);

            Assert.AreEqual ("12A4CD45", result);
        }

        [TestMethod]
        public void Unit_Big16ToInt()
        {
            var buf = new byte[] { 0x12, 0xA4, 0xCD, 0x45 };
            var result = ConvertTo.FromBig16ToInt32 (buf, 1);

            Assert.AreEqual (0xA4CD, result);
        }

        [TestMethod]
        public void Unit_Big24ToInt()
        {
            var buf = new byte[] { 0x12, 0xA4, 0xCD, 0x45 };
            var result = ConvertTo.FromBig24ToInt32 (buf, 1);


            Assert.AreEqual (result.GetType(), typeof (Int32));
            Assert.AreEqual (0xA4CD45, result);
        }

        [TestMethod]
        public void Unit_Big32ToInt()
        {
            var buf = new byte[] { 0x12, 0xFF, 0xFF, 0xFF, 0xFE, 0xED };
            var result = ConvertTo.FromBig32ToInt32 (buf, 1);

            Assert.AreEqual (result.GetType(), typeof (Int32));
            Assert.AreEqual (-2, result);
        }

        [TestMethod]
        public void Unit_Big32ToUInt()
        {
            var buf = new byte[] { 0x12, 0xFF, 0xA4, 0xCD, 0x45, 0xED };
            var result = ConvertTo.FromBig32ToUInt32 (buf, 1);

            Assert.AreEqual (result.GetType(), typeof (UInt32));
            Assert.AreEqual (0xFFA4CD45, result);
        }

        [TestMethod]
        public void Unit_Lit16ToInt()
        {
            var buf = new byte[] { 0x12, 0xA4, 0xCD, 0x45 };
            var result = ConvertTo.FromLit16ToInt32 (buf, 1);

            Assert.AreEqual (0xCDA4, result);
        }

        [TestMethod]
        public void Unit_Lit32ToInt()
        {
            var buf = new byte[] { 0x12, 0xFD, 0xFF, 0xFF, 0xFF, 0xED };
            var result = ConvertTo.FromLit32ToInt32 (buf, 1);

            Assert.AreEqual (result.GetType(), typeof (Int32));
            Assert.AreEqual (-3, result);
        }

        [TestMethod]
        public void Unit_AsciizToString()
        {
            var buf = new byte[] { 0x30, 0x31, 0x42, 0x43, 0x44, 0, 0x46, 0x47 };
            var result = ConvertTo.FromAsciizToString (buf, 2);

            Assert.AreEqual (result.GetType(), typeof (string));
            Assert.AreEqual ("BCD", result);
        }

        [TestMethod]
        public void Unit_Wobbly()
        {
            var s1 = new MemoryStream (new byte[] { 0x61 });
            var r1 = s1.ReadWobbly (out byte[] buf);
            Assert.AreEqual (0x61, r1);
            Assert.AreEqual (1, buf.Length);
            Assert.AreEqual (0x61, buf[0]);

            r1 = new MemoryStream (new byte[] { 0xFF } ).ReadWobbly (out buf);
            Assert.IsTrue (r1 < 0);
            Assert.AreEqual (1, buf.Length);
            Assert.AreEqual (0xFF, buf[0]);

            r1 = new MemoryStream (new byte[] { 0xC3, 0xBF } ).ReadWobbly (out buf);
            Assert.AreEqual (0xFF, r1);
            Assert.AreEqual (2, buf.Length);
            Assert.AreEqual (0xC3, buf[0]);
            Assert.AreEqual (0xBF, buf[1]);

            r1 = new MemoryStream (new byte[] { 0xDF, 0xBF } ).ReadWobbly (out buf);
            Assert.AreEqual (0x7FF, r1);
            Assert.AreEqual (2, buf.Length);
            Assert.AreEqual (0xDF, buf[0]);
            Assert.AreEqual (0xBF, buf[1]);

            var buf3 = new byte[] { 0xEF, 0xBF, 0xBF };
            var sFFFF = new MemoryStream (buf3).ReadWobbly (out buf);
            Assert.AreEqual (0xFFFF, sFFFF);
            Assert.AreEqual (3, buf.Length);
            Assert.IsTrue (buf3.SequenceEqual (buf));

            var buf4bad = new byte[] { 0xF7, 0xBF, 0x00, 0xBF };
            var bad4 = new MemoryStream (buf4bad).ReadWobbly (out buf);
            Assert.IsTrue (bad4 < 0);
            Assert.AreEqual (4, buf.Length);
            Assert.IsTrue (buf4bad.SequenceEqual (buf));

            var buf7 = new byte[] { 0xFE, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF };
            var maxResult = new MemoryStream (buf7).ReadWobbly (out buf);
            Assert.AreEqual (0xFFFFFFFFF, maxResult);
            Assert.AreEqual (7, buf.Length);
            Assert.IsTrue (buf7.SequenceEqual (buf));
        }
    }
}
