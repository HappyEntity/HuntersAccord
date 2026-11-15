using HuntersAccord.Core.Emulator;

namespace HuntersAccord.Core.Tests;

public sealed class AppPathsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_the_variable_the_data_folder_is_the_launchers(string? variable) =>
        Assert.Equal(AppPaths.LauncherDataRoot, AppPaths.DataRootFor(variable));

    [Fact]
    public void The_variable_moves_the_data_folder_as_a_full_path()
    {
        var elsewhere = Path.Combine(Path.GetTempPath(), "ha-tests-data");
        Assert.Equal(Path.GetFullPath(elsewhere), AppPaths.DataRootFor($"  {elsewhere}  "));
        Assert.True(Path.IsPathFullyQualified(AppPaths.DataRootFor("relative-data")));
    }

    [Fact]
    public void The_launchers_folder_is_named_HuntersAccord() =>
        Assert.Equal("HuntersAccord", Path.GetFileName(AppPaths.LauncherDataRoot));

    [Fact]
    public void Inside_means_the_folder_itself_or_below_it_not_a_sibling_with_its_name_as_a_prefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "x", "HuntersAccord");
        Assert.True(AppPaths.IsInside(root, root));
        Assert.True(AppPaths.IsInside(root + Path.DirectorySeparatorChar, root));
        Assert.True(AppPaths.IsInside(Path.Combine(root, "profile", "user", "home"), root));
        Assert.True(AppPaths.IsInside(Path.Combine(root, "profile", "..", "profile-2"), root));
        Assert.False(AppPaths.IsInside(root + "-CoopTest", root));
        Assert.False(AppPaths.IsInside(Path.Combine(root, "..", "HuntersAccord-CoopTest", "profile"), root));
        Assert.False(AppPaths.IsInside(Path.GetDirectoryName(root)!, root));
    }

    [Fact]
    public void Inside_ignores_case_and_slashes_on_Windows()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True(AppPaths.IsInside(@"C:/PROFILES/a/AppData/Local/HUNTERSACCORD/profile", @"c:\profiles\a\appdata\local\HuntersAccord\"));
        Assert.True(AppPaths.SamePath(@"C:\Data\HuntersAccord\", @"c:/data/huntersaccord"));
    }

    [Fact]
    public void A_profile_folder_per_player_in_a_data_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "data");
        Assert.Equal(Path.Combine(root, "profile"), EmulatorProfile.RootIn(root, 1));
        Assert.Equal(Path.Combine(root, "profile-2"), EmulatorProfile.RootIn(root, 2));
    }

    [Theory]
    [InlineData(null, ModContract.DirectoryPort)]
    [InlineData("", ModContract.DirectoryPort)]
    [InlineData("18680", 18680)]
    [InlineData("0", ModContract.DirectoryPort)]
    [InlineData("70000", ModContract.DirectoryPort)]
    [InlineData("-5", ModContract.DirectoryPort)]
    [InlineData("port", ModContract.DirectoryPort)]
    public void The_session_directorys_port_moves_only_to_a_port(string? variable, int port) =>
        Assert.Equal(port, ModContract.DirectoryPortFor(variable));
}
