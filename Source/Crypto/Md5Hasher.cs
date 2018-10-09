using System.Security.Cryptography;

namespace KaosCrypto
{
    public class Md5Hasher : CryptoFullHasher
    {
        public Md5Hasher() => hasher = new MD5CryptoServiceProvider();
        public override string Name => "Md5";
    }
}
