using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

public class CertificateStore : IDisposable
{
    private const int SecretLength = 8;
    private const string LocalCertificatesStorageKey = "local-certificates";
    private const string RemoteCertificatesStorageKey = "remote-certificates";
    private ISecurePersistenceProvider persistenceProvider;
    private Dictionary<string, X509Certificate2> localCertificates = new Dictionary<string, X509Certificate2>();
    private Dictionary<string, X509Certificate2> remoteCertificates = new Dictionary<string, X509Certificate2>();

    public CertificateStore() : this(null)
    {
    }

    public CertificateStore(ISecurePersistenceProvider persistence)
    {
        persistenceProvider = persistence;
        LoadStoredCertificates();
    }

    public void ImportRemoteCertificate(string commonName, X509Certificate2 remoteCertificate)
    {
        remoteCertificates.Add(commonName, remoteCertificate);
    }

    public void Dispose()
    {
        StoreCertificates();
        persistenceProvider?.Dispose();
    }

    public void StoreCertificates()
    {
        if (persistenceProvider == null) return;
        var localCertificatesJson = CertificatesToJson(localCertificates);
        var remoteCertificatesJson = CertificatesToJson(remoteCertificates);
        persistenceProvider.StoreStringByKey(LocalCertificatesStorageKey, localCertificatesJson);
        persistenceProvider.StoreStringByKey(RemoteCertificatesStorageKey, remoteCertificatesJson);
    }

    internal X509Certificate2 GetOrGenerateLocalCertificate(string commonName)
    {
        var success = localCertificates.TryGetValue(commonName, out X509Certificate2? certificate);
        if (!success || certificate == null)
        {
            certificate = GenerateSelfSignedCertificate(commonName);
            localCertificates.Add(commonName, certificate);
        }
        return certificate;
    }
    internal X509Certificate2? GetRemoteCertificate(string commonName)
    {
        remoteCertificates.TryGetValue(commonName, out X509Certificate2? certificate);
        return certificate;
    }

    internal ReadOnlyMemory<byte> GenerateSecretBytes()
    {
        return new ReadOnlyMemory<byte>(RandomNumberGenerator.GetBytes(SecretLength));
    }

    private static X509Certificate2 GenerateSelfSignedCertificate(string commonName)
    {
        var ecdsa = ECDsa.Create(ECCurve.CreateFromValue("1.2.840.10045.3.1.7"));
        var name = new X500DistinguishedName($"C=DE,CN={commonName}");
        var request = new CertificateRequest(name, ecdsa, HashAlgorithmName.SHA256);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pkcs12));
    }

    private void LoadStoredCertificates()
    {
        if (persistenceProvider == null) return;
        // [ {"commonName": "...", "base64Certificate": "..." }, ... ]
        var localCertificatesJson = persistenceProvider.LoadStringByKey(LocalCertificatesStorageKey);
        var remoteCertificatesJson = persistenceProvider.LoadStringByKey(RemoteCertificatesStorageKey);
        ImportFromJson(localCertificatesJson, localCertificates);
        ImportFromJson(remoteCertificatesJson, remoteCertificates);
    }

    private static void ImportFromJson(string json, Dictionary<string, X509Certificate2> targetDictionary)
    {
        var certificateCollection = JsonSerializer.Deserialize<StoredCertificate[]>(json);
        if (certificateCollection != null)
        {
            foreach (var certificate in certificateCollection)
            {
                targetDictionary.AddCertificate(certificate);
            }
        }
    }

    private static string CertificatesToJson(Dictionary<string, X509Certificate2> certificates)
    {
        StoredCertificate[] localStoredCertificates = certificates.ToArray().Select(kvp =>
        {
            //does Pkcs12 export with privateKey?
            var encodedCertificate = Convert.ToBase64String(kvp.Value.Export(X509ContentType.Pkcs12));
            return new StoredCertificate(kvp.Key, encodedCertificate);
        }).ToArray();

        return JsonSerializer.Serialize(localStoredCertificates);
    }

}

internal record StoredCertificate(string CommonName, string Base64EncodedCertificate);

public interface ISecurePersistenceProvider : IDisposable
{
    string LoadStringByKey(string key);
    void StoreStringByKey(string key, string value);
}

internal static class CertificateExtensions
{
    internal static void AddCertificate(this Dictionary<string, X509Certificate2> dictionary, StoredCertificate certificate)
    {
        var certificateObject = new X509Certificate2(Convert.FromBase64String(certificate.Base64EncodedCertificate));
        if (certificateObject == null) throw new ArgumentException("Provided certificate is not valid");
        dictionary.Add(certificate.CommonName, certificateObject);
    }
}