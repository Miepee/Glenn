using System.IO.Compression;
using AM2RPortHelperLib;
using UndertaleModLib;
using Xunit;
using Xunit.Abstractions;

namespace AM2RPortHelperTests;

public class RawModsTests : IDisposable
{
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

    public void Dispose()
    {
        // Get rid of our test directory to not leave a huge mess
        Directory.Delete(testTempDir, true);
    }

    #region GetModOSOfRawZipTests
    
    [Theory]
    [InlineData("./GameWin.zip", Core.ModOS.Windows)]
    [InlineData("./GameLin.zip", Core.ModOS.Linux)]
    [InlineData("./GameMac.zip", Core.ModOS.Mac)]
    public void OSZipWithGoodRunnerShouldSucceed(string input, Core.ModOS os)
    {
        var result = RawMods.GetModOSOfRawZip(input);
        Assert.True(result == os);
    }

    [Theory]
    [InlineData("./GameLin.zip", "/runner")]
    [InlineData("./GameMac.zip", "/AM2R.app/Contents/MacOS/Mac_Runner")]
    public void UnixWithWrongRunnerShouldThrow(string input, string runnerSuffix)
    {
        var destinationZip = Path.GetTempPath() + Guid.NewGuid();
        ZipFile.ExtractToDirectory(input, testTempDir);
        File.Move(testTempDir + runnerSuffix, testTempDir + runnerSuffix + "_");
        ZipFile.CreateFromDirectory(testTempDir, destinationZip);
        Assert.Throws<NotSupportedException>(() => RawMods.GetModOSOfRawZip(destinationZip));
        File.Delete(destinationZip);
    }
    
    [Fact]
    public void WindowsZipWithDifferentRunnerShouldSucceed()
    {
        var destinationZip = Path.GetTempPath() + Guid.NewGuid() + ".zip";
        ZipFile.ExtractToDirectory("./GameWin.zip", testTempDir);
        File.Move(testTempDir + "/AM2R.exe", testTempDir + "/Game.exe");
        ZipFile.CreateFromDirectory(testTempDir, destinationZip);
        var result = RawMods.GetModOSOfRawZip(destinationZip);
        Assert.True(result == Core.ModOS.Windows);
        File.Delete(destinationZip);
    }
    
    [Fact]
    public void WindowsZipWithTwoRunnersShouldThrow()
    {
        var destinationZip = Path.GetTempPath() + Guid.NewGuid();
        ZipFile.ExtractToDirectory("./GameWin.zip", testTempDir);
        File.Copy(testTempDir + "/AM2R.exe", testTempDir + "/Game.exe");
        ZipFile.CreateFromDirectory(testTempDir, destinationZip);
        Assert.Throws<NotSupportedException>(() => RawMods.GetModOSOfRawZip(destinationZip));
        File.Delete(destinationZip);
    }
    
    [Theory]
    [InlineData("./GameWin.zip", "/data.win")]
    [InlineData("./GameLin.zip", "/assets/game.unx")]
    [InlineData("./GameMac.zip", "/AM2R.app/Contents/Resources/game.ios")]
    public void OSZipWithInvalidDataFileShouldThrow(string input, string dataSuffix)
    {
        var destinationZip = Path.GetTempPath() + Guid.NewGuid() + ".zip";
        ZipFile.ExtractToDirectory(input, testTempDir);
        File.Move(testTempDir + dataSuffix, testTempDir + dataSuffix + "_");
        ZipFile.CreateFromDirectory(testTempDir, destinationZip);
        Assert.Throws<NotSupportedException>(() => RawMods.GetModOSOfRawZip(destinationZip));
        File.Delete(destinationZip);
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
    [InlineData("./GameMac.zip", false, false)]
    [InlineData("./GameWin.zip", true, true)]
    [InlineData("./GameLin.zip", true, true)]
    [InlineData("./GameMac.zip", true, true)]
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
                archiveDeepSuffix = "assets/" + deepSuffix;
            else if (origMod == Core.ModOS.Mac)
                archiveDeepSuffix = "AM2R.app/Contents/Resources/" + deepSuffix;
            
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
                // File contents should be same between the zips except for runner+d3d.dll missing in original and data file being different
                var origFiles = new DirectoryInfo(origExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                origFiles.Remove("game.unx");
                origFiles.Add("data.win");
                origFiles.Add("AM2R.exe");
                origFiles.Add("D3DX9_43.dll");
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract).GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            case Core.ModOS.Mac:
            {
                // File contents should be the same between the zips except for runner+d3d.dll missing in original, data file being different and extra mac files
                var origFiles = new DirectoryInfo(origExtract + "/AM2R.app/Contents/Resources").GetFiles().Select(f => f.Name).ToList();
                origFiles.Remove("game.ios");
                origFiles.Add("data.win");
                origFiles.Add("AM2R.exe");
                origFiles.Add("D3DX9_43.dll");
                origFiles.Remove("gamecontrollerdb.txt");
                origFiles.Remove("yoyorunner.config");
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract).GetFiles().Select(f => f.Name).ToList();
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
    
    #region PortToLinux

    [Theory]
    [InlineData("./GameWin.zip", false, false)]
    [InlineData("./GameLin.zip", false, false)]
    [InlineData("./GameMac.zip", false, false)]
    [InlineData("./GameWin.zip", true, true)]
    [InlineData("./GameLin.zip", true, true)]
    [InlineData("./GameMac.zip", true, true)]
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
                archiveDeepSuffix =  "assets/" + deepSuffix.ToLower();
            else if (origMod == Core.ModOS.Mac)
                archiveDeepSuffix = "AM2R.app/Contents/Resources/" + deepSuffix;
            
            File.Copy(inputZip, testTempDir + inputZip.ToLower() + "_modified");
            inputZip = testTempDir + inputZip.ToLower() + "_modified";
            using ZipArchive archive = ZipFile.Open(inputZip, ZipArchiveMode.Update);
            archive.CreateEntry(archiveDeepSuffix + origInput.ToLower());
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
                origFiles.Remove("am2r.exe");
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
                var origFiles = new DirectoryInfo(origExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            case Core.ModOS.Mac:
            {
                // File contents should be the same between the zips except for data file being different and extra mac files
                var origFiles = new DirectoryInfo(origExtract + "/AM2R.app/Contents/Resources").GetFiles().Select(f => f.Name).ToList();
                origFiles.Remove("game.ios");
                origFiles.Add("game.unx");
                origFiles.Remove("gamecontrollerdb.txt");
                origFiles.Remove("yoyorunner.config");
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
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
    [InlineData("./GameWin.zip")]
    [InlineData("./GameLin.zip")]
    [InlineData("./GameMac.zip")]
    public void CheckThatLinuxPortHasProperIcons(string inputZip)
    {
        var outputZip = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid() + "/";
        
        // With default icons
        void CheckIconsWithPath(string? path)
        {
            File.Delete(outputZip);
            RawMods.PortToLinux(inputZip, outputZip, path, path);
            if (Directory.Exists(newExtract))
                Directory.Delete(newExtract, true);
            ZipFile.ExtractToDirectory(outputZip, newExtract);
            var newIcon = File.ReadAllBytes(newExtract + "/assets/icon.png");
            var newSplash = File.ReadAllBytes(newExtract + "/assets/splash.png");
            Directory.CreateDirectory(libTempDir);
            var oldIcon = File.ReadAllBytes(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), path));
            var oldSplash = File.ReadAllBytes(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.splash), path));

            Assert.True(newIcon.SequenceEqual(oldIcon));
            Assert.True(newSplash.SequenceEqual(oldSplash));
        }
        
        CheckIconsWithPath(null);
        CheckIconsWithPath(inputZip);
    }
    
    #endregion
    
    #region PortToMac
    
    [Theory]
    [InlineData("./GameWin.zip", false, false, false)]
    [InlineData("./GameLin.zip", false, false, false)]
    [InlineData("./GameMac.zip", false, false, false)]
    [InlineData("./GameWin.zip", true, true, true)]
    [InlineData("./GameLin.zip", true, true, true)]
    [InlineData("./GameMac.zip", true, true, true)]
    public void PortZipToMac(string inputZip, bool useSubdirectories, bool createWorkingDirectoryBeforeHand, bool testFontsFolder)
    {
        var origMod = RawMods.GetModOSOfRawZip(inputZip);
        var outputZip = testTempDir + Guid.NewGuid();
        var origExtract = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid() + "/";
        var deepSuffix = "Foobar/Foobar/Foo/Blag/";
        var origInput = inputZip;

        if (testFontsFolder && origMod != Core.ModOS.Mac)
        {
            string assetsDir = "";
            if (origMod == Core.ModOS.Linux)
                assetsDir = "assets/";

            File.Copy(inputZip, testTempDir + inputZip.ToLower().Replace(testTempDir, "") + "_modified");
            inputZip = testTempDir + inputZip.ToLower().Replace(testTempDir, "") + "_modified";
            using ZipArchive archive = ZipFile.Open(inputZip, ZipArchiveMode.Update);
            archive.CreateEntry(assetsDir + "lang/fonts/");
        }
        
        if (useSubdirectories)
        {
            string archiveDeepSuffix = deepSuffix;
            if (origMod == Core.ModOS.Linux)
                archiveDeepSuffix =  "assets/" + deepSuffix;
            else if (origMod == Core.ModOS.Mac)
                archiveDeepSuffix = "AM2R.app/Contents/Resources/" + deepSuffix.ToLower();
            
            File.Copy(inputZip, testTempDir + inputZip.Replace(testTempDir, "") + "_modified");
            inputZip = testTempDir + inputZip.Replace(testTempDir, "") + "_modified";
            using ZipArchive archive = ZipFile.Open(inputZip, ZipArchiveMode.Update);
            archive.CreateEntry(archiveDeepSuffix + origInput.ToLower());
        }
        
        if (createWorkingDirectoryBeforeHand)
            Directory.CreateDirectory(libTempDir + Path.GetFileNameWithoutExtension(inputZip));
        
        RawMods.PortToMac(inputZip, outputZip);
        
        // Our function should see that its a mac zip
        Assert.True(RawMods.GetModOSOfRawZip(outputZip) == Core.ModOS.Mac);
        
        ZipFile.ExtractToDirectory(inputZip, origExtract);
        ZipFile.ExtractToDirectory(outputZip, newExtract);
        var appDir = new DirectoryInfo(newExtract).GetDirectories().First(n => n.Name.EndsWith(".app"));
        
        // Confirm via UTMT that it is indeed BC16
        using (FileStream fs = new FileInfo(appDir.FullName + "/Contents/Resources/game.ios").OpenRead())
        {
            UndertaleData gmData = UndertaleIO.Read(fs);
            Assert.Equal(16, gmData.GeneralInfo.BytecodeVersion);
        }
        
        switch (origMod)
        {
            case Core.ModOS.Windows:
            {
                // File contents should be same between the zips except for old runner+d3d.dll, new splash+icon,
                // files being lowercase, data file being different a new gamecontrollerdb and a new yoyorunner.config
                var origFiles = new DirectoryInfo(origExtract).GetFiles().Select(f => f.Name.ToLower()).ToList();
                origFiles.Remove("am2r.exe");
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
                var origFiles = new DirectoryInfo(origExtract + "/assets").GetFiles().Select(f => f.Name.ToLower()).ToList();
                origFiles.Remove("game.unx");
                origFiles.Add("game.ios");
                origFiles.Add("gamecontrollerdb.txt");
                origFiles.Add("yoyorunner.config");
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract + "/" + appDir.Name + "/Contents/Resources").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            case Core.ModOS.Mac:
            {
                // File contents should be same between the zips
                var origFiles = new DirectoryInfo(origExtract + "/" + appDir.Name + "/Contents/Resources").GetFiles().Select(f => f.Name).ToList();
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract + "/" + appDir.Name + "/Contents/Resources").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
        }
        
        // There should be exactly one subdir here and it should end with .app
        Assert.Single(new DirectoryInfo(newExtract).GetDirectories());
        Assert.EndsWith(".app", new DirectoryInfo(newExtract).GetDirectories().First().Name);
        Assert.Equal("English.lproj", new DirectoryInfo(newExtract + "/" + appDir.Name + "/Contents/Resources").GetDirectories().First(d => d.Name == "English.lproj").Name);
        
        // If we didn't specify any, there should two more subdirs after that at the end (the English.lproj, lang)
        // fonts directory in lang should not exist anymore!
        
        Assert.True(new DirectoryInfo(newExtract + "/" + appDir.Name + "/Contents/Resources/English.lproj").Exists);
        Assert.False(new DirectoryInfo(newExtract + "/" + appDir.Name + "/Contents/Resources/lang/fonts").Exists);
        
        if (!useSubdirectories)
            return;
        
        // Otherwise there should be also our stuff
        Assert.True(File.Exists(newExtract + "/" + appDir.Name + "/Contents/Resources/" + deepSuffix.ToLower() + origInput.ToLower()));
    }
    
    [Theory]
    [InlineData("./GameWin.zip")]
    [InlineData("./GameLin.zip")]
    [InlineData("./GameMac.zip")]
    public void CheckThatMacPortHasProperIcons(string inputZip)
    {
        var outputZip = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid() + "/";
        
        // With default icons
        void CheckIconsWithPath(string? path)
        {
            File.Delete(outputZip);
            RawMods.PortToMac(inputZip, outputZip, path, path);
            if (Directory.Exists(newExtract))
                Directory.Delete(newExtract, true);
            ZipFile.ExtractToDirectory(outputZip, newExtract);
            var newIcon = File.ReadAllBytes(newExtract + "/AM2R.app/Contents/Resources/icon.png");
            var newSplash = File.ReadAllBytes(newExtract + "/AM2R.app/Contents/Resources/splash.png");
            Directory.CreateDirectory(libTempDir);
            var oldIcon = File.ReadAllBytes(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), path));
            var oldSplash = File.ReadAllBytes(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.splash), path));

            Assert.True(newIcon.SequenceEqual(oldIcon));
            Assert.True(newSplash.SequenceEqual(oldSplash));
        }
        
        CheckIconsWithPath(null);
        CheckIconsWithPath(inputZip);
    }
    
    #endregion

    #region PortInvalidZips

    [Theory]
    [InlineData(Core.ModOS.Windows)]
    [InlineData(Core.ModOS.Linux)]
    [InlineData(Core.ModOS.Mac)]
    public void PortInvalidZipsToOS(Core.ModOS os)
    {
        Action<string?, string?> function = os switch
        {
            Core.ModOS.Windows => (input, outputFile) => RawMods.PortToWindows(input, outputFile),
            Core.ModOS.Linux => (input, outputFile) => RawMods.PortToLinux(input, outputFile),
            Core.ModOS.Mac => (input, outputFile) => RawMods.PortToMac(input, outputFile),
            _ => throw new Exception("This should not have happened! new unhandled data!")
        };
        
        Assert.Throws<ArgumentNullException>(() => function.Invoke(null, "/foo"));
        Assert.Throws<FileNotFoundException>(() => function.Invoke("/foo", "/foo"));
        Assert.Throws<ArgumentOutOfRangeException>(() => function.Invoke("./GameLin.zip", null));

    }
    #endregion

    #region Make sure porting methods work when called in succession

    [Theory]
    [InlineData("./GameWin.zip", Core.ModOS.Windows)]
    [InlineData("./GameWin.zip", Core.ModOS.Linux)]
    [InlineData("./GameWin.zip", Core.ModOS.Mac)]
    [InlineData("./GameLin.zip", Core.ModOS.Windows)]
    [InlineData("./GameLin.zip", Core.ModOS.Linux)]
    [InlineData("./GameLin.zip", Core.ModOS.Mac)]
    [InlineData("./GameMac.zip", Core.ModOS.Windows)]
    [InlineData("./GameMac.zip", Core.ModOS.Linux)]
    [InlineData("./GameMac.zip", Core.ModOS.Mac)]
    public void TestPortToOSMultipleTimes(string input, Core.ModOS os)
    {
        Action<string?, string?> function = os switch
        {
            Core.ModOS.Windows => (inputFile, outputFile) => RawMods.PortToWindows(inputFile, outputFile),
            Core.ModOS.Linux => (inputFile, outputFile) => RawMods.PortToLinux(inputFile, outputFile),
            Core.ModOS.Mac => (inputFile, outputFile) => RawMods.PortToMac(inputFile, outputFile),
            _ => throw new Exception("This should not have happened! new unhandled data!")
        };
        
        string outputZip = testTempDir + "/foobar.zip";
        function.Invoke(input, outputZip);
        Assert.Throws<IOException>(() => function.Invoke(input, outputZip));
        File.Delete(outputZip);
        function.Invoke(input, outputZip);
        Assert.True(File.Exists(outputZip));
    }
    
    #endregion
    
    // TODO: write tests for porttoandroid
}