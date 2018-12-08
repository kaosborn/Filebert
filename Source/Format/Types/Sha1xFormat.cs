using System.IO;
using System.Text;
using KaosCrypto;

namespace KaosFormat
{
    public class Sha1xFormat : HashesContainer
    {
        public static string[] SNames => new string[] { "sha1x" };
        public override string[] Names => SNames;

        public static Model CreateModel (Stream stream, byte[] hdr, string path)
        {
            if (path.ToLower().EndsWith(".sha1x"))
                return new Model (stream, path);
            return null;
        }


        public new class Model : HashesContainer.Model
        {
            public new readonly Sha1xFormat Data;

            public Model (Stream stream, string path) : base (path, 20)
            {
                base._data = Data = new Sha1xFormat (this, stream, path);
                ParseHashes();
            }

            public override void CalcHashes (Hashes hashFlags, Validations validationFlags)
            {
                if (Data.Issues.HasFatal)
                    return;

                if ((validationFlags & Validations.SHA1) != 0)
                    ComputeContentHashes (new Sha1Hasher(), Hashes.MediaSHA1);

                base.CalcHashes (hashFlags, validationFlags);
            }
        }


        private Sha1xFormat (Model model, Stream stream, string path) : base (model, stream, path, Encoding.UTF8)
         => Validation = Validations.SHA1;
    }
}
