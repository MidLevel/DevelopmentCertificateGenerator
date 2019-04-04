using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MLAPI.CertificateGeneratorCommon
{
    public class CertificateEmpire
    {
        public RSAParameters issuerKeyPair;
        public RSAParameters certificateKeyPair;
        public X509Certificate2 issuerCertificate;
        public X509Certificate2 selfSignedCertificate;
        public int issuerKeySize;
        public int certificateKeySize;
    }
}