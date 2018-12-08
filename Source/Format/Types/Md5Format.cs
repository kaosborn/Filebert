using System.IO;
using KaosCrypto;

namespace KaosFormat
{
    public class Md5Format : HashesContainer
    {
        public static string[] SNames => new string[] { "md5" };
        public override string[] Names => SNames;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (path.ToLower().EndsWith(".md5"))
                return new Model (stream, path);
            return null;
        }


        public new class Model : HashesContainer.Model
        {
            public new readonly Md5Format Data;

            public Model (Stream stream, string path) : base (path, 16)
            {
                base._data = Data = new Md5Format (this, stream, path);
                ParseHashes();
            }

            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                if (Data.Issues.HasFatal)
                    return;

                if ((validationFlags & Validations.MD5) != 0)
                    ComputeContentHashes (new Md5Hasher());

                base.CalcHashes (hashFlags, validationFlags);
            }
        }


        private Md5Format (Model model, Stream stream, string path) : base (model, stream, path)
         => Validation = Validations.MD5;
    }
}
