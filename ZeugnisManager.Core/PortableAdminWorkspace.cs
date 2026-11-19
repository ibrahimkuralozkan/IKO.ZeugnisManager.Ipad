namespace IKO.ZeugnisManager.Core;

public sealed class PortableAdminWorkspace
{
    public const int CurrentFormatVersion = 1;
    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public Guid WorkspaceId { get; set; } = Guid.NewGuid();
    public int Revision { get; set; } = 1;
    public string SchoolName { get; set; } = "";
    public string SchoolYear { get; set; } = "";
    public int HalfYear { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Teachers { get; set; } = [];
    public List<PortableAssignmentTemplate> Templates { get; set; } = [];
    public List<PortableAuthorization> Authorizations { get; set; } = [];
    public List<Guid> ImportedResultIds { get; set; } = [];
    public List<PortableAuditEntry> Audit { get; set; } = [];
}

public sealed class PortableAssignmentTemplate
{
    public string ClassName { get; set; } = "";
    public string ScopeId { get; set; } = "";
    public string ScopeLabel { get; set; } = "";
    public List<string> AllowedRatingKeys { get; set; } = [];
    public List<string> AllowedTextKeys { get; set; } = [];
    public List<string> AllowedSelectionKeys { get; set; } = [];
    public List<ExchangeStudentRecord> Students { get; set; } = [];
}

public sealed class PortableAuthorization
{
    public Guid PackageId { get; set; }
    public string TeacherName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ScopeId { get; set; } = "";
    public DateTimeOffset ExpiresUtc { get; set; }
    public List<Guid> StudentIds { get; set; } = [];
    public List<string> AllowedRatingKeys { get; set; } = [];
    public List<string> AllowedTextKeys { get; set; } = [];
    public List<string> AllowedSelectionKeys { get; set; } = [];
}

public sealed class PortableAuditEntry
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Action { get; set; } = "";
    public Guid PackageId { get; set; }
    public string TeacherName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ScopeLabel { get; set; } = "";
}
