using System.Security.Cryptography;

namespace KaosCrypto
{
    public class Sha256Hasher : CryptoFullHasher
    {
        public Sha256Hasher() => hasher = new SHA256CryptoServiceProvider();
        public override string Name => "Sha256";
    }
}
