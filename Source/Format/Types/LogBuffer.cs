using System;
using System.Globalization;
using System.Text;
using KaosIssue;

namespace KaosFormat
{
    /// <summary>
    /// Provides primitive text parsing for a cached text file.
    /// </summary>
    public class LogBuffer
    {
        private readonly byte[] buf;
        private int bufPos;
        private int linePos;
        private Encoding encoding;

        public int FilePosition
         => bufPos;

        public int LinePosition
         => linePos;

        public int LineNum
        { get; private set; }

        public bool EOF
         => bufPos >= buf.Length;

        public string GetPlace()
         => "line " + LineNum.ToString();

        public LogBuffer (byte[] data, Encoding encoding)
        {
            this.buf = data;
            this.encoding = encoding;
            this.bufPos = encoding == Encoding.Unicode ? 2 : 0;
        }

        // Get next non-blank line, left trimmed.
        public string ReadLineLTrim()
         => ReadLineNonempty().TrimStart();

        // Get next non-blank line, untrimmed.
        public string ReadLineNonempty()
        {
            if (EOF)
                return String.Empty;

            for (;;)
            {
                string result = ReadLine();
                if (EOF || ! String.IsNullOrWhiteSpace (result))
                    return result;
            }
        }

        // Get next line.
        public string ReadLine()
        {
            int eolWidth = 0;
            for (linePos = bufPos; bufPos < buf.Length; ++bufPos)
                if (buf[bufPos]==0x0A)
                { eolWidth = 1; ++bufPos; break; }
                else if (buf[bufPos]==0x0D)
                    if (encoding == Encoding.Unicode)
                    {
                        if (bufPos < buf.Length-3 && buf[bufPos+1]==0 && buf[bufPos+2]==0x0A && buf[bufPos+3]==0)
                        { eolWidth = 4; bufPos += 4; break; }
                    }
                    else if (bufPos < buf.Length-1 && buf[bufPos+1]==0x0A)
                    { eolWidth = 2; bufPos += 2; break; }

            ++LineNum;
            string result = encoding.GetString (buf, linePos, bufPos-linePos-eolWidth);
            return result;
        }
    }
}
