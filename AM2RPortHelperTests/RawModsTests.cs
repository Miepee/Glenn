using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
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
        testTempDir = Path.GetTempPath() + Guid.NewGuid() + "/";
        Directory.CreateDirectory(testTempDir);
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
        var destinationZip = Path.GetTempPath() + Guid.NewGuid() + ".zip";
        ZipFile.ExtractToDirectory("./GameWin.zip", testTempDir);
        File.Move(testTempDir + "/AM2R Server.exe", testTempDir + "/AM2R.exe");
        ZipFile.CreateFromDirectory(testTempDir, destinationZip);
        var result = RawMods.GetModOSOfRawZip(destinationZip);
        Assert.True(result == Core.ModOS.Windows);
    }
    
    [Fact]
    public void WindowsZipWithTwoRunnersShouldThrow()
    {
        var destinationZip = Path.GetTempPath() + Guid.NewGuid();
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
    [InlineData("./GameWin.zip", false, false)]
    [InlineData("./GameLin.zip", false, false)]
    [InlineData("./GameWin.zip", true, true)]
    [InlineData("./GameLin.zip", true, true)]
    public void PortZipToWindows(string inputZip, bool useSubdirectories, bool createWorkingDirectoryBeforeHand)
    {
        var origMod = RawMods.GetModOSOfRawZip(inputZip);
        var outputZip = testTempDir + Guid.NewGuid();
        var origExtract = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid() + "/";
        var deepSuffix = "Foobar/Foobar/Foo/Blag/";
        var origInput = inputZip;
        
        if (useSubdirectories)
        {
            string archiveDeepSuffix = deepSuffix;
            if (origMod == Core.ModOS.Linux)
            {
                archiveDeepSuffix =  "assets/" + deepSuffix;
            }
            
            File.Copy(inputZip, testTempDir + inputZip + "_modified");
            inputZip = testTempDir + inputZip + "_modified";
            using ZipArchive archive = ZipFile.Open(inputZip, ZipArchiveMode.Update);
            archive.CreateEntry(archiveDeepSuffix + origInput);
        }

        if (createWorkingDirectoryBeforeHand)
            Directory.CreateDirectory(libTempDir + Path.GetFileNameWithoutExtension(inputZip));
        
        RawMods.PortToWindows(inputZip, outputZip);
        // Our function should see that its a windows zip
        Assert.True(RawMods.GetModOSOfRawZip(outputZip) == Core.ModOS.Windows);
        
        ZipFile.ExtractToDirectory(inputZip, origExtract);
        ZipFile.ExtractToDirectory(outputZip, newExtract);
        switch (origMod)
        {
            case Core.ModOS.Windows:
            {
                // File contents should be same between the zips
                var origFiles = new DirectoryInfo(origExtract).GetFiles().Select(f => f.Name).ToList();
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract).GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            case Core.ModOS.Linux:
            {
                // File contents should be same between the zips except for runner missing in original and data file being different
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
        
        // If we didn't specify any, there should be no subdirectories at the end
        if (!useSubdirectories)
        {
            Assert.Empty(new DirectoryInfo(newExtract).GetDirectories());
            return;
        }
        
        //Otherwise there should be our stuff
        Assert.True(File.Exists(newExtract + deepSuffix + origInput));
    }
    #endregion

    // TODO: check that the ports here use the icons we specified
    #region PortToLinux

    [Theory]
    [InlineData("./GameWin.zip", false, false)]
    [InlineData("./GameLin.zip", false, false)]
    [InlineData("./GameWin.zip", true, true)]
    [InlineData("./GameLin.zip", true, true)]
    public void PortZipToLinux(string inputZip, bool useSubdirectories, bool createWorkingDirectoryBeforeHand)
    {
        var origMod = RawMods.GetModOSOfRawZip(inputZip);
        var outputZip = testTempDir + Guid.NewGuid();
        var origExtract = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid() + "/";
        var deepSuffix = "Foobar/Foobar/Foo/Blag/";
        var origInput = inputZip;
        
        if (useSubdirectories)
        {
            string archiveDeepSuffix = deepSuffix;
            if (origMod == Core.ModOS.Linux)
            {
                archiveDeepSuffix =  "assets/" + deepSuffix;
            }
            
            File.Copy(inputZip, testTempDir + inputZip + "_modified");
            inputZip = testTempDir + inputZip + "_modified";
            using ZipArchive archive = ZipFile.Open(inputZip, ZipArchiveMode.Update);
            archive.CreateEntry(archiveDeepSuffix + origInput);
        }
        
        if (createWorkingDirectoryBeforeHand)
            Directory.CreateDirectory(libTempDir + Path.GetFileNameWithoutExtension(inputZip));
        
        RawMods.PortToLinux(inputZip, outputZip);
        // Our function should see that its a linux zip
        Assert.True(RawMods.GetModOSOfRawZip(outputZip) == Core.ModOS.Linux);
        
        ZipFile.ExtractToDirectory(inputZip, origExtract);
        ZipFile.ExtractToDirectory(outputZip, newExtract);
        switch (origMod)
        {
            case Core.ModOS.Windows:
            {
                // File contents should be same between the zips except for old runner+d3d.dll, new splash+icon,
                // files being lowered and data file being different
                var origFiles = new DirectoryInfo(origExtract).GetFiles().Select(f => f.Name.ToLower()).ToList();
                origFiles.Remove("am2r server.exe");
                origFiles.Remove("d3dx9_43.dll");
                origFiles.Add("splash.png");
                origFiles.Add("icon.png");
                origFiles.Remove("data.win");
                origFiles.Add("game.unx");
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            case Core.ModOS.Linux:
            {
                // File contents should be same between the zips
                List<string> origFiles = new DirectoryInfo(origExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                origFiles.Sort();
                List<string> newFiles = new DirectoryInfo(newExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
        }
        
        // There should be exactly one subdir here
        Assert.Single(new DirectoryInfo(newExtract).GetDirectories());
        
        // If we didn't specify any, there should no more subdirs after that at the end
        if (!useSubdirectories)
        {
            Assert.Empty(new DirectoryInfo(newExtract + "/assets").GetDirectories());
            return;
        }
        
        //Otherwise there should be our stuff
        Assert.True(File.Exists(newExtract + "/assets/" + deepSuffix.ToLower() + origInput.ToLower()));
    }
    
    [Theory]
    [InlineData("./GameWin.zip", false, false)]
    [InlineData("./GameLin.zip", false, false)]
    [InlineData("./GameWin.zip", true, true)]
    [InlineData("./GameLin.zip", true, true)]
    public void PortZipToMac(string inputZip, bool useSubdirectories, bool createWorkingDirectoryBeforeHand)
    {
        var origMod = RawMods.GetModOSOfRawZip(inputZip);
        var outputZip = testTempDir + Guid.NewGuid();
        var origExtract = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid() + "/";
        var deepSuffix = "Foobar/Foobar/Foo/Blag/";
        var origInput = inputZip;
        
        if (useSubdirectories)
        {
            string archiveDeepSuffix = deepSuffix;
            if (origMod == Core.ModOS.Linux)
            {
                archiveDeepSuffix =  "assets/" + deepSuffix;
            }
            
            File.Copy(inputZip, testTempDir + inputZip + "_modified");
            inputZip = testTempDir + inputZip + "_modified";
            using ZipArchive archive = ZipFile.Open(inputZip, ZipArchiveMode.Update);
            archive.CreateEntry(archiveDeepSuffix + origInput);
        }
        
        if (createWorkingDirectoryBeforeHand)
            Directory.CreateDirectory(libTempDir + Path.GetFileNameWithoutExtension(inputZip));
        
        RawMods.PortToMac(inputZip, outputZip);
        // Our function should see that its a mac zip
        Assert.True(RawMods.GetModOSOfRawZip(outputZip) == Core.ModOS.Mac);
        
        ZipFile.ExtractToDirectory(inputZip, origExtract);
        ZipFile.ExtractToDirectory(outputZip, newExtract);
        var appDir = new DirectoryInfo(newExtract).GetDirectories().First(n => n.Name.EndsWith(".app"));
        switch (origMod)
        {
            case Core.ModOS.Windows:
            {
                // File contents should be same between the zips except for old runner+d3d.dll, new splash+icon,
                // files being lowercase, data file being different a new gamecontrollerdb and a new yoyorunner.config
                
                var origFiles = new DirectoryInfo(origExtract).GetFiles().Select(f => f.Name.ToLower()).ToList();
                origFiles.Remove("am2r server.exe");
                origFiles.Remove("d3dx9_43.dll");
                origFiles.Add("splash.png");
                origFiles.Add("icon.png");
                origFiles.Remove("data.win");
                origFiles.Add("game.ios");
                origFiles.Add("gamecontrollerdb.txt");
                origFiles.Add("yoyorunner.config");
                origFiles.Sort();
                var newFiles = new DirectoryInfo(appDir.FullName + "/Contents/Resources").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            case Core.ModOS.Linux:
            {
                // File contents should be same between the zips except for all files being lowercase, data file being different and mac specific files
                List<string> origFiles = new DirectoryInfo(origExtract + "/assets").GetFiles().Select(f => f.Name.ToLower()).ToList();
                origFiles.Remove("game.unx");
                origFiles.Add("game.ios");
                origFiles.Add("gamecontrollerdb.txt");
                origFiles.Add("yoyorunner.config");
                origFiles.Sort();
                List<string> newFiles = new DirectoryInfo(newExtract + "/" + appDir.Name + "/Contents/Resources").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
        }
        
        // There should be exactly one subdir here and it should end with .app
        Assert.Single(new DirectoryInfo(newExtract).GetDirectories());
        Assert.EndsWith(".app", new DirectoryInfo(newExtract).GetDirectories().First().Name);
        
        // If we didn't specify any, there should one more subdirs after that at the end (the English.lproj)
        if (!useSubdirectories)
        {
            Assert.Equal("English.lproj", new DirectoryInfo(newExtract + "/" + appDir.Name + "/Contents/Resources").GetDirectories().First().Name);
            return;
        }
        
        //Otherwise there should be also stuff
        Assert.Equal("English.lproj", new DirectoryInfo(newExtract + "/" + appDir.Name + "/Contents/Resources").GetDirectories().First(d => d.Name == "English.lproj").Name);
        Assert.True(File.Exists(newExtract + "/" + appDir.Name + "/Contents/Resources/" + deepSuffix.ToLower() + origInput.ToLower()));
    }

    #endregion
    
    // TODO: write tests for porttoandroid
}