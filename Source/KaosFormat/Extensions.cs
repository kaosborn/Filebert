using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace KaosFormat
{
    public static partial class StringBuilderExtensions
    {
        public static StringBuilder AppendHexString (this StringBuilder sb, byte[] data)
        {
            foreach (byte octet in data)
                sb.Append (octet.ToString ("X2"));
            return sb;
        }
    }


    public static partial class StreamExtensions
    {
        // Wobbly Transformation Format 8
        // 1- to 7-byte uncooked & extended UTF-8 for up to 36 bits of storage.
        // See wikipedia.org/wiki/UTF-8#WTF-8
        // On exit:
        //   returns <0 if read or encoding error;
        //   else returns decoding with stream advanced to next unread byte, buf contains encoding.
        public static long ReadWobbly (this Stream stream, out byte[] buf)
        {
            int octet = stream.ReadByte();
            if (octet < 0)
            { buf = null; return octet; }

            if ((octet & 0x80) == 0)
            { buf = new byte[] { (byte) octet }; return octet; }

            if (octet == 0xFF)
            { buf = new byte[] { (byte) octet }; return -1; }

            int followCount = 1;
            byte mask;
            for (mask = 0x20; (octet & mask) != 0; mask >>= 1)
                ++followCount;

            buf = new byte[1+followCount];
            buf[0] = (byte) octet;
            int got = stream.Read (buf, 1, followCount);
            if (got != followCount)
                return -9;

            long result = octet & (mask-1);
            for (int ix = 1; ix < buf.Length; ++ix)
            {
                octet = buf[ix];
                if ((octet & 0xC0) != 0x80)
                    return -ix-1;
                result = (result << 6) | (uint) (octet & 0x3F);
            }

            return result;
        }
    }


    /// <summary>
    /// Extend the functionality of System.Convert and System.BitConverter.
    /// </summary>
    public static class ConvertTo
    {
        public static Int32 FromBig16ToInt32 (byte[] data, int index)
        { unchecked { return data[index] << 8 | data[index+1]; } }

        public static Int32 FromBig24ToInt32 (byte[] data, int index)
        { unchecked { return data[index] << 16 | data[index+1] << 8 | data[index+2]; } }

        public static UInt32 FromBig24ToUInt32 (byte[] data, int index)
        { unchecked { return (UInt32) data[index] << 16 | (UInt32) data[index+1] << 8 | data[index+2]; } }

        public static Int32 FromBig32ToInt32 (byte[] data, int index)
        { unchecked { return data[index] << 24 | data[index+1] << 16 | data[index+2] << 8 | data[index+3]; } }

        public static UInt32 FromBig32ToUInt32 (byte[] data, int index)
        { unchecked { return (UInt32) data[index] << 24 | (UInt32) data[index+1] << 16 | (UInt32) data[index+2] << 8 | data[index+3]; } }

        public static Int32 FromLit16ToInt32 (byte[] data, int index)
        { unchecked { return data[index] | data[index+1] << 8; } }

        public static Int32 FromLit32ToInt32 (byte[] data, int index)
        { unchecked { return data[index] | data[index+1] << 8 | data[index+2] << 16 | data[index+3] << 24; } }

        public static UInt32 FromLit32ToUInt32 (byte[] data, int index)
        { unchecked { return data[index] | (UInt32) data[index+1] << 8 | (UInt32) data[index+2] << 16 | (UInt32) data[index+3] << 24; } }

        // returns count advanced in source
        public static int FromStringToInt32 (string source, int offset, out int result)
        {
            int start = offset;
            while (start < source.Length && Char.IsWhiteSpace (source[start]))
                ++start;
            int stop = start;
            while (stop < source.Length && Char.IsDigit (source[stop]))
                ++stop;
            int.TryParse (source.Substring (start, stop-start), out result);
            return stop-offset;
        }

        public static string FromAsciizToString (byte[] data, int offset = 0)
        {
            int stop = offset;
            while (stop < data.Length && data[stop] != 0)
                ++stop;
            return Encoding.ASCII.GetString (data, offset, stop - offset);
        }


        public static string FromAsciiToString (byte[] data, int offset, int length)
        {
            string result = String.Empty;
            for (; --length >= 0; ++offset)
                if (data[offset] >= 32 && data[offset] < 127)
                    result += (char) data[offset];
                else
                    break;
            return result;
        }

        public static bool StartsWithAscii (byte[] data, int offset, string val)
        {
            if (offset + val.Length >= data.Length)
                return false;
            for (int ix = 0; ix < val.Length; ++ix)
                if (data[offset+ix] != val[ix])
                    return false;
            return true;
        }

        public static string ToBitString (byte[] data, int length)
        {
            var sb = new StringBuilder (length * 10 - 1);
            for (int ix = 0;;)
            {
                for (int mask = 0x80;;)
                {
                    sb.Append ((data[ix] & mask) == 0 ? '0' : '1');
                    mask >>= 1;
                    if (mask == 0)
                        break;
                    else if (mask == 8)
                        sb.Append (' ');
                }
                if (++ix >= length)
                    break;
                sb.Append (' ');
            }
            return sb.ToString();
        }


        public static string ToBitString (int value, int bitCount)
        {
            var sb = new StringBuilder (bitCount + (bitCount>>2));
            if (bitCount < 0 || bitCount >= 32)
            {
                sb.Append ((value & 0x80000000) == 0 ? '0' : '1');
                bitCount = 31;
            }
            for (int mask = 1 << (bitCount - 1);;)
            {
                sb.Append ((value & mask) == 0 ? '0' : '1');
                mask >>= 1;
                if (mask == 0)
                    break;
                if ((mask & 0x08888888) != 0)
                    sb.Append (' ');
            }
            return sb.ToString();
        }


        public static string ToHexString (byte[] data)
        {
            var sb = new StringBuilder (data.Length * 2);
            return sb.AppendHexString (data).ToString();
        }


        public static byte[] FromHexStringToBytes (string hs, int start, int len)
        {
            var hash = new byte[len];
            for (var hx = 0; hx < len; ++hx)
            {
                if (! Byte.TryParse (hs.Substring (start+hx*2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte octet))
                    return null;
                hash[hx] = octet;
            }
            return hash;
        }
    }
}
