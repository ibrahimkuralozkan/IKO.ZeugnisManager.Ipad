using System.Text.Json.Serialization;

namespace ZeugnisManager.Admin.Models;

public enum LegacyStudentType { Normal, SE, Foerder }

public sealed class LegacyStudent
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string ClassName { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LegacyStudentType Type { get; set; }
    [JsonIgnore] public bool IsSe => Type == LegacyStudentType.SE;
    [JsonIgnore] public bool IsFoerder => Type == LegacyStudentType.Foerder;
}

public sealed class LegacyTeacher
{
    public string FullName { get; set; } = "";
    public string ClassName { get; set; } = "";
}

public sealed class LegacyZeugnis
{
    public Guid StudentId { get; set; }
    public string ClassName { get; set; } = "";
    public string SchoolYear { get; set; } = "";
    public int Halbjahr { get; set; } = 2;
    public Dictionary<string, Dictionary<string, int>> Bewertungen { get; set; } = new();
    public Dictionary<string, string> Texte { get; set; } = new();
    public Dictionary<string, string> Auswahlwerte { get; set; } = new();
}

public sealed class LegacySettings
{
    public string SchoolName { get; set; } = "";
    public string SchoolYear { get; set; } = "";
    public int HalfYear { get; set; } = 2;
}

public sealed class RealExchangeScope
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public List<string> RatingKeys { get; init; } = [];
    public List<string> TextKeys { get; init; } = [];
    public List<string> SelectionKeys { get; init; } = [];
    public bool IncludesNormalStudents { get; init; }
    public bool IncludesSeStudents { get; init; }
}

public sealed class RealPackageImportResult
{
    public Guid PackageId { get; init; }
    public string TeacherName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string ScopeLabel { get; init; } = "";
    public int UpdatedStudents { get; init; }
    public int UpdatedFields { get; init; }
}
