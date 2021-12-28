using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

public class CertificateStore
{
    private const int SecretLength = 8;
    private ISecurePersistenceProvider persistenceProvider;
    private Dictionary<string, X509Certificate2> localCertificates = new Dictionary<string, X509Certificate2>();
    private Dictionary<string, X509Certificate2> remoteCertificates = new Dictionary<string, X509Certificate2>();

    internal CertificateStore() : this(null)
    {

    }

    internal CertificateStore(ISecurePersistenceProvider persistence)
    {
        persistenceProvider = persistence;
    }

    public X509Certificate2 GetOrGenerateLocalCertificate(string commonName)
    {
        var success = localCertificates.TryGetValue(commonName, out X509Certificate2? certificate);
        if (!success || certificate == null)
        {
            certificate = GenerateSelfSignedCertificate(commonName);
            localCertificates.Add(commonName, certificate);
        }
        return certificate;
    }
    public X509Certificate2? GetRemoteCertificate(string commonName)
    {
        localCertificates.TryGetValue(commonName, out X509Certificate2? certificate);
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

    internal ReadOnlyMemory<byte> GenerateSecretBytes()
    {
        return new ReadOnlyMemory<byte>(RandomNumberGenerator.GetBytes(SecretLength));
    }
}

public interface ISecurePersistenceProvider
{

}