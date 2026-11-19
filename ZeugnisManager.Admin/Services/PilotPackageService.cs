using System.Text.Json;
using IKO.ZeugnisManager.Core;

namespace ZeugnisManager.Admin.Services;

public sealed class PilotPackageService(IWebHostEnvironment environment)
{
    private readonly string registryPath = Path.Combine(environment.ContentRootPath, "AppData", "Pilot", "authorizations.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public (byte[] Bytes, string FileName, ExchangePackageDocument Document) Create(string teacherName, string password)
    {
        var document = new ExchangePackageDocument
        {
            TeacherName = teacherName.Trim(), ClassName = "Pilotklasse", ScopeId = "pilot:sport", ScopeLabel = "Sport – Pilottest",
            SchoolName = "Bodelschwinghschule", SchoolYear = "2026/2027", HalfYear = 1,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7), AllowedRatingKeys = ["Pilot_Sport"], AllowedTextKeys = ["Pilot_Bemerkung"]
        };
        document.Students.Add(CreateStudent("Test", "Schüler A"));
        document.Students.Add(CreateStudent("Test", "Schüler B"));
        var bytes = PackageCrypto.Encrypt(document, password);
        SaveAuthorization(document);
        return (bytes, $"Pilot_Sport_{document.PackageId:N}.zmpkg", document);
    }

    public ExchangePackageDocument Import(byte[] bytes, string password)
    {
        var result = PackageCrypto.Decrypt(bytes, password);
        if (result.Kind != ExchangePackageKind.Result || result.SourcePackageId is null)
            throw new PackageSecurityException("Bitte ein ausgefülltes Rückgabepaket auswählen.");
        var authorizations = LoadAuthorizations();
        var authorization = authorizations.FirstOrDefault(x => x.PackageId == result.SourcePackageId.Value)
            ?? throw new PackageSecurityException("Der ursprüngliche Pilot-Arbeitsauftrag wurde auf diesem Gerät nicht gefunden.");
        if (!string.Equals(authorization.TeacherName, result.TeacherName, StringComparison.Ordinal)
            || authorization.ExpiresUtc < DateTimeOffset.UtcNow)
            throw new PackageSecurityException("Das Rückgabepaket ist nicht berechtigt oder abgelaufen.");
        if (authorization.ImportedResultIds.Contains(result.PackageId))
            throw new PackageSecurityException("Dieses Rückgabepaket wurde bereits geprüft.");
        authorization.ImportedResultIds.Add(result.PackageId);
        WriteAuthorizations(authorizations);
        return result;
    }

    private static ExchangeStudentRecord CreateStudent(string firstName, string lastName)
    {
        var section = new ExchangeRatingSection
        {
            RatingKey = "Pilot_Sport", Title = "Sport",
            Criteria =
            [
                new() { Id = "01", Text = "beteiligte sich aktiv und zuverlässig." },
                new() { Id = "02", Text = "hielt vereinbarte Regeln ein." },
                new() { Id = "03", Text = "arbeitete fair mit anderen zusammen." }
            ]
        };
        return new ExchangeStudentRecord
        {
            StudentId = Guid.NewGuid(), FirstName = firstName, LastName = lastName, ClassName = "Pilotklasse",
            RatingSections = [section], Ratings = new() { ["Pilot_Sport"] = new() }, Texts = new() { ["Pilot_Bemerkung"] = "" }
        };
    }

    private void SaveAuthorization(ExchangePackageDocument document)
    {
        var authorizations = LoadAuthorizations();
        authorizations.Add(new PilotAuthorization { PackageId = document.PackageId, TeacherName = document.TeacherName, ExpiresUtc = document.ExpiresUtc });
        WriteAuthorizations(authorizations.TakeLast(100).ToList());
    }

    private List<PilotAuthorization> LoadAuthorizations()
    {
        try { return File.Exists(registryPath) ? JsonSerializer.Deserialize<List<PilotAuthorization>>(File.ReadAllText(registryPath)) ?? [] : []; }
        catch { return []; }
    }

    private void WriteAuthorizations(List<PilotAuthorization> authorizations)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(registryPath)!);
        var temporary = registryPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(authorizations, JsonOptions));
        File.Move(temporary, registryPath, true);
    }

    private sealed class PilotAuthorization
    {
        public Guid PackageId { get; set; }
        public string TeacherName { get; set; } = "";
        public DateTimeOffset ExpiresUtc { get; set; }
        public List<Guid> ImportedResultIds { get; set; } = [];
    }
}
