using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Serialization;

namespace MLAPI.CertificateGeneratorCommon
{
    public static class Generator
    {
        public static CertificateEmpire GenerateCertificateEmpire(RSAParameters issuerKeyPair, RSAParameters certificateKeyPair, string issuerName, string certificateName, DateTime validityStartTime, DateTime validityEndTime, byte[] certificateSerialNumber)
        {
            using (RSACryptoServiceProvider issuerRsa = new RSACryptoServiceProvider())
            {
                try
                {
                    issuerRsa.ImportParameters(issuerKeyPair);

                    using (RSACryptoServiceProvider certificateRsa = new RSACryptoServiceProvider())
                    {
                        try
                        {
                            certificateRsa.ImportParameters(certificateKeyPair);

                            Console.WriteLine("Generating issuer...");
                            CertificateRequest issuerRequest = new CertificateRequest("CN=" + issuerName, issuerRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                            issuerRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(issuerRequest.PublicKey, false));

                            Console.WriteLine("Generating issuer certificate....");
                            X509Certificate2 issuerCertificate = issuerRequest.CreateSelfSigned(validityStartTime, validityEndTime);

                            Console.WriteLine("Generating self signed certificate...");
                            CertificateRequest certificateRequest = new CertificateRequest("CN=" + certificateName, certificateRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                            certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));
                            certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection()
                            {
                                new Oid("1.3.6.1.5.5.7.3.8") // https://www.alvestrand.no/objectid/1.3.6.1.5.5.7.3.8.html
                            }, true));
                            certificateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false));

                            Console.WriteLine("Signing certificate...");
                            X509Certificate2 selfSignedCert = certificateRequest.Create(issuerCertificate, validityStartTime, validityEndTime, certificateSerialNumber);

                            return new CertificateEmpire()
                            {
                                issuerKeyPair = issuerKeyPair,
                                certificateKeyPair = certificateKeyPair,
                                issuerCertificate = issuerCertificate,
                                selfSignedCertificate = selfSignedCert
                            };
                        }
                        finally
                        {
                            certificateRsa.PersistKeyInCsp = false;
                        }
                    }
                }
                finally
                {
                    issuerRsa.PersistKeyInCsp = false;
                }
            }
        }
        
        public static RSAParameters GenerateKeyPair(int size)
        {
            using (RSACryptoServiceProvider csp = new RSACryptoServiceProvider(size))
            {
                try
                {
                    RSAParameters parameters = csp.ExportParameters(true);

                    Console.WriteLine("Successfully generated RSA key pair of length " + csp.KeySize);

                    return parameters;
                }
                finally
                {
                    csp.PersistKeyInCsp = false;
                }
            }
        }

        public static string GetMarkdownInstructions(CertificateEmpire empire)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("*ONLY USE SELF SIGNED CERTIFICATES INTERNALLY OR FOR TESTING. USE A SERVICE LIKE LETSENCRYPT FOR REAL CERTIFICATES. THIS PROGRAM WILL GENERATE A CERTIFICATE AUTHORITY KEY PAIR AND A CERTIFICATE SIGNED BY THAT AUTHORITY. CERTIFICATES ARE ONLY VALID FOR 30 DAYS. AFTER THAT TIME YOU NEED A NEW ISSUER AND CERTIFICATE.*");
            sb.AppendLine();
            sb.AppendLine("# Overview");
            sb.AppendLine();
            sb.AppendLine("## Server instructions");
            sb.AppendLine("**IMPORTANT:** *ONLY DO THIS STEP ON THE SERVER. IT SHOULD NOT BE DONE ON CLIENTS*");
            sb.AppendLine();
            sb.AppendLine("In the NetworkingManager.NetworkConfig ``ServerBase64PfxCertificate`` text field. Enter the following:");
            sb.AppendLine("```");
            sb.AppendLine(Convert.ToBase64String(empire.selfSignedCertificate.Export(X509ContentType.Pfx)));
            sb.AppendLine("```");
            sb.AppendLine("## Client instructions");
            sb.AppendLine("To make clients trust your certificate issuer. Please do the following before connecting:");
            sb.AppendLine("```csharp");
            sb.AppendLine("CryptographyHelper.OnValidateCertificateCallback = (certificate, hostname) =>");
            sb.AppendLine("{");
            sb.AppendLine("  X509Certificate2 issuerCertificate = new X509Certificate2(Convert.FromBase64String(\"" + Convert.ToBase64String(empire.issuerCertificate.Export(X509ContentType.Cert)) + "\"));");
            sb.AppendLine("  X509Chain verify = new X509Chain();");
            sb.AppendLine("  verify.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;");
            sb.AppendLine("  verify.ChainPolicy.ExtraStore.Add(issuerCertificate);");
            sb.AppendLine("  verify.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;");
            sb.AppendLine();
            sb.AppendLine("  // Check if the chain accepts it. This can mean that it's from a CA we trust OR our own CA.");
            sb.AppendLine("  bool isAcceptedByChain = verify.Build(new X509Certificate2(certificate));");
            sb.AppendLine();
            sb.AppendLine("  if (isAcceptedByChain)");
            sb.AppendLine("  {");
            sb.AppendLine("    // Validate with the last added CA, that's our CA");
            sb.AppendLine("    return verify.ChainElements[verify.ChainElements.Count - 1].Certificate.Thumbprint == issuerCertificate.Thumbprint;");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  return false;");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("# Advanced");
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------------------------|--------------|");
            sb.AppendLine("| Issuer Name | " + empire.issuerCertificate.IssuerName.Name + " |");
            sb.AppendLine("| Issuer Key Type | " + "RSA" + " |");
            sb.AppendLine("| Issuer Key Size | " + "4096" + " |");
            sb.AppendLine("| Issuer Validity Start | " + empire.issuerCertificate.NotBefore + " |");
            sb.AppendLine("| Issuer Validity End | " + empire.issuerCertificate.NotAfter + " |");
            sb.AppendLine("| Issuer Serial Number | " + empire.issuerCertificate.SerialNumber + " |");
            sb.AppendLine("| Issuer Thumbprint | " + empire.issuerCertificate.Thumbprint + " |");
            sb.AppendLine("| Certificate Name | " + empire.selfSignedCertificate.IssuerName.Name + " |");
            sb.AppendLine("| Certificate Serial Number | " + empire.selfSignedCertificate.SerialNumber + " |");
            sb.AppendLine("| Certificate Thumbprint | " + empire.selfSignedCertificate.Thumbprint + " |");
            sb.AppendLine("| Certificate Key Type | " + "RSA" + " |");
            sb.AppendLine("| Certificate Key Size | " + "4096" + " |");
            sb.AppendLine("| Certificate Validity Start | " + empire.selfSignedCertificate.NotBefore + " |");
            sb.AppendLine("| Certificate Validity End | " + empire.selfSignedCertificate.NotAfter + " |");
            sb.AppendLine();
            sb.AppendLine("## Keys");
            sb.AppendLine("These are the keys that were used");
            sb.AppendLine("### Issuer Public/Private Key");
            sb.AppendLine("```xml");
            sb.AppendLine(RSAParameterToXMLString(empire.issuerKeyPair));
            sb.AppendLine("```");
            sb.AppendLine("### Certificate Public/Private Key");
            sb.AppendLine("```xml");
            sb.AppendLine(RSAParameterToXMLString(empire.certificateKeyPair));
            sb.AppendLine("```");

            return sb.ToString();
        }

        public static string RSAParameterToXMLString(RSAParameters parameters)
        {
            using (StringWriter writer = new StringWriter())
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(RSAParameters));
                xmlSerializer.Serialize(writer, parameters);

                return writer.ToString();
            }
        }
    }
}