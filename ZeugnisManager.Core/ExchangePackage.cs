namespace IKO.ZeugnisManager.Core;

public enum ExchangePackageKind
{
    Assignment,
    Result
}

public sealed class ExchangePackageDocument
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public Guid PackageId { get; set; } = Guid.NewGuid();
    public ExchangePackageKind Kind { get; set; } = ExchangePackageKind.Assignment;
    public string SchoolName { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ScopeId { get; set; } = "";
    public string ScopeLabel { get; set; } = "";
    public string SchoolYear { get; set; } = "";
    public int HalfYear { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresUtc { get; set; }
    public Guid? SourcePackageId { get; set; }
    public List<string> AllowedRatingKeys { get; set; } = new();
    public List<string> AllowedTextKeys { get; set; } = new();
    public List<string> AllowedSelectionKeys { get; set; } = new();
    public List<ExchangeStudentRecord> Students { get; set; } = new();
}

public sealed class ExchangeStudentRecord
{
    public Guid StudentId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string StudentType { get; set; } = "Normal";
    public List<ExchangeRatingSection> RatingSections { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> Ratings { get; set; } = new();
    public Dictionary<string, string> Texts { get; set; } = new();
    public Dictionary<string, string> Selections { get; set; } = new();
}

public sealed class ExchangeRatingSection
{
    public string RatingKey { get; set; } = "";
    public string Title { get; set; } = "";
    public List<ExchangeCriterion> Criteria { get; set; } = new();
}

public sealed class ExchangeCriterion
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class EncryptedPackageEnvelope
{
    public string Format { get; set; } = PackageCrypto.EnvelopeFormat;
    public int Version { get; set; } = 1;
    public string Kdf { get; set; } = "PBKDF2-SHA256";
    public int Iterations { get; set; }
    public string Salt { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Ciphertext { get; set; } = "";
}

public sealed class PackageSecurityException : Exception
{
    public PackageSecurityException(string message) : base(message) { }
}
