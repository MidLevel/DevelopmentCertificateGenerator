using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Serialization;
using MLAPI.CertificateGeneratorCommon;

namespace MLAPI.CertGenCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("MLAPI Certificate Generator. http://midlevel.io/");
            Console.WriteLine("================");
            Console.WriteLine("DISCLAIMER:");
            Console.WriteLine("ONLY USE SELF SIGNED CERTIFICATES INTERNALLY OR FOR TESTING.");
            Console.WriteLine("USE A SERVICE LIKE LETSENCRYPT FOR REAL CERTIFICATES.");
            Console.WriteLine("THIS PROGRAM WILL GENERATE A CERTIFICATE AUTHORITY KEY PAIR AND A CERTIFICATE SIGNED BY THAT AUTHORITY.");
            Console.WriteLine("THIS PROGRAM GENERATES 4096 BIT KEYS.");
            Console.WriteLine("CERTIFICATES ARE ONLY VALID FOR 30 DAYS. AFTER THAT TIME YOU NEED A NEW ISSUER AND CERTIFICATE.");
            Console.WriteLine("================");


            Console.Write("Please enter your ISSUER name (example: \"Albin Corén\"): ");
            string issuerName = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(issuerName))
            {
                Console.WriteLine("Empty ISSUER name. Defaulting to \"Unnamed Issuer\"");
                issuerName = "Unnamed Issuer";
            }

            Console.WriteLine("================");

            Console.Write("Please enter your CERTIFICATE name (example: \"MLAPI Development Certificate\"): ");
            string certificateName = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(certificateName))
            {
                Console.WriteLine("Empty CERTIFICATE name. Defaulting to \"Unnamed MLAPI Development Certificate\"");
                certificateName = "Unnamed MLAPI Development Certificate";
            }

            Console.WriteLine("================");

            Console.WriteLine("Generating issuer key pair...");
            RSAParameters issuerKeyPair = Generator.GenerateKeyPair(4096);

            Console.WriteLine("Generating certificate key pair...");
            RSAParameters certificateKeyPair = Generator.GenerateKeyPair(4096);

            Console.WriteLine("================");

            DateTime startTime = DateTime.UtcNow;
            DateTime endTime = DateTime.UtcNow.AddDays(30);

            byte[] serialNumber = new byte[20];

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                // Should ensure non negativity and make it smaller
                // See https://tools.ietf.org/html/rfc3280#section-4.1.2.2
                rng.GetBytes(serialNumber);
            }

            Console.WriteLine("Certificate validity start: " + startTime);
            Console.WriteLine("Certificate validity end: " + endTime);
            Console.WriteLine("Certificate serial number: " + BitConverter.ToString(serialNumber));
            Console.WriteLine("Certificate name: " + certificateName);
            Console.WriteLine("Certificate issuer name: " + issuerName);

            Console.WriteLine("================");

            Console.WriteLine("Generating certificates...");

            CertificateEmpire empire = Generator.GenerateCertificateEmpire(issuerKeyPair, certificateKeyPair, issuerName, certificateName, startTime, endTime, serialNumber);

            Console.WriteLine("================");

            Console.WriteLine("Writing to disk...");

            string outputPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Guid.NewGuid().ToString().Replace("-", ""));
            string rawOutputPath = Path.Combine(outputPath, "raw");

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            if (!Directory.Exists(rawOutputPath))
                Directory.CreateDirectory(rawOutputPath);

            WriteRsaKeyPairToFile(Path.Combine(rawOutputPath, "issuerKeyPair.xml"), empire.issuerKeyPair);
            WriteRsaKeyPairToFile(Path.Combine(rawOutputPath, "certificateKeyPair.xml"), empire.certificateKeyPair);


            WriteCertificateToFile(Path.Combine(rawOutputPath, "issuerCertificate.cert"), empire.issuerCertificate);
            WriteCertificateToFile(Path.Combine(rawOutputPath, "selfSignedCertificate.cert"), empire.selfSignedCertificate);


            WriteInstructions(empire, Path.Combine(outputPath, "quickstart.md"));

            Console.WriteLine("================");
            Console.WriteLine("Completed!");
            Console.WriteLine("");
            Console.WriteLine("To get started. Read the generated quickstart.md file located at \"" + Path.Combine(outputPath, "quickstart.md") + "\"");
            Console.WriteLine("For ADVANCED users, all the certificates and RSA key pairs have been exported to \"" + rawOutputPath + "\"");
        }

        private static void WriteInstructions(CertificateEmpire empire, string path)
        {
            using (FileStream fileStream = new FileStream(path, FileMode.CreateNew))
            {
                using (StreamWriter writer = new StreamWriter(fileStream))
                {
                    writer.Write(Generator.GetMarkdownInstructions(empire));
                }
            }
        }

        private static void WriteCertificateToFile(string path, X509Certificate2 cert)
        {
            byte[] pfxBytes = cert.Export(X509ContentType.Pfx);
            File.WriteAllBytes(path, pfxBytes);
        }

        private static void WriteRsaKeyPairToFile(string path, RSAParameters keyPair)
        {
            using (FileStream fileStream = new FileStream(path, FileMode.CreateNew))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(RSAParameters));
                xmlSerializer.Serialize(fileStream, keyPair);
            }
        }
    }
}