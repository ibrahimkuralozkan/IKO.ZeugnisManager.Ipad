using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IKO.ZeugnisManager.Core;

public static class WorkspaceCrypto
{
    public const string EnvelopeFormat = "IKO.ZeugnisManager.ZMADM";
    private const int Iterations = 310_000;
    private static readonly byte[] AdditionalData = Encoding.UTF8.GetBytes(EnvelopeFormat + "|1");
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static byte[] Encrypt(PortableAdminWorkspace workspace, string password)
    {
        Validate(workspace, password);
        var plain = JsonSerializer.SerializeToUtf8Bytes(workspace, Json);
        var salt = RandomNumberGenerator.GetBytes(16); var nonce = RandomNumberGenerator.GetBytes(12);
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
        var cipher = new byte[plain.Length]; var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag, AdditionalData);
            return JsonSerializer.SerializeToUtf8Bytes(new EncryptedPackageEnvelope { Format = EnvelopeFormat, Iterations = Iterations, Salt = Convert.ToBase64String(salt), Nonce = Convert.ToBase64String(nonce), Tag = Convert.ToBase64String(tag), Ciphertext = Convert.ToBase64String(cipher) }, Json);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }

    public static PortableAdminWorkspace Decrypt(ReadOnlySpan<byte> bytes, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12) throw new PackageSecurityException("Das Admin-Kennwort muss mindestens 12 Zeichen lang sein.");
        try
        {
            var envelope = JsonSerializer.Deserialize<EncryptedPackageEnvelope>(bytes, Json) ?? throw new PackageSecurityException("Die Admin-Datei ist ungültig.");
            if (envelope.Format != EnvelopeFormat || envelope.Version != 1) throw new PackageSecurityException("Dieses Admin-Dateiformat wird nicht unterstützt.");
            var salt = Convert.FromBase64String(envelope.Salt); var nonce = Convert.FromBase64String(envelope.Nonce); var tag = Convert.FromBase64String(envelope.Tag); var cipher = Convert.FromBase64String(envelope.Ciphertext);
            if (salt.Length != 16 || nonce.Length != 12 || tag.Length != 16 || envelope.Iterations is < 100_000 or > 2_000_000) throw new PackageSecurityException("Die Sicherheitsparameter der Admin-Datei sind ungültig.");
            var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, envelope.Iterations, HashAlgorithmName.SHA256, 32); var plain = new byte[cipher.Length];
            try
            {
                using var aes = new AesGcm(key, 16); aes.Decrypt(nonce, cipher, tag, plain, AdditionalData);
                var workspace = JsonSerializer.Deserialize<PortableAdminWorkspace>(plain, Json) ?? throw new PackageSecurityException("Der Inhalt der Admin-Datei ist ungültig.");
                Validate(workspace, password); return workspace;
            }
            finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
        }
        catch (PackageSecurityException) { throw; }
        catch (CryptographicException) { throw new PackageSecurityException("Das Kennwort ist falsch oder die Admin-Datei wurde verändert."); }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException) { throw new PackageSecurityException("Die Admin-Datei ist beschädigt."); }
    }

    private static void Validate(PortableAdminWorkspace workspace, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12) throw new PackageSecurityException("Das Admin-Kennwort muss mindestens 12 Zeichen lang sein.");
        if (workspace.FormatVersion != PortableAdminWorkspace.CurrentFormatVersion || workspace.WorkspaceId == Guid.Empty || workspace.Revision < 1 || workspace.HalfYear is not (1 or 2)) throw new PackageSecurityException("Pflichtangaben der Admin-Datei fehlen.");
    }
}
