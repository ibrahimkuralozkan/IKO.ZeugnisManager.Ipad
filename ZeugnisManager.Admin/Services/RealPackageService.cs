using System.Text.Json;
using IKO.ZeugnisManager.Core;
using ZeugnisManager.Admin.Models;

namespace ZeugnisManager.Admin.Services;

public sealed class RealPackageService(LegacyDataSourceService dataSource, LegacyCatalogService catalog,
    IWebHostEnvironment environment)
{
    private readonly string authorizationDir = Path.Combine(environment.ContentRootPath, "AppData", "Exchange", "Authorizations");
    private readonly string auditPath = Path.Combine(environment.ContentRootPath, "AppData", "Exchange", "audit.json");
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    private string Root => dataSource.GetConfiguredPath();
    public IReadOnlyList<string> GetClasses() => Directory.Exists(Path.Combine(Root, "AppData", "Students"))
        ? Directory.GetFiles(Path.Combine(Root, "AppData", "Students"), "*.json").Select(Path.GetFileNameWithoutExtension).Where(x => x is not null).Cast<string>().Order().ToList() : [];

    public IReadOnlyList<string> GetTeachers() => Load<List<LegacyTeacher>>(Path.Combine(Root, "AppData", "Teachers", "teachers.json"))
        .Select(x => x.FullName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order().ToList();

    public IReadOnlyList<RealExchangeScope> GetScopes(string className)
    {
        var settings = GetSettings();
        var level = LegacyCatalogService.Klassenstufe(className);
        var types = GetStudents(className).Where(s => !s.IsFoerder).Select(s => s.IsSe).Distinct().ToList();
        return types.SelectMany(isSe => BuildScopes(LegacyCatalogService.GetLayout(level, isSe, settings.HalfYear), isSe))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RealExchangeScope
            {
                Id = g.Key, Label = g.First().Label,
                RatingKeys = g.SelectMany(x => x.RatingKeys).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                TextKeys = g.SelectMany(x => x.TextKeys).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                SelectionKeys = g.SelectMany(x => x.SelectionKeys).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                IncludesNormalStudents = g.Any(x => x.IncludesNormalStudents),
                IncludesSeStudents = g.Any(x => x.IncludesSeStudents)
            }).OrderBy(x => x.Label).ToList();
    }

    public (byte[] Bytes, string FileName, ExchangePackageDocument Document) CreateAssignment(
        string className, string teacherName, RealExchangeScope scope, string password, DateTimeOffset expiresUtc)
    {
        var document = BuildAssignmentDocument(className, teacherName, scope, expiresUtc);
        var bytes = PackageCrypto.Encrypt(document, password);
        SaveAuthorization(document);
        AppendAudit("created", document, 0, 0);
        return (bytes, $"{Safe(className)}_{Safe(scope.Label)}_{Safe(teacherName)}_{document.PackageId:N}.zmpkg", document);
    }

    public ExchangePackageDocument BuildAssignmentDocument(string className, string teacherName, RealExchangeScope scope, DateTimeOffset expiresUtc)
    {
        var settings = GetSettings();
        var document = new ExchangePackageDocument
        {
            SchoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "Bodelschwinghschule" : settings.SchoolName,
            TeacherName = teacherName.Trim(), ClassName = className, ScopeId = scope.Id, ScopeLabel = scope.Label,
            SchoolYear = settings.SchoolYear, HalfYear = settings.HalfYear, ExpiresUtc = expiresUtc.ToUniversalTime(),
            AllowedRatingKeys = scope.RatingKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AllowedTextKeys = scope.TextKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AllowedSelectionKeys = scope.SelectionKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
        foreach (var student in GetStudents(className).Where(s => !s.IsFoerder &&
                     (s.IsSe ? scope.IncludesSeStudents : scope.IncludesNormalStudents)))
        {
            var source = LoadZeugnis(student.Id);
            document.Students.Add(new ExchangeStudentRecord
            {
                StudentId = student.Id, FirstName = student.FirstName, LastName = student.LastName,
                ClassName = student.ClassName, StudentType = student.Type.ToString(),
                RatingSections = BuildRatingSections(student, document.AllowedRatingKeys, settings.HalfYear),
                Ratings = FilterRatings(source.Bewertungen, document.AllowedRatingKeys),
                Texts = Filter(source.Texte, document.AllowedTextKeys),
                Selections = Filter(source.Auswahlwerte, document.AllowedSelectionKeys)
            });
        }
        if (document.Students.Count == 0) throw new InvalidOperationException("Für diese Klasse wurden keine Schüler gefunden.");
        return document;
    }

    public PortableAdminWorkspace CreateWorkspace()
    {
        var settings = GetSettings();
        var workspace = new PortableAdminWorkspace { SchoolName = settings.SchoolName, SchoolYear = settings.SchoolYear, HalfYear = settings.HalfYear, Teachers = GetTeachers().ToList() };
        foreach (var className in GetClasses())
        foreach (var scope in GetScopes(className))
        {
            var document = BuildAssignmentDocument(className, "Vorlage", scope, DateTimeOffset.UtcNow.AddYears(1));
            workspace.Templates.Add(new PortableAssignmentTemplate { ClassName = className, ScopeId = scope.Id, ScopeLabel = scope.Label, AllowedRatingKeys = document.AllowedRatingKeys, AllowedTextKeys = document.AllowedTextKeys, AllowedSelectionKeys = document.AllowedSelectionKeys, Students = document.Students });
        }
        return workspace;
    }

    public (int Students, int Fields) ApplyWorkspace(PortableAdminWorkspace workspace)
    {
        var settings = GetSettings();
        if (workspace.SchoolYear != settings.SchoolYear || workspace.HalfYear != settings.HalfYear) throw new PackageSecurityException("Schuljahr oder Halbjahr der Admin-Datei stimmt nicht mit der Datenquelle überein.");
        var changedStudents = new HashSet<Guid>(); var fields = 0;
        foreach (var incoming in workspace.Templates.SelectMany(x => x.Students))
        {
            var target = LoadZeugnis(incoming.StudentId);
            foreach (var pair in incoming.Ratings) { target.Bewertungen[pair.Key] = new(pair.Value); fields += pair.Value.Count; }
            fields += Merge(target.Texte, incoming.Texts, incoming.Texts.Keys);
            fields += Merge(target.Auswahlwerte, incoming.Selections, incoming.Selections.Keys);
            target.StudentId = incoming.StudentId; target.ClassName = incoming.ClassName; target.SchoolYear = workspace.SchoolYear; target.Halbjahr = workspace.HalfYear;
            SaveZeugnis(target); changedStudents.Add(incoming.StudentId);
        }
        return (changedStudents.Count, fields);
    }

    public ExchangePackageDocument Inspect(byte[] bytes, string password) => PackageCrypto.Decrypt(bytes, password);

    public RealPackageImportResult ImportResult(byte[] bytes, string password)
    {
        var document = PackageCrypto.Decrypt(bytes, password);
        if (document.Kind != ExchangePackageKind.Result || document.SourcePackageId is null)
            throw new PackageSecurityException("Es kann nur ein Rückgabepaket importiert werden.");
        if (IsImported(document.PackageId)) throw new PackageSecurityException("Dieses Rückgabepaket wurde bereits importiert.");
        var authorization = LoadAuthorization(document.SourcePackageId.Value)
            ?? throw new PackageSecurityException("Der ursprüngliche Arbeitsauftrag wurde auf diesem Verwaltungsgerät nicht gefunden.");
        if (authorization.ExpiresUtc < DateTimeOffset.UtcNow) throw new PackageSecurityException("Die Paketberechtigung ist abgelaufen.");
        if (document.TeacherName != authorization.TeacherName || !document.ClassName.Equals(authorization.ClassName, StringComparison.OrdinalIgnoreCase)
            || !document.ScopeId.Equals(authorization.ScopeId, StringComparison.OrdinalIgnoreCase)
            || document.SchoolYear != authorization.SchoolYear || document.HalfYear != authorization.HalfYear)
            throw new PackageSecurityException("Das Rückgabepaket stimmt nicht mit dem Arbeitsauftrag überein.");
        var settings = GetSettings();
        if (document.SchoolYear != settings.SchoolYear || document.HalfYear != settings.HalfYear)
            throw new PackageSecurityException("Schuljahr oder Halbjahr stimmt nicht mit der Datenquelle überein.");

        var allowedStudents = authorization.StudentIds.ToHashSet();
        if (document.Students.Select(x => x.StudentId).Distinct().Count() != document.Students.Count)
            throw new PackageSecurityException("Das Rückgabepaket enthält doppelte Schülerdatensätze.");
        if (document.Students.Any(x => !allowedStudents.Contains(x.StudentId)))
            throw new PackageSecurityException("Das Rückgabepaket enthält nicht freigegebene Schülerdatensätze.");
        var updatedStudents = 0; var updatedFields = 0;
        foreach (var incoming in document.Students.Where(x => allowedStudents.Contains(x.StudentId)))
        {
            var target = LoadZeugnis(incoming.StudentId); var before = updatedFields;
            foreach (var key in authorization.AllowedRatingKeys)
                if (incoming.Ratings.TryGetValue(key, out var values)) { target.Bewertungen[key] = new(values); updatedFields += values.Count; }
            updatedFields += Merge(target.Texte, incoming.Texts, authorization.AllowedTextKeys);
            updatedFields += Merge(target.Auswahlwerte, incoming.Selections, authorization.AllowedSelectionKeys);
            if (updatedFields == before) continue;
            target.StudentId = incoming.StudentId; target.ClassName = document.ClassName; target.SchoolYear = document.SchoolYear; target.Halbjahr = document.HalfYear;
            SaveZeugnis(target); updatedStudents++;
        }
        AppendAudit("imported", document, updatedStudents, updatedFields);
        return new() { PackageId = document.PackageId, TeacherName = document.TeacherName, ClassName = document.ClassName, ScopeLabel = document.ScopeLabel, UpdatedStudents = updatedStudents, UpdatedFields = updatedFields };
    }

    private IEnumerable<RealExchangeScope> BuildScopes(IEnumerable<SubjectGroup> layout, bool isSe)
    {
        foreach (var group in layout)
        {
            if (group.NotesGrid) { foreach (var note in group.Notes) yield return new() { Id = "selection:" + note.AwKey, Label = note.Label, SelectionKeys = [note.AwKey], IncludesNormalStudents = !isSe, IncludesSeStudents = isSe }; continue; }
            var scope = new RealExchangeScope
            {
                Id = "group:" + new string(group.Title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()), Label = group.Title,
                RatingKeys = group.Bereiche.Select(x => x.CatalogKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                TextKeys = string.IsNullOrWhiteSpace(group.TextTag) ? [] : [group.TextTag],
                SelectionKeys = group.Notes.Select(x => x.AwKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                IncludesNormalStudents = !isSe,
                IncludesSeStudents = isSe
            };
            if (scope.RatingKeys.Count + scope.TextKeys.Count + scope.SelectionKeys.Count > 0) yield return scope;
        }
    }

    private List<ExchangeRatingSection> BuildRatingSections(LegacyStudent student, IEnumerable<string> allowedKeys, int halfYear)
    {
        var allowed = allowedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase); var result = new List<ExchangeRatingSection>();
        var level = LegacyCatalogService.Klassenstufe(student.ClassName); var itemsByKey = catalog.Load(level, student.IsSe, halfYear);
        foreach (var group in LegacyCatalogService.GetLayout(level, student.IsSe, halfYear))
        foreach (var area in group.Bereiche.Where(x => allowed.Contains(x.CatalogKey)))
        {
            if (!itemsByKey.TryGetValue(area.CatalogKey, out var items)) continue;
            var selected = area.ItemIds.Count == 0 ? items : items.Where(x => area.ItemIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();
            result.Add(new() { RatingKey = area.CatalogKey, Title = string.IsNullOrWhiteSpace(area.SubTitle) ? group.Title : area.SubTitle, Criteria = selected.Select(x => new ExchangeCriterion { Id = x.Id, Text = x.Text }).ToList() });
        }
        return result;
    }

    private List<LegacyStudent> GetStudents(string className) => Load<List<LegacyStudent>>(Path.Combine(Root, "AppData", "Students", className + ".json"));
    private LegacySettings GetSettings() => Load<LegacySettings>(Path.Combine(Root, "AppData", "settings.json"));
    private LegacyZeugnis LoadZeugnis(Guid id) => Load<LegacyZeugnis>(Path.Combine(Root, "AppData", "Zeugnisse", id + ".json"), new LegacyZeugnis { StudentId = id });
    private void SaveZeugnis(LegacyZeugnis value) => AtomicJsonFile.Write(Path.Combine(Root, "AppData", "Zeugnisse", value.StudentId + ".json"), JsonSerializer.Serialize(value, Json));
    private static T Load<T>(string path, T? fallback = default) where T : class => File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ?? fallback ?? Activator.CreateInstance<T>() : fallback ?? Activator.CreateInstance<T>();
    private static Dictionary<string, Dictionary<string, int>> FilterRatings(Dictionary<string, Dictionary<string, int>> source, IEnumerable<string> keys) => keys.Where(source.ContainsKey).ToDictionary(x => x, x => new Dictionary<string, int>(source[x]), StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> Filter(Dictionary<string, string> source, IEnumerable<string> keys) => keys.Where(source.ContainsKey).ToDictionary(x => x, x => source[x], StringComparer.OrdinalIgnoreCase);
    private static int Merge(Dictionary<string, string> target, Dictionary<string, string> source, IEnumerable<string> keys) { var count = 0; foreach (var key in keys) if (source.TryGetValue(key, out var value)) { target[key] = value; count++; } return count; }

    private void SaveAuthorization(ExchangePackageDocument doc) => AtomicJsonFile.Write(Path.Combine(authorizationDir, doc.PackageId.ToString("N") + ".json"), JsonSerializer.Serialize(new Authorization
    { PackageId = doc.PackageId, TeacherName = doc.TeacherName, ClassName = doc.ClassName, ScopeId = doc.ScopeId, SchoolYear = doc.SchoolYear, HalfYear = doc.HalfYear, ExpiresUtc = doc.ExpiresUtc, StudentIds = doc.Students.Select(x => x.StudentId).ToList(), AllowedRatingKeys = doc.AllowedRatingKeys, AllowedTextKeys = doc.AllowedTextKeys, AllowedSelectionKeys = doc.AllowedSelectionKeys }, Json));
    private Authorization? LoadAuthorization(Guid id) { var path = Path.Combine(authorizationDir, id.ToString("N") + ".json"); return File.Exists(path) ? JsonSerializer.Deserialize<Authorization>(File.ReadAllText(path), Json) : null; }
    private bool IsImported(Guid id) => Load<List<Audit>>(auditPath).Any(x => x.Action == "imported" && x.PackageId == id);
    private void AppendAudit(string action, ExchangePackageDocument doc, int students, int fields) { var entries = Load<List<Audit>>(auditPath); entries.Add(new() { TimestampUtc = DateTimeOffset.UtcNow, Action = action, PackageId = doc.PackageId, SourcePackageId = doc.SourcePackageId, TeacherName = doc.TeacherName, ClassName = doc.ClassName, ScopeLabel = doc.ScopeLabel, UpdatedStudents = students, UpdatedFields = fields }); AtomicJsonFile.Write(auditPath, JsonSerializer.Serialize(entries.TakeLast(2000), Json)); }
    private static string Safe(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsWhiteSpace(c) ? '_' : c));

    private sealed class Authorization { public Guid PackageId { get; set; } public string TeacherName { get; set; } = ""; public string ClassName { get; set; } = ""; public string ScopeId { get; set; } = ""; public string SchoolYear { get; set; } = ""; public int HalfYear { get; set; } public DateTimeOffset ExpiresUtc { get; set; } public List<Guid> StudentIds { get; set; } = []; public List<string> AllowedRatingKeys { get; set; } = []; public List<string> AllowedTextKeys { get; set; } = []; public List<string> AllowedSelectionKeys { get; set; } = []; }
    private sealed class Audit { public DateTimeOffset TimestampUtc { get; set; } public string Action { get; set; } = ""; public Guid PackageId { get; set; } public Guid? SourcePackageId { get; set; } public string TeacherName { get; set; } = ""; public string ClassName { get; set; } = ""; public string ScopeLabel { get; set; } = ""; public int UpdatedStudents { get; set; } public int UpdatedFields { get; set; } }
}
