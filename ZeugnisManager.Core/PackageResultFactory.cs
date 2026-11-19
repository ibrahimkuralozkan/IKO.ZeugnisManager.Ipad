namespace IKO.ZeugnisManager.Core;

public static class PackageResultFactory
{
    public static (byte[] Bytes, string FileName) Create(ExchangePackageDocument assignment, string password)
    {
        if (assignment.Kind != ExchangePackageKind.Assignment)
            throw new PackageSecurityException("Nur ein Arbeitsauftrag kann als Rückgabepaket gespeichert werden.");
        var sourceId = assignment.PackageId;
        var originalCreatedUtc = assignment.CreatedUtc;
        assignment.Kind = ExchangePackageKind.Result;
        assignment.SourcePackageId = sourceId;
        assignment.PackageId = Guid.NewGuid();
        assignment.CreatedUtc = DateTimeOffset.UtcNow;
        try
        {
            var bytes = PackageCrypto.Encrypt(assignment, password);
            var fileName = $"Rueckgabe_{Safe(assignment.ClassName)}_{Safe(assignment.ScopeLabel)}_{Safe(assignment.TeacherName)}_{assignment.PackageId:N}.zmpkg";
            return (bytes, fileName);
        }
        finally
        {
            assignment.PackageId = sourceId;
            assignment.Kind = ExchangePackageKind.Assignment;
            assignment.SourcePackageId = null;
            assignment.CreatedUtc = originalCreatedUtc;
        }
    }

    private static string Safe(string value) => string.Concat(value.Select(c =>
        Path.GetInvalidFileNameChars().Contains(c) || char.IsWhiteSpace(c) ? '_' : c));
}
