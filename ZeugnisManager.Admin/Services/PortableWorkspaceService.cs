using IKO.ZeugnisManager.Core;

namespace ZeugnisManager.Admin.Services;

public sealed class PortableWorkspaceService(RealPackageService packages)
{
    public (byte[] Bytes, string FileName, PortableAdminWorkspace Workspace) Create(string password)
    {
        var workspace = packages.CreateWorkspace();
        var bytes = WorkspaceCrypto.Encrypt(workspace, password);
        return (bytes, $"Admin-Arbeitsbestand_{workspace.SchoolYear.Replace('/', '-')}_H{workspace.HalfYear}_R{workspace.Revision}.zmadm", workspace);
    }

    public PortableAdminWorkspace Inspect(byte[] bytes, string password) => WorkspaceCrypto.Decrypt(bytes, password);

    public (int Students, int Fields) Import(byte[] bytes, string password)
    {
        var workspace = WorkspaceCrypto.Decrypt(bytes, password);
        return packages.ApplyWorkspace(workspace);
    }
}
