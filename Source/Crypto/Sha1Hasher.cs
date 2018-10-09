using System.Security.Cryptography;

namespace KaosCrypto
{
    public class Sha1Hasher : CryptoFullHasher
    {
        public Sha1Hasher() => hasher = new SHA1CryptoServiceProvider();
        public override string Name => "Sha1";
    }
}
