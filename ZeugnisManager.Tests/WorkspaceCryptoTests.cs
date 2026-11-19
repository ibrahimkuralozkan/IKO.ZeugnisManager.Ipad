using IKO.ZeugnisManager.Core;

namespace ZeugnisManager.Tests;

public sealed class WorkspaceCryptoTests
{
    private const string Password = "Admin-Test-2026!";

    [Fact]
    public void RoundTrip_PreservesWorkspaceRevisionAndTemplates()
    {
        var source = CreateWorkspace();
        var restored = WorkspaceCrypto.Decrypt(WorkspaceCrypto.Encrypt(source, Password), Password);
        Assert.Equal(source.WorkspaceId, restored.WorkspaceId);
        Assert.Equal(3, restored.Revision);
        Assert.Single(restored.Templates);
    }

    [Fact]
    public void WrongPassword_IsRejected() => Assert.Throws<PackageSecurityException>(() =>
        WorkspaceCrypto.Decrypt(WorkspaceCrypto.Encrypt(CreateWorkspace(), Password), "Falsches-Adminpasswort!"));

    [Fact]
    public void ModifiedWorkspace_IsRejected()
    {
        var bytes = WorkspaceCrypto.Encrypt(CreateWorkspace(), Password);
        bytes[^12] ^= 0x01;
        Assert.Throws<PackageSecurityException>(() => WorkspaceCrypto.Decrypt(bytes, Password));
    }

    private static PortableAdminWorkspace CreateWorkspace() => new()
    {
        Revision = 3, SchoolName = "Testschule", SchoolYear = "2026/2027", HalfYear = 1,
        Templates = { new PortableAssignmentTemplate { ClassName = "4a", ScopeId = "sport", ScopeLabel = "Sport" } }
    };
}
