using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IKO.ZeugnisManager.Core;

public static class PackageCrypto
{
    public const string EnvelopeFormat = "IKO.ZeugnisManager.ZMPKG";
    public const int DefaultIterations = 310_000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private static readonly byte[] AdditionalData = Encoding.UTF8.GetBytes(EnvelopeFormat + "|1");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static byte[] Encrypt(ExchangePackageDocument document, string password)
    {
        ValidateDocument(document);
        ValidatePassword(password);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(password, salt, DefaultIterations);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, AdditionalData);
            var envelope = new EncryptedPackageEnvelope
            {
                Iterations = DefaultIterations,
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertext)
            };
            return JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static ExchangePackageDocument Decrypt(ReadOnlySpan<byte> packageBytes, string password,
        DateTimeOffset? now = null)
    {
        ValidatePassword(password);
        try
        {
            var envelope = JsonSerializer.Deserialize<EncryptedPackageEnvelope>(packageBytes, JsonOptions)
                ?? throw new PackageSecurityException("Das Paketformat ist ungültig.");
            if (envelope.Format != EnvelopeFormat || envelope.Version != 1)
                throw new PackageSecurityException("Dieses Paketformat wird nicht unterstützt.");
            if (envelope.Iterations < 100_000 || envelope.Iterations > 2_000_000)
                throw new PackageSecurityException("Die Schlüsselableitung des Pakets ist ungültig.");

            var salt = Convert.FromBase64String(envelope.Salt);
            var nonce = Convert.FromBase64String(envelope.Nonce);
            var tag = Convert.FromBase64String(envelope.Tag);
            var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
            if (salt.Length != SaltSize || nonce.Length != NonceSize || tag.Length != TagSize)
                throw new PackageSecurityException("Die Sicherheitsparameter des Pakets sind ungültig.");

            var plaintext = new byte[ciphertext.Length];
            var key = DeriveKey(password, salt, envelope.Iterations);
            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, AdditionalData);
                var document = JsonSerializer.Deserialize<ExchangePackageDocument>(plaintext, JsonOptions)
                    ?? throw new PackageSecurityException("Der Paketinhalt ist ungültig.");
                ValidateDocument(document);
                if (document.ExpiresUtc < (now ?? DateTimeOffset.UtcNow))
                    throw new PackageSecurityException("Das Paket ist abgelaufen.");
                return document;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        catch (PackageSecurityException)
        {
            throw;
        }
        catch (CryptographicException)
        {
            throw new PackageSecurityException("Das Kennwort ist falsch oder das Paket wurde verändert.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        {
            throw new PackageSecurityException("Das Paket ist beschädigt oder hat ein unbekanntes Format.");
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, KeySize);

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new PackageSecurityException("Das Paketkennwort muss mindestens 12 Zeichen lang sein.");
    }

    private static void ValidateDocument(ExchangePackageDocument document)
    {
        if (document.FormatVersion != ExchangePackageDocument.CurrentFormatVersion)
            throw new PackageSecurityException("Die Paketversion wird nicht unterstützt.");
        if (document.PackageId == Guid.Empty || string.IsNullOrWhiteSpace(document.TeacherName)
            || string.IsNullOrWhiteSpace(document.ClassName) || string.IsNullOrWhiteSpace(document.ScopeId))
            throw new PackageSecurityException("Pflichtangaben des Pakets fehlen.");
        if (document.HalfYear is not (1 or 2) || document.ExpiresUtc <= document.CreatedUtc)
            throw new PackageSecurityException("Zeitraum oder Halbjahr des Pakets ist ungültig.");
    }
}
