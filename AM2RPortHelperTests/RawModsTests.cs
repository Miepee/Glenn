using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
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
            default: throw new Exception("unwritten test case for new os?");
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
            default: throw new Exception("unwritten test case for new os?");
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
            default: throw new Exception("unwritten test case for new os?");
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
    
    #endregion

    #region PortInvalidZips

    [Theory]
    [InlineData(Core.ModOS.Windows)]
    [InlineData(Core.ModOS.Linux)]
    [InlineData(Core.ModOS.Mac)]
    [InlineData(Core.ModOS.Android)]
    public void PortInvalidZipsToOS(Core.ModOS os)
    {
        Action<string?, string?> function = os switch
        {
            Core.ModOS.Windows => (input, outputFile) => RawMods.PortToWindows(input, outputFile),
            Core.ModOS.Linux => (input, outputFile) => RawMods.PortToLinux(input, outputFile),
            Core.ModOS.Mac => (input, outputFile) => RawMods.PortToMac(input, outputFile),
            Core.ModOS.Android => (input, outputFile) => RawMods.PortToAndroid(input, outputFile),
            _ => throw new Exception("This should not have happened! new unhandled data!")
        };
        
        Assert.Throws<ArgumentNullException>(() => function.Invoke(null, "/foo"));
        Assert.Throws<FileNotFoundException>(() => function.Invoke("/foo", "/foo"));
        Assert.Throws<ArgumentOutOfRangeException>(() => function.Invoke("./GameLin.zip", null));

    }
    
    #endregion
    
    #region PortToAndroid
    
    [Theory]
    [InlineData("./GameWin.zip", false, false, false, false)]
    [InlineData("./GameLin.zip", false, false, false, false)]
    [InlineData("./GameMac.zip", false, false, false, false)]
    [InlineData("./GameWin.zip", true, true, true, true)]
    [InlineData("./GameLin.zip", true, true, true, true)]
    [InlineData("./GameMac.zip", true, true, true, true)]
    public void PortZipToAndroid(string inputZip, bool useSubdirectories, bool createWorkingDirectoryBeforeHand, bool useCustomSave, bool useInternet)
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
        
        RawMods.PortToAndroid(inputZip, outputZip, null, null, useCustomSave, useInternet);
        
        // HACK: STORE'd files aren't compressed, thus the compressed size is the same as the normal
        using (var archive = ZipFile.OpenRead(outputZip))
        {
            var entry = archive.GetEntry("assets/coolsong.ogg");
            Assert.Equal(entry.Length, entry.CompressedLength);
        }

        ZipFile.ExtractToDirectory(inputZip, origExtract);
        ZipFile.ExtractToDirectory(outputZip, newExtract);
        switch (origMod)
        {
            case Core.ModOS.Windows:
            {
                // File contents should be same between the zips except for all files being lowered now, data file being different, runner+dll not existing now, and splash being new
                var origFiles = new DirectoryInfo(origExtract).GetFiles().Select(f => f.Name.ToLower()).ToList();
                origFiles.Remove("am2r.exe");
                origFiles.Remove("d3dx9_43.dll");
                origFiles.Remove("data.win");
                origFiles.Add("game.droid");
                origFiles.Remove("am2r.exe");
                origFiles.Add("splash.png");
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            case Core.ModOS.Linux:
            {
                // File contents should be same between the zips except for data file being different
                var origFiles = new DirectoryInfo(origExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                origFiles.Remove("game.unx");
                origFiles.Add("game.droid");
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
                origFiles.Add("game.droid");
                origFiles.Remove("gamecontrollerdb.txt");
                origFiles.Remove("yoyorunner.config");
                origFiles.Sort();
                var newFiles = new DirectoryInfo(newExtract + "/assets").GetFiles().Select(f => f.Name).ToList();
                newFiles.Sort();
                Assert.True(origFiles.SequenceEqual(newFiles));
                break;
            }
            default: throw new Exception("unwritten test case for new os?");
        }
        
        // TODO: check save folder - probably needs to be done by decompiling again. If one does this, then the "useInternet" check below should also get redone
        if (useCustomSave)
        {
        }
        
        // HACK: ugly af, but works
        if (useInternet)
        {
            Assert.Contains("    a n d r o i d . p e r m i s s i o n ." +
                            " I N T E R N E T    a n d r o i d ." +
                            " p e r m i s s i o n . B L U E T O O T H", 
                            File.ReadAllText(newExtract + "/AndroidManifest.xml"));
        }
        
        // there should be four subdirs in root
        Assert.Equal(4, new DirectoryInfo(newExtract).GetDirectories().Length);
        
        // If we didn't specify any, there should be no subdirectories at the end in asset folder
        if (!useSubdirectories)
        {
            Assert.Empty(new DirectoryInfo(newExtract + "/assets/").GetDirectories());
            return;
        }
        
        //Otherwise there should be our stuff
        Assert.True(File.Exists(newExtract + "/assets/" + deepSuffix.ToLower() + origInput.ToLower()));
        
        // TODO: check whether final signature is correct?
    }
    
    [Theory]
    [InlineData("./GameWin.zip", "")]
    [InlineData("./GameWin.zip", "фыва")]
    [InlineData("./GameLin.zip", "")]
    [InlineData("./GameLin.zip", "фыва")]
    [InlineData("./GameMac.zip", "")]
    [InlineData("./GameMac.zip", "фыва")]
    public void HandleInvalidAndroidDisplayNames(string inputZip, string nameToTest)
    {
        var origMod = RawMods.GetModOSOfRawZip(inputZip);
        var outputZip = testTempDir + Guid.NewGuid();
        
        string assetFile = "data.win";
        if (origMod == Core.ModOS.Linux)
            assetFile = "assets/game.unx";
        else if (origMod == Core.ModOS.Mac)
            assetFile = "AM2R.app/Contents/Resources/game.ios";
            
        File.Copy(inputZip, testTempDir + inputZip + "_modified");
        inputZip = testTempDir + inputZip + "_modified";
        using (ZipArchive archive = ZipFile.Open(inputZip, ZipArchiveMode.Update))
        {
            var file = archive.GetEntry(assetFile);
            var outputFile = testTempDir + Guid.NewGuid();
            file.ExtractToFile(outputFile);
            // Read data file and change display name
            {
                UndertaleData gmData;
                using (FileStream fs = new FileInfo(outputFile).OpenRead())
                {
                    gmData = UndertaleIO.Read(fs);
                    var newName = gmData.Strings.MakeString(nameToTest);
                    gmData.GeneralInfo.DisplayName = newName;
                }

                using (FileStream fs = new FileInfo(outputFile).OpenWrite())
                {
                    UndertaleIO.Write(fs, gmData);
                }
            }
            file.Delete();
            archive.CreateEntryFromFile(outputFile, assetFile);
        }
        Assert.Throws<InvalidDataException>(() => RawMods.PortToAndroid(inputZip, outputZip, null, null, true));
    }
    
    
    #endregion

    #region Check proper icons after porting

    [Theory]
    [InlineData("./GameWin.zip", Core.ModOS.Linux)]
    [InlineData("./GameLin.zip", Core.ModOS.Linux)]
    [InlineData("./GameMac.zip", Core.ModOS.Linux)]
    [InlineData("./GameWin.zip", Core.ModOS.Mac)]
    [InlineData("./GameLin.zip", Core.ModOS.Mac)]
    [InlineData("./GameMac.zip", Core.ModOS.Mac)]
    public void CheckThatUnixPortHasProperIcons(string inputZip, Core.ModOS os)
    {
        const string icon = "icon.png";
        const string splash = "splash.png";
        string assetSuffix;
        Action<string, string, string?, string?> function;

        switch (os)
        {
            case Core.ModOS.Linux:
                assetSuffix = "/assets/";
                function = (inp, outp, ic, spl) => RawMods.PortToLinux(inp, outp, ic, spl);
                break;
            case Core.ModOS.Mac:
                assetSuffix = "/AM2R.app/Contents/Resources/";
                function = (inp, outp, ic, spl) => RawMods.PortToMac(inp, outp, ic, spl);
                break;
            default: throw new Exception("was called with unimplemented os");
        }
        
        var outputZip = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid() + "/";
        
        // With default icons
        void CheckIconsWithPath(string? path)
        {
            File.Delete(outputZip);
            function.Invoke(inputZip, outputZip, path, path);
            if (Directory.Exists(newExtract))
                Directory.Delete(newExtract, true);
            ZipFile.ExtractToDirectory(outputZip, newExtract);
            var newIcon = File.ReadAllBytes(newExtract + assetSuffix + icon);
            var newSplash = File.ReadAllBytes(newExtract + assetSuffix + splash);
            Directory.CreateDirectory(libTempDir);
            var oldIcon = File.ReadAllBytes(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), path));
            var oldSplash = File.ReadAllBytes(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.splash), path));

            Assert.True(newIcon.SequenceEqual(oldIcon));
            Assert.True(newSplash.SequenceEqual(oldSplash));
        }
        
        CheckIconsWithPath(null);
        CheckIconsWithPath(inputZip);
    }
    
    // TODO: see skip reason
    [Theory(Skip = "Currently buggy, due to probably an apktool bug.")]
    [InlineData("./GameWin.zip")]
    [InlineData("./GameLin.zip")]
    [InlineData("./GameMac.zip")]
    public void CheckThatAndroidHasProperIcons(string inputZip)
    {
        const string splash = "splash.png";
        string assetSuffix = "/assets/";
        Dictionary<string, int> resPaths = new Dictionary<string, int>
        {
            {"/res/drawable/icon.png", 96},
            {"/res/drawable-hdpi-v4/icon.png", 72},
            {"/res/drawable-ldpi-v4/icon.png", 36},
            {"/res/drawable-mdpi-v4/icon.png", 48},
            {"/res/drawable-xhdpi-v4/icon.png", 96},
            {"/res/drawable-xxhdpi-v4/icon.png", 144},
            {"/res/drawable-xxxhdpi-v4/icon.png", 192}
        };
        
        var outputZip = testTempDir + Guid.NewGuid();
        var newExtract = testTempDir + Guid.NewGuid() + "/";
        
        // With default icons
        void CheckIconsWithPath(string? path)
        {
            File.Delete(outputZip);
            RawMods.PortToAndroid(inputZip, outputZip, path, path);
            if (Directory.Exists(newExtract))
                Directory.Delete(newExtract, true);
            ZipFile.ExtractToDirectory(outputZip, newExtract);
            var newSplash = File.ReadAllBytes(newExtract + assetSuffix + splash);
            Directory.CreateDirectory(libTempDir);
            var oldSplash = File.ReadAllBytes(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.splash), path));
            Assert.True(newSplash.SequenceEqual(oldSplash));
            foreach (var kvp in resPaths)
            {
                var sizedPath = testTempDir + "/" + Guid.NewGuid() +".png";
                var newIcon = File.ReadAllBytes(newExtract + kvp.Key);
                var oldIconPath = RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), path);
                
                HelperMethods.SaveAndroidIcon(oldIconPath, kvp.Value, sizedPath);
                var oldIcon = File.ReadAllBytes(sizedPath);
                Assert.True(newIcon.SequenceEqual(oldIcon));
            }

        }
        
        CheckIconsWithPath(null);
        CheckIconsWithPath(inputZip);
    }
    
    #endregion
    
    #region Make sure porting methods work when called in succession
    
    [Theory]
    [InlineData("./GameWin.zip", Core.ModOS.Windows)]
    [InlineData("./GameWin.zip", Core.ModOS.Linux)]
    [InlineData("./GameWin.zip", Core.ModOS.Mac)]
    [InlineData("./GameWin.zip", Core.ModOS.Android)]
    [InlineData("./GameLin.zip", Core.ModOS.Windows)]
    [InlineData("./GameLin.zip", Core.ModOS.Linux)]
    [InlineData("./GameLin.zip", Core.ModOS.Mac)]
    [InlineData("./GameLin.zip", Core.ModOS.Android)]
    [InlineData("./GameMac.zip", Core.ModOS.Windows)]
    [InlineData("./GameMac.zip", Core.ModOS.Linux)]
    [InlineData("./GameMac.zip", Core.ModOS.Mac)]
    [InlineData("./GameMac.zip", Core.ModOS.Android)]
    public void TestPortToOSMultipleTimes(string input, Core.ModOS os)
    {
        Action<string?, string?> function = os switch
        {
            Core.ModOS.Windows => (inputFile, outputFile) => RawMods.PortToWindows(inputFile, outputFile),
            Core.ModOS.Linux => (inputFile, outputFile) => RawMods.PortToLinux(inputFile, outputFile),
            Core.ModOS.Mac => (inputFile, outputFile) => RawMods.PortToMac(inputFile, outputFile),
            Core.ModOS.Android => (inputFile, outputFile) => RawMods.PortToAndroid(inputFile, outputFile), 
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
}