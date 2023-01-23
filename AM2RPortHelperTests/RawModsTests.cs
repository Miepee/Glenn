using System.Diagnostics;
using System.IO.Compression;
using AM2RPortHelperLib;
using UndertaleModLib.Decompiler;
using Xunit;
using Xunit.Abstractions;

namespace AM2RPortHelperTests;

public class RawModsTests
{
    //TODO: write tests for mac later

    private readonly string testTempDir;
    private readonly string libTempDir = Path.GetTempPath() + "/PortHelper/";
    private readonly ITestOutputHelper output;
    
    public RawModsTests(ITestOutputHelper output)
    {
        Directory.CreateDirectory(libTempDir);
        testTempDir = Path.GetTempPath() + Guid.NewGuid();
        this.output = output;
    }

    #region GetModOSOfRawZipTests
    
    [Fact]
    public void WindowsZipWithDifferentRunnerShouldBeWindows()
    {
        var result = RawMods.GetModOSOfRawZip("./GameWin.zip");
        Assert.True(result == Core.ModOS.Windows);
    }
    
    [Fact]
    public void WindowsZipWithSameRunnerShouldBeWindows()
    {
        var destinationZip = testTempDir + Guid.NewGuid();
        ZipFile.ExtractToDirectory("./GameWin.zip", testTempDir);
        File.Move(testTempDir + "/AM2R Server.exe", testTempDir + "/AM2R.exe");
        ZipFile.CreateFromDirectory(testTempDir, destinationZip);
        var result = RawMods.GetModOSOfRawZip(destinationZip);
        Assert.True(result == Core.ModOS.Windows);
    }
    
    [Fact]
    public void WindowsZipWithTwoRunnersShouldThrow()
    {
        var destinationZip = testTempDir + Guid.NewGuid();
        ZipFile.ExtractToDirectory("./GameWin.zip", testTempDir);
        File.Copy(testTempDir + "/AM2R Server.exe", testTempDir + "/AM2R.exe");
        ZipFile.CreateFromDirectory(testTempDir, destinationZip);
        Assert.Throws<NotSupportedException>(() => RawMods.GetModOSOfRawZip(destinationZip));
    }
    
    [Fact]
    public void LinuxZipWithGoodRunnerShouldBeLinux()
    {
        var result = RawMods.GetModOSOfRawZip("./GameLin.zip");
        Assert.True(result == Core.ModOS.Linux);
    }
    
    [Fact]
    public void LinuxZipWithWrongRunnerShouldThrow()
    {
        var destinationZip = Path.GetTempPath() + Guid.NewGuid();
        ZipFile.ExtractToDirectory("./GameLin.zip", testTempDir);
        File.Move(testTempDir + "/runner", testTempDir + "/AM2R");
        ZipFile.CreateFromDirectory(testTempDir, destinationZip);
        Assert.Throws<NotSupportedException>(() => RawMods.GetModOSOfRawZip(destinationZip));
    }
    #endregion

    #region GetProperPathToBuiltinIcons

    [Fact]
    public void ExistingPathShouldReturnPath()
    {
        const string relative = "./GameLin.zip";
        string absolute = new FileInfo(relative).FullName;
        string result = RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), relative);
        Assert.True(result == relative);
        result = RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), absolute);
        Assert.True(result == absolute);
    }

    [Fact]
    public void NonExistantPathShouldReturnPathToResource()
    {
        string iconPath = libTempDir + "/" + nameof(Resources.icon) + ".png";
        string result = RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), null);
        Assert.True(result == iconPath);
        result = RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), "/foo");
        Assert.True(result == iconPath);
    }

    [Fact]
    public void NonExistantResourceShouldThrow()
    {
        Assert.Throws<InvalidDataException>(() => RawMods.GetProperPathToBuiltinIcons("foo", null));
    }

    #endregion
    
    #region PortToWindows
    
    [Theory]
    [InlineData("./GameWin.zip")]
    [InlineData("./GameLin.zip")]
    public void PortZipToWindows(string inputZip)
    {
        var origMod = RawMods.GetModOSOfRawZip(inputZip);
        var outputZip = testTempDir + Guid.NewGuid();
        var origExtract = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid();
        RawMods.PortToWindows(inputZip, outputZip);
        // Our function should see that its a windows zip
        Assert.True(RawMods.GetModOSOfRawZip(outputZip) == Core.ModOS.Windows);
        switch (origMod)
        {
            case Core.ModOS.Windows:
            {
                // File contents should be same between the zips
                ZipFile.ExtractToDirectory(inputZip, origExtract);
                ZipFile.ExtractToDirectory(outputZip, newExtract);
                var origFiles = new DirectoryInfo(origExtract).GetFiles().Select(f => f.Name);
                var newFiles = new DirectoryInfo(newExtract).GetFiles().Select(f => f.Name);
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            case Core.ModOS.Linux:
            {
                // File contents should be same between the zips except for runner missing in original and data file being different
                ZipFile.ExtractToDirectory(inputZip, origExtract);
                ZipFile.ExtractToDirectory(outputZip, newExtract);
                List<string> origFiles = new DirectoryInfo(origExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                origFiles.Remove("game.unx");
                origFiles.Add("data.win");
                origFiles.Add("AM2R.exe");
                origFiles.Sort();
                List<string> newFiles = new DirectoryInfo(newExtract).GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
        }
        // There should be no subdirectories at the end
        Assert.Equal(0, new DirectoryInfo(newExtract).GetDirectories().Length);
    }
    #endregion
    
    // TODO: write tests for porttolinux, porttoandroid, porttomac
}