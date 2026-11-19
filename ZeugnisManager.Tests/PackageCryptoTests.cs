using IKO.ZeugnisManager.Core;

namespace ZeugnisManager.Tests;

public sealed class PackageCryptoTests
{
    private const string Password = "Schule-Test-2026!";

    [Fact]
    public void RoundTrip_PreservesPackageAndStudentData()
    {
        var source = CreatePackage();
        var restored = PackageCrypto.Decrypt(PackageCrypto.Encrypt(source, Password), Password);
        Assert.Equal(source.PackageId, restored.PackageId);
        Assert.Equal("Sport", restored.ScopeLabel);
        Assert.Single(restored.Students);
    }

    [Fact]
    public void WrongPassword_IsRejected() => Assert.Throws<PackageSecurityException>(() => PackageCrypto.Decrypt(PackageCrypto.Encrypt(CreatePackage(), Password), "Falsches-Passwort!"));

    [Fact]
    public void ModifiedPackage_IsRejected()
    {
        var bytes = PackageCrypto.Encrypt(CreatePackage(), Password);
        bytes[^10] ^= 0x01;
        Assert.Throws<PackageSecurityException>(() => PackageCrypto.Decrypt(bytes, Password));
    }

    private static ExchangePackageDocument CreatePackage() => new()
    {
        TeacherName = "Testlehrkraft", ClassName = "4a", ScopeId = "sport", ScopeLabel = "Sport", SchoolYear = "2026/2027", HalfYear = 1,
        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7), Students = { new ExchangeStudentRecord { StudentId = Guid.NewGuid(), FirstName = "Test", LastName = "Person", ClassName = "4a" } }
    };
}
