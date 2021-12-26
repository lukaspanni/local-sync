using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

internal class Keystore
{
    private static X509Certificate2? acceptedCertificate;
    private static Dictionary<string, X509Certificate2> localCertificates = new Dictionary<string, X509Certificate2>();

    public static string AcceptedCertificateHost => acceptedCertificate?.GetNameInfo(X509NameType.SimpleName, false) ?? "";
    public static void SetAcceptedRemoteCertificate(string base64EncodedCertificate)
    {
        SetAcceptedRemoteCertificate(new X509Certificate2(Convert.FromBase64String(base64EncodedCertificate)));
    }

    public static void SetAcceptedRemoteCertificate(X509Certificate2 certificate)
    {
        if (acceptedCertificate != null) throw new InvalidOperationException("A certificate has already been set");
        if (certificate == null || certificate.HasPrivateKey) throw new ArgumentException("Provided certificate cannot be used for this operation");
        acceptedCertificate = certificate;
    }

    public static X509Certificate2 GetCertificateByCommonName(string commonName)
    {
        var success = localCertificates.TryGetValue(commonName, out X509Certificate2? certificate);
        if (!success || certificate == null)
        {
            certificate = GenerateSelfSignedCertificate(commonName);
            localCertificates.Add(commonName, certificate);
        }
        return certificate;
    }

    private static X509Certificate2 GenerateSelfSignedCertificate(string commonName)
    {
        var ecdsa = ECDsa.Create(ECCurve.CreateFromValue("1.2.840.10045.3.1.7"));
        var name = new X500DistinguishedName($"C=DE,CN={commonName}");
        var request = new CertificateRequest(name, ecdsa, HashAlgorithmName.SHA256);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pkcs12));
    }

    public static bool ValidateServerCertificate(
       object sender,
       X509Certificate certificate,
       X509Chain chain,
       SslPolicyErrors sslPolicyErrors)
    {
        if (acceptedCertificate != null)
            return acceptedCertificate.Equals(certificate);
        return false;
    }


    private static bool pairing = true;
    public static bool ValidateClientCertificate(
      object sender,
      X509Certificate certificate,
      X509Chain chain,
      SslPolicyErrors sslPolicyErrors)
    {
        if (pairing) return true;
        if (acceptedCertificate != null)
            return acceptedCertificate.Equals(certificate);
        return false;
    }
}

