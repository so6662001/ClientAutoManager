using PuxunAppManager.Core.Installation;
using PuxunAppManager.Core.Models;

namespace PuxunAppManager.Tests;

public class InstallerCommandTests
{
    [Fact]
    public void Msi_Install_UsesMsiexecSlashI()
    {
        var pkg = new InstallerPackage { InstallerType = InstallerType.Msi };
        var (file, args) = InstallerService.BuildCommand(pkg, @"C:\tmp\a.msi", OperationType.Install);
        Assert.Equal("msiexec.exe", file);
        Assert.Contains("/i", args);
        Assert.Contains("/qn", args);
        Assert.Contains("/norestart", args);
    }

    [Fact]
    public void Msi_Repair_UsesSlashFa()
    {
        var pkg = new InstallerPackage { InstallerType = InstallerType.Msi };
        var (_, args) = InstallerService.BuildCommand(pkg, @"C:\tmp\a.msi", OperationType.Repair);
        Assert.Contains("/fa", args);
    }

    [Fact]
    public void Exe_ReplacesFilePlaceholder()
    {
        var pkg = new InstallerPackage { InstallerType = InstallerType.Exe, SilentArgs = "/S /path={file}" };
        var (file, args) = InstallerService.BuildCommand(pkg, @"C:\tmp\setup.exe", OperationType.Install);
        Assert.Equal(@"C:\tmp\setup.exe", file);
        Assert.Contains(@"""C:\tmp\setup.exe""", args);
        Assert.DoesNotContain("{file}", args);
    }
}
