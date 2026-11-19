using System.Text.Json;

namespace ZeugnisManager.Admin.Services;

public sealed class LegacyDataSourceService(IWebHostEnvironment environment)
{
    private readonly string configPath = Path.Combine(environment.ContentRootPath, "AppData", "Admin", "source.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public string SuggestedPath => @"C:\Users\b230650\source\repos\ibrahimkuralozkan\IKO.ZeugnisManager.Web";

    public string GetConfiguredPath()
    {
        try
        {
            if (!File.Exists(configPath)) return SuggestedPath;
            return JsonSerializer.Deserialize<SourceConfiguration>(File.ReadAllText(configPath), JsonOptions)?.RootPath ?? SuggestedPath;
        }
        catch { return SuggestedPath; }
    }

    public DataSourceSnapshot Inspect(string rootPath, bool saveConfiguration)
    {
        var fullPath = Path.GetFullPath(rootPath.Trim());
        var appData = Path.Combine(fullPath, "AppData");
        var studentsDir = Path.Combine(appData, "Students");
        var catalogsDir = Path.Combine(appData, "Kataloge");
        if (!Directory.Exists(studentsDir))
            throw new DirectoryNotFoundException("AppData/Students klasörü bulunamadı.");
        if (!Directory.Exists(catalogsDir))
            throw new DirectoryNotFoundException("AppData/Kataloge klasörü bulunamadı.");

        var snapshot = new DataSourceSnapshot { RootPath = fullPath };
        foreach (var file in Directory.GetFiles(studentsDir, "*.json").OrderBy(Path.GetFileNameWithoutExtension))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Array) continue;
            var item = new ClassSummary { ClassName = Path.GetFileNameWithoutExtension(file), StudentCount = document.RootElement.GetArrayLength() };
            foreach (var student in document.RootElement.EnumerateArray())
            {
                var type = student.TryGetProperty("Type", out var typeElement) ? typeElement.ToString() : "Normal";
                if (type.Equals("SE", StringComparison.OrdinalIgnoreCase)) item.SeCount++;
                else if (type.Equals("Foerder", StringComparison.OrdinalIgnoreCase)) item.FoerderCount++;
                else item.NormalCount++;
            }
            snapshot.Classes.Add(item);
        }

        var settingsPath = Path.Combine(appData, "settings.json");
        if (File.Exists(settingsPath))
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (settings.RootElement.TryGetProperty("SchoolYear", out var year)) snapshot.SchoolYear = year.GetString() ?? "";
            if (settings.RootElement.TryGetProperty("HalfYear", out var half) && half.TryGetInt32(out var halfYear)) snapshot.HalfYear = halfYear;
        }

        var teacherPath = Path.Combine(appData, "Teachers", "teachers.json");
        if (File.Exists(teacherPath))
        {
            using var teachers = JsonDocument.Parse(File.ReadAllText(teacherPath));
            if (teachers.RootElement.ValueKind == JsonValueKind.Array) snapshot.TeacherCount = teachers.RootElement.GetArrayLength();
        }
        snapshot.CatalogCount = Directory.GetFiles(catalogsDir, "*.json").Length;

        if (saveConfiguration)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var temporary = configPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new SourceConfiguration { RootPath = fullPath }, JsonOptions));
            File.Move(temporary, configPath, true);
        }
        return snapshot;
    }

    private sealed class SourceConfiguration { public string RootPath { get; set; } = ""; }
}

public sealed class DataSourceSnapshot
{
    public string RootPath { get; set; } = "";
    public string SchoolYear { get; set; } = "";
    public int HalfYear { get; set; }
    public int TeacherCount { get; set; }
    public int CatalogCount { get; set; }
    public List<ClassSummary> Classes { get; set; } = [];
}

public sealed class ClassSummary
{
    public string ClassName { get; set; } = "";
    public int StudentCount { get; set; }
    public int NormalCount { get; set; }
    public int SeCount { get; set; }
    public int FoerderCount { get; set; }
}
