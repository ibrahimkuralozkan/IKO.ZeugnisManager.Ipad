using System.Text.Json;
using IKO.ZeugnisManager.Core;
using Microsoft.JSInterop;

namespace ZeugnisManager.Teacher.Services;

public sealed class BrowserWorkspaceService(IJSRuntime js, BrowserPackageCrypto packages)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public async Task<PortableAdminWorkspace> OpenAsync(byte[] bytes, string password)
    {
        ValidatePassword(password);
        try { var text = await js.InvokeAsync<string>("browserWorkspaceCrypto.decrypt", Convert.ToBase64String(bytes), password); var result = JsonSerializer.Deserialize<PortableAdminWorkspace>(text, Json) ?? throw new PackageSecurityException("Die Admin-Datei ist ungültig."); if (result.FormatVersion != PortableAdminWorkspace.CurrentFormatVersion || result.WorkspaceId == Guid.Empty || result.HalfYear is not (1 or 2)) throw new PackageSecurityException("Die Admin-Datei ist ungültig."); return result; }
        catch (JSException) { throw new PackageSecurityException("Das Kennwort ist falsch, die Admin-Datei wurde verändert oder wird nicht unterstützt."); }
    }
    public async Task<(byte[] Bytes, string FileName)> SaveAsync(PortableAdminWorkspace workspace, string password)
    {
        ValidatePassword(password); workspace.Revision++; workspace.UpdatedUtc = DateTimeOffset.UtcNow;
        try { var base64 = await js.InvokeAsync<string>("browserWorkspaceCrypto.encrypt", JsonSerializer.Serialize(workspace, Json), password); return (Convert.FromBase64String(base64), $"Admin-Arbeitsbestand_{workspace.SchoolYear.Replace('/', '-')}_H{workspace.HalfYear}_R{workspace.Revision}.zmadm"); }
        catch (JSException) { workspace.Revision--; throw new PackageSecurityException("Die Admin-Datei konnte nicht verschlüsselt gespeichert werden."); }
    }
    public async Task<(byte[] Bytes, string FileName)> CreateAssignmentAsync(PortableAdminWorkspace workspace, PortableAssignmentTemplate template, string teacher, DateTimeOffset expiresUtc, string password)
    {
        var students = JsonSerializer.Deserialize<List<ExchangeStudentRecord>>(JsonSerializer.Serialize(template.Students, Json), Json) ?? [];
        var document = new ExchangePackageDocument { SchoolName = workspace.SchoolName, SchoolYear = workspace.SchoolYear, HalfYear = workspace.HalfYear, TeacherName = teacher, ClassName = template.ClassName, ScopeId = template.ScopeId, ScopeLabel = template.ScopeLabel, ExpiresUtc = expiresUtc, AllowedRatingKeys = [.. template.AllowedRatingKeys], AllowedTextKeys = [.. template.AllowedTextKeys], AllowedSelectionKeys = [.. template.AllowedSelectionKeys], Students = students };
        var bytes = await packages.EncryptAsync(document, password);
        workspace.Authorizations.Add(new PortableAuthorization { PackageId = document.PackageId, TeacherName = teacher, ClassName = document.ClassName, ScopeId = document.ScopeId, ExpiresUtc = document.ExpiresUtc, StudentIds = students.Select(x => x.StudentId).ToList(), AllowedRatingKeys = [.. document.AllowedRatingKeys], AllowedTextKeys = [.. document.AllowedTextKeys], AllowedSelectionKeys = [.. document.AllowedSelectionKeys] });
        workspace.Audit.Add(new PortableAuditEntry { Action = "created", PackageId = document.PackageId, TeacherName = teacher, ClassName = document.ClassName, ScopeLabel = document.ScopeLabel });
        return (bytes, $"{Safe(document.ClassName)}_{Safe(document.ScopeLabel)}_{Safe(teacher)}_{document.PackageId:N}.zmpkg");
    }
    public async Task<(int Students, int Fields)> ImportResultAsync(PortableAdminWorkspace workspace, byte[] bytes, string password)
    {
        var document = await packages.DecryptAsync(bytes, password);
        if (document.Kind != ExchangePackageKind.Result || document.SourcePackageId is null) throw new PackageSecurityException("Es kann nur ein Rückgabepaket importiert werden.");
        if (workspace.ImportedResultIds.Contains(document.PackageId)) throw new PackageSecurityException("Dieses Rückgabepaket wurde bereits importiert.");
        var auth = workspace.Authorizations.FirstOrDefault(x => x.PackageId == document.SourcePackageId) ?? throw new PackageSecurityException("Der zugehörige Arbeitsauftrag fehlt in dieser Admin-Datei.");
        if (auth.ExpiresUtc < DateTimeOffset.UtcNow || auth.TeacherName != document.TeacherName || auth.ClassName != document.ClassName || auth.ScopeId != document.ScopeId) throw new PackageSecurityException("Das Rückgabepaket stimmt nicht mit dem Arbeitsauftrag überein.");
        var allowed = auth.StudentIds.ToHashSet();
        if (document.Students.Select(x => x.StudentId).Distinct().Count() != document.Students.Count || document.Students.Any(x => !allowed.Contains(x.StudentId))) throw new PackageSecurityException("Das Rückgabepaket enthält nicht freigegebene Schülerdatensätze.");
        var template = workspace.Templates.FirstOrDefault(x => x.ClassName == document.ClassName && x.ScopeId == document.ScopeId) ?? throw new PackageSecurityException("Der Bearbeitungsbereich fehlt in der Admin-Datei.");
        var students = 0; var fields = 0;
        foreach (var incoming in document.Students)
        {
            var target = template.Students.FirstOrDefault(x => x.StudentId == incoming.StudentId); if (target is null) continue;
            foreach (var key in auth.AllowedRatingKeys) if (incoming.Ratings.TryGetValue(key, out var values)) { target.Ratings[key] = new(values); fields += values.Count; }
            foreach (var key in auth.AllowedTextKeys) if (incoming.Texts.TryGetValue(key, out var value)) { target.Texts[key] = value; fields++; }
            foreach (var key in auth.AllowedSelectionKeys) if (incoming.Selections.TryGetValue(key, out var value)) { target.Selections[key] = value; fields++; }
            students++;
        }
        workspace.ImportedResultIds.Add(document.PackageId); workspace.Audit.Add(new PortableAuditEntry { Action = "imported", PackageId = document.PackageId, TeacherName = document.TeacherName, ClassName = document.ClassName, ScopeLabel = document.ScopeLabel });
        return (students, fields);
    }
    private static void ValidatePassword(string password) { if (string.IsNullOrWhiteSpace(password) || password.Length < 12) throw new PackageSecurityException("Das Admin-Kennwort muss mindestens 12 Zeichen lang sein."); }
    private static string Safe(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsWhiteSpace(c) ? '_' : c));
}
