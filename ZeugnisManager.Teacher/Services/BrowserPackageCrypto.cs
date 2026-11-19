using System.Text.Json;
using IKO.ZeugnisManager.Core;
using Microsoft.JSInterop;

namespace ZeugnisManager.Teacher.Services;

public sealed class BrowserPackageCrypto(IJSRuntime js)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ExchangePackageDocument> DecryptAsync(byte[] packageBytes, string password)
    {
        ValidatePassword(password);
        try
        {
            var json = await js.InvokeAsync<string>("browserPackageCrypto.decrypt",
                Convert.ToBase64String(packageBytes), password);
            var document = JsonSerializer.Deserialize<ExchangePackageDocument>(json, JsonOptions)
                ?? throw new PackageSecurityException("Der Paketinhalt ist ungültig.");
            ValidateDocument(document);
            return document;
        }
        catch (JSException)
        {
            throw new PackageSecurityException("Das Kennwort ist falsch, das Paket wurde verändert oder wird nicht unterstützt.");
        }
    }

    public async Task<(byte[] Bytes, string FileName)> CreateResultAsync(ExchangePackageDocument assignment, string password)
    {
        ValidatePassword(password);
        if (assignment.Kind != ExchangePackageKind.Assignment)
            throw new PackageSecurityException("Nur ein Arbeitsauftrag kann als Rückgabepaket gespeichert werden.");

        var originalId = assignment.PackageId;
        var originalCreated = assignment.CreatedUtc;
        assignment.Kind = ExchangePackageKind.Result;
        assignment.SourcePackageId = originalId;
        assignment.PackageId = Guid.NewGuid();
        assignment.CreatedUtc = DateTimeOffset.UtcNow;
        try
        {
            ValidateDocument(assignment);
            var plaintextJson = JsonSerializer.Serialize(assignment, JsonOptions);
            var base64 = await js.InvokeAsync<string>("browserPackageCrypto.encrypt", plaintextJson, password);
            var fileName = $"Rueckgabe_{Safe(assignment.ClassName)}_{Safe(assignment.ScopeLabel)}_{Safe(assignment.TeacherName)}_{assignment.PackageId:N}.zmpkg";
            return (Convert.FromBase64String(base64), fileName);
        }
        catch (JSException)
        {
            throw new PackageSecurityException("Das verschlüsselte Rückgabepaket konnte in diesem Browser nicht erstellt werden.");
        }
        finally
        {
            assignment.PackageId = originalId;
            assignment.Kind = ExchangePackageKind.Assignment;
            assignment.SourcePackageId = null;
            assignment.CreatedUtc = originalCreated;
        }
    }

    public async Task<byte[]> EncryptAsync(ExchangePackageDocument document, string password)
    {
        ValidatePassword(password); ValidateDocument(document);
        try
        {
            var json = JsonSerializer.Serialize(document, JsonOptions);
            var base64 = await js.InvokeAsync<string>("browserPackageCrypto.encrypt", json, password);
            return Convert.FromBase64String(base64);
        }
        catch (JSException) { throw new PackageSecurityException("Das Paket konnte in diesem Browser nicht verschlüsselt werden."); }
    }

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
        if (document.ExpiresUtc < DateTimeOffset.UtcNow)
            throw new PackageSecurityException("Das Paket ist abgelaufen.");
    }

    private static string Safe(string value) => string.Concat(value.Select(c =>
        Path.GetInvalidFileNameChars().Contains(c) || char.IsWhiteSpace(c) ? '_' : c));
}
