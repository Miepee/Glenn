using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using UndertaleModLib;
using static AM2RPortHelperLib.Core;

namespace AM2RPortHelperLib;

public abstract class RawModsBase : ModsBase
{
    // For completionist sake, it should be possible to also port raw APKs to win/lin/mac
    // But until some person actually shows up that needs this feature, I'm too lazy to implement it
    
    /// <summary>
    /// Determines for which OS a raw mod zip was made for. 
    /// </summary>
    /// <param name="inputRawZipPath">The path to the raw mod zip.</param>
    /// <returns>The OS for which the zip was made for as <see cref="ModOS"/>.</returns>
    /// <exception cref="NotSupportedException">The OS for which the zip was made for could not be determined.</exception>
    public static ModOS GetModOSOfRawZip(string inputRawZipPath)
    {
        ZipArchive archive = ZipFile.OpenRead(inputRawZipPath);
        if (archive.Entries.Any(f => f.FullName == "AM2R.exe") && archive.Entries.Any(f => f.FullName == "data.win"))
            return ModOS.Windows;
        
        if (archive.Entries.Any(f => f.FullName == "runner") && archive.Entries.Any(f => f.FullName == "assets/game.unx"))
            return ModOS.Linux;
        
        // I probably *should* use fullpaths for these, but the .app file could technically be different and don't want to thinka bout how to circumvent it
        if (archive.Entries.Any(f => f.FullName.Contains("Contents/MacOS/Mac_Runner")) && archive.Entries.Any(f => f.FullName.Contains("Contents/Resources/game.ios")))
            return ModOS.Mac;
        
        throw new NotSupportedException("The OS of the mod zip is unknown and thus not supported");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="nameOfResource"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    private static string GetProperPathToBuiltinIcons(string nameOfResource)
    {
        string SubCaseFunction(string resource)
        {
            string origPath = ConfigDir + "/" + resource;
            if (File.Exists(origPath))
                return origPath;

            var byteArray = resource switch
            {
                nameof(Resources.icon) + ".png" => Resources.icon,
                nameof(Resources.splash) + ".png" => Resources.splash,
                nameof(Resources.splashAndroid) + ".png" => Resources.splashAndroid,
                _ => throw new InvalidDataException("SubCaseFunction was called with an improper resource!")
            };
            
            Image.Load(byteArray).SaveAsPng(TempDir + "/" + resource);
            origPath = TempDir + "/" + resource;
            return origPath;
        }
        
        switch (nameOfResource)
        {
            case nameof(Resources.icon):
                return SubCaseFunction(nameof(Resources.icon) + ".png");
            case nameof(Resources.splash):
                return SubCaseFunction(nameof(Resources.splash) + ".png");
            case nameof(Resources.splashAndroid):
                return SubCaseFunction(nameof(Resources.splashAndroid) + ".png");
            default: throw new InvalidDataException(nameOfResource + " is an unknown Icon!");
        }
    }

    // TODO: Port to Windows
    public static void PortToWindows(string inputRawZipPath, string outputRawZipPath, OutputHandlerDelegate outputHandlerDelegate = null)
    {
        
    }
    
    /// <summary>
    /// Ports a raw AM2R mod zip for Linux. 
    /// </summary>
    /// <param name="inputRawZipPath">The path to the raw mod zip.</param>
    /// <param name="outputRawZipPath">The path where the ported Linux mod zip should be saved to.</param>
    /// <param name="outputDelegate">A delegate to post output info to.</param>
    /// <exception cref="NotSupportedException">The raw mod zip was made for an OS that can't be determined.</exception>
    public static void PortToLinux(string inputRawZipPath, string outputRawZipPath, OutputHandlerDelegate outputDelegate = null)
    {
        ModOS currentOS = GetModOSOfRawZip(inputRawZipPath);
        SendOutput("Zip Recognized as " + currentOS);

        if (currentOS == ModOS.Linux)
        {
            SendOutput("Zip is already a raw Linux zip. Copying to output directory...");
            File.Copy(inputRawZipPath, outputRawZipPath, true);
            return;
        }

        OutputHandler = outputDelegate;
        string extractDirectory = TempDir + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string assetsDir = extractDirectory + "/assets";

        // Check if temp folder exists, delete if yes, extract zip to there
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        SendOutput("Extracting for Raw Linux...");
        Directory.CreateDirectory(assetsDir);
        ZipFile.ExtractToDirectory(inputRawZipPath, assetsDir);
        
        // Delete unnecessary files, rename data.win, move in the new runner
        SendOutput("Delete unnecessary files for Linux and lowercase them...");
        switch (currentOS)
        {
            case ModOS.Windows:
                File.Delete(assetsDir + "/AM2R.exe");
                File.Delete(assetsDir + "/D3DX9_43.dll");
                File.Move(assetsDir + "/data.win", assetsDir + "/game.unx");
                break;
            case ModOS.Mac:
                var appDir = new DirectoryInfo(assetsDir).GetDirectories().First(n => n.Name.EndsWith(".app"));
                HelperMethods.DirectoryCopy(assetsDir + "/" + appDir.Name + "/Contents/Resources", assetsDir);
                File.Delete(assetsDir + "/gamecontrollerdb.txt");
                File.Delete(assetsDir + "/yoyorunner.config");
                Directory.Delete(assetsDir + "/English.lproj", true);
                Directory.Delete(assetsDir + "/" + appDir.Name, true);
                File.Move(assetsDir + "/game.ios", assetsDir + "/game.unx");
                break;
            default: throw new NotSupportedException("The OS of the mod zip is unknown and thus not supported");
        }
        
        File.Copy(UtilDir + "/runner", extractDirectory + "/runner");
        if (!File.Exists(assetsDir + "/icon.png"))
            File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.icon)), assetsDir + "/icon.png");
        if (!File.Exists(assetsDir + "/splash.png"))
            File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.splash)), assetsDir + "/splash.png");

        //recursively lowercase everything in the assets folder
        HelperMethods.LowercaseFolder(assetsDir);

        //zip the result
        SendOutput("Creating raw Linux zip...");
        ZipFile.CreateFromDirectory(extractDirectory, outputRawZipPath);

        // Clean up
        Directory.Delete(TempDir, true);
    }
    
    /// <summary>
    /// Ports a raw AM2R mod zip for Android.
    /// </summary>
    /// <param name="inputRawZipPath">The path to the raw mod zip.</param>
    /// <param name="outputRawApkPath">The path where the ported Android mod apk should be saved to.</param>
    /// <param name="useCustomSaveDirectory">Whether the mod should use a custom save location on Android.</param>
    /// <param name="usesInternet">Whether the mod needs an Internet connection.</param>
    /// <param name="outputDelegate">A delegate to post output info to.</param>
    /// <exception cref="NotSupportedException">The raw mod zip was made for an OS that can't be determined.</exception>
    /// <exception cref="InvalidDataException"><paramref name="useCustomSaveDirectory"/> was given, but the display name of the mod is unsuitable as a name for the directory.</exception>
    public static void PortToAndroid(string inputRawZipPath, string outputRawApkPath, bool useCustomSaveDirectory = false, bool usesInternet = false, OutputHandlerDelegate outputDelegate = null)
    {
        ModOS currentOS = GetModOSOfRawZip(inputRawZipPath);
        SendOutput("Zip Recognized as " + currentOS);
        
        OutputHandler = outputDelegate;
        string extractDirectory = TempDir + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string apkDir = extractDirectory + "/apk";
        string apkAssetsDir = apkDir + "/assets";
        string bin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "java";
        string args = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/C java -jar " : "-jar ";
        string apktool = CurrentDir + "/utils/apktool.jar";
        string signer = CurrentDir + "/utils/uber-apk-signer.jar";
        string finalApkBuild = extractDirectory + "/build-aligned-debugSigned.apk";

        // Check if temp folder exists, delete if yes, extract zip to there
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        Directory.CreateDirectory(extractDirectory);

        // Run APKTOOL and decompress the file
        SendOutput("Decompiling apk...");
        ProcessStartInfo pStartInfo = new ProcessStartInfo
        {
            FileName = bin,
            Arguments = args + "\"" + apktool + "\" d -f -o \"" + apkDir + "\" \"" + UtilDir + "/AM2RWrapper.apk" + "\"",
            CreateNoWindow = true
        };
        Process p = new Process { StartInfo = pStartInfo };
        p.Start();
        p.WaitForExit();
        
        SendOutput("Extracting for Raw Android...");
        ZipFile.ExtractToDirectory(inputRawZipPath, apkAssetsDir);
        
        // Delete unnecessary files, rename data.win, move in the new runner
        SendOutput("Delete unnecessary files for Android and lowercase them...");
        switch (currentOS)
        {
            case ModOS.Windows:
                File.Delete(apkAssetsDir + "/AM2R.exe");
                File.Delete(apkAssetsDir + "/D3DX9_43.dll");
                File.Move(apkAssetsDir + "/data.win", apkAssetsDir + "/game.droid");
                break;
            case ModOS.Linux:
                File.Delete(apkAssetsDir + "/runner");
                HelperMethods.DirectoryCopy(apkAssetsDir + "/assets", apkAssetsDir);
                Directory.Delete(apkAssetsDir + "/assets", true);
                File.Move(apkAssetsDir + "/game.unx", apkAssetsDir + "/game.droid");
                break;
            case ModOS.Mac:
                var appDir = new DirectoryInfo(apkAssetsDir).GetDirectories().First(n => n.Name.EndsWith(".app"));
                HelperMethods.DirectoryCopy(apkAssetsDir + "/" + appDir.Name + "/Contents/Resources", apkAssetsDir);
                File.Delete(apkAssetsDir + "/gamecontrollerdb.txt");
                File.Delete(apkAssetsDir + "/yoyorunner.config");
                Directory.Delete(apkAssetsDir + "/English.lproj", true);
                Directory.Delete(apkAssetsDir + "/" + appDir.Name, true);
                File.Move(apkAssetsDir + "/game.ios", apkAssetsDir + "/game.droid");
                break;
            default: throw new NotSupportedException("The OS of the mod zip is unknown and thus not supported");
        }
        
        // The wrapper always has a splash image, so we want to overwrite it.
        File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.splashAndroid)), apkAssetsDir + "/splash.png", true);

        //recursively lowercase everything in the assets folder
        HelperMethods.LowercaseFolder(apkAssetsDir);

        // Edit apktool.yml to not compress music
        string yamlFile = File.ReadAllText(apkDir + "/apktool.yml");
        yamlFile = yamlFile.Replace("doNotCompress:", "doNotCompress:\n- ogg");
        File.WriteAllText(apkDir + "/apktool.yml", yamlFile);

        // Edit the icons in the apk. Wrapper always has these, so we need to overwrite these too.
        string resPath = apkDir + "/res";
        // Icon should only be read from if its there, otherwise default frog icon should be in the assembly
        string origPath = GetProperPathToBuiltinIcons(nameof(Resources.icon));
        HelperMethods.SaveAndroidIcon(origPath, 96, resPath + "/drawable/icon.png");
        HelperMethods.SaveAndroidIcon(origPath, 72, resPath + "/drawable-hdpi-v4/icon.png");
        HelperMethods.SaveAndroidIcon(origPath, 36, resPath + "/drawable-ldpi-v4/icon.png");
        HelperMethods.SaveAndroidIcon(origPath, 48, resPath + "/drawable-mdpi-v4/icon.png");
        HelperMethods.SaveAndroidIcon(origPath, 96, resPath + "/drawable-xhdpi-v4/icon.png");
        HelperMethods.SaveAndroidIcon(origPath, 144, resPath + "/drawable-xxhdpi-v4/icon.png");
        HelperMethods.SaveAndroidIcon(origPath, 192, resPath + "/drawable-xxxhdpi-v4/icon.png");
        
        // On certain occasions, we need to modify the manifest file.
        if (useCustomSaveDirectory || usesInternet)
        {
            string manifestFile = File.ReadAllText(apkDir + "/AndroidManifest.xml");

            // If a custom name was given, replace it everywhere.
            if (useCustomSaveDirectory)
            {
                string modName;
                FileInfo datafile = new FileInfo(extractDirectory + "/game.ios");
                using (FileStream fs = datafile.OpenRead())
                {
                    UndertaleData gmData = UndertaleIO.Read(fs, SendOutput, SendOutput);
                    modName = gmData.GeneralInfo.DisplayName.Content;
                }
                modName = modName.Replace(" ", "").Replace(":", "");
                
                // rules for name: A-Z, a-z, digits, underscore and needs to start with letters
                Regex nameReg = new Regex(@"^[a-zA-Z][a-zA-Z0-9_]*$");
                if (!nameReg.Match(modName).Success)
                    throw new InvalidDataException("The display name " + modName + " is invalid! The name has to start with letters (a-z), and can only contain letters, digits, space, colon and underscore!");
                
                // first in the manifest
                manifestFile = manifestFile.Replace("com.companyname.AM2RWrapper", $"com.companyname.{modName}");
                
                // then in the rest
                string AndroidIdReplace(string content)
                {
                    return content.Replace("com.companyname.AM2RWrapper", $"com.companyname.{modName}")
                        .Replace("com/companyname/AM2RWrapper", $"com/companyname/{modName}")
                        .Replace("com$companyname$AM2RWrapper", $"com$companyname${modName}");
                }
                foreach (var file in Directory.GetFiles($"{apkDir}/smali/com/yoyogames/runner"))
                {
                    var content = File.ReadAllText(file);
                    content = AndroidIdReplace(content);
                    File.WriteAllText(file, content);
                }
                var am2rWrapperDir = new DirectoryInfo($"{apkDir}/smali/com/companyname/AM2RWrapper");
                foreach (var file in am2rWrapperDir.GetFiles())
                {
                    var content = File.ReadAllText(file.FullName);
                    content = AndroidIdReplace(content);
                    File.WriteAllText(file.FullName, content);
                }
                am2rWrapperDir.MoveTo($"{apkDir}/smali/com/companyname/{modName}");

                var layoutContent = File.ReadAllText($"{apkDir}/res/layout/main.xml");
                layoutContent = AndroidIdReplace(layoutContent);
                File.WriteAllText($"{apkDir}/res/layout/main.xml", layoutContent);
            }

            // Add internet permission, keying off the Bluetooth permission.
            if (usesInternet)
            {
                const string bluetoothPermission = "<uses-permission android:name=\"android.permission.BLUETOOTH\"/>";
                const string internetPermission = "<uses-permission android:name=\"android.permission.INTERNET\"/>";
                manifestFile = manifestFile.Replace(bluetoothPermission, internetPermission + "\n    " + bluetoothPermission);
            }
            File.WriteAllText(apkDir + "/AndroidManifest.xml", manifestFile);
        }

        // Run APKTOOL and build the apk
        SendOutput("Rebuild apk...");
        pStartInfo = new ProcessStartInfo
        {
            FileName = bin,
            Arguments = args + "\"" + apktool + "\" b \"" + apkDir + "\" -o \"" + extractDirectory + "/build.apk" + "\"",
            CreateNoWindow = true
        };
        p = new Process { StartInfo = pStartInfo };
        p.Start();
        p.WaitForExit();

        // Sign the apk
        SendOutput("Sign apk...");
        pStartInfo = new ProcessStartInfo
        {
            FileName = bin,
            Arguments = args + "\"" + signer + "\" -a \"" + extractDirectory + "/build.apk" + "\"",
            CreateNoWindow = true
        };
        p = new Process { StartInfo = pStartInfo };
        p.Start();
        p.WaitForExit();

        //Move apk
        File.Move(finalApkBuild, outputRawApkPath);

        // Clean up
        Directory.Delete(TempDir, true);
    }
    
    /// <summary>
    /// Ports a raw AM2R mod zip for macOS.
    /// </summary>
    /// <param name="inputRawZipPath">The path to the raw mod zip.</param>
    /// <param name="outputRawZipPath">he path where the ported Mac mod zip should be saved to.</param>
    /// <param name="outputDelegate">A delegate to post output info to.</param>
    /// <exception cref="NotSupportedException">The raw mod zip was made for an OS that can't be determined.</exception>
    public static void PortToMac(string inputRawZipPath, string outputRawZipPath, OutputHandlerDelegate outputDelegate = null)
    {
        ModOS currentOS = GetModOSOfRawZip(inputRawZipPath);
        SendOutput("Zip Recognized as " + currentOS);

        if (currentOS == ModOS.Mac)
        {
            SendOutput("Zip is already a raw Mac zip. Copying to output dir...");
            File.Copy(inputRawZipPath, outputRawZipPath, true);
            return;
        }
        
        OutputHandler = outputDelegate;
        string baseTempDirectory = TempDir + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string extractDirectory = baseTempDirectory + "/extract";
        string appDirectory = baseTempDirectory + "/AM2R.app";
        string contentsDir = baseTempDirectory + "/Contents";
        string assetsDir = contentsDir + "/Resources";
        
        // Check if temp folder exists, delete if yes, copy bare runner to there
        if (Directory.Exists(baseTempDirectory))
            Directory.Delete(baseTempDirectory, true);
        SendOutput("Copying Mac Runner...");
        Directory.CreateDirectory(contentsDir);
        HelperMethods.DirectoryCopy(UtilDir + "/Contents", contentsDir);

        // Extract mod to temp location
        SendOutput("Extracting Mac...");
        ZipFile.ExtractToDirectory(inputRawZipPath, extractDirectory);

        // Delete unnecessary files, rename data.win, move in the new runner
        SendOutput("Delete unnecessary files for Mac and lowercase them...");
        switch (currentOS)
        {
            case ModOS.Windows:
                File.Delete(assetsDir + "/AM2R.exe");
                File.Delete(assetsDir + "/D3DX9_43.dll");
                File.Move(assetsDir + "/data.win", assetsDir + "/game.ios");
                break;
            case ModOS.Linux:
                File.Delete(assetsDir + "/runner");
                HelperMethods.DirectoryCopy(assetsDir + "/assets", assetsDir);
                Directory.Delete(assetsDir + "/assets", true);
                File.Move(assetsDir + "/game.unx", assetsDir + "/game.ios");
                break;
            default: throw new NotSupportedException("The OS of the mod zip is unknown and thus not supported");
        }

        // TODO: do we really want to keep their images?
        if (!File.Exists(assetsDir + "/icon.png"))
            File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.icon)), extractDirectory + "/icon.png");
        if (!File.Exists(assetsDir + "/splash.png"))
            File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.splash)), extractDirectory + "/splash.png");
        
        // Delete fonts folder if it exists, because I need to convert bytecode version from game and newer version doesn't support font loading
        if (Directory.Exists(extractDirectory + "/lang/fonts"))
            Directory.Delete(extractDirectory + "/lang/fonts", true);

        // Lowercase every file first
        HelperMethods.LowercaseFolder(extractDirectory);

        // Convert data.win to BC16 and get rid of not needed functions anymore
        SendOutput("Editing data.win to change ByteCode version and functions...");
        string bin;
        string args;

        // TODO: replace this via built-in lib
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            bin = "\"" + UtilDir + "/UTMTCli/UndertaleModCli.exe\"";
            args = "";
        }
        else
        {
            // First chmod the file, just in case
            Process.Start("chmod", "+x \"" + UtilDir + "/UTMTCli/UndertaleModCli.dll\"");
            bin = "dotnet";
            args = "\"" + UtilDir + "/UTMTCli/UndertaleModCli.dll\" ";
            // Also chmod the runner. Just in case.
            Process.Start("chmod", "+x \"" + contentsDir + "/MacOS/Mac_Runner");
        }

        ProcessStartInfo pStartInfo = new ProcessStartInfo
        {
            FileName = bin,
            Arguments = args + "load \"" + extractDirectory + "/game.ios\" -s \"" + UtilDir + "/bc16AndRemoveFunctions.csx\" -o \"" + extractDirectory + "/game.ios\"",
            CreateNoWindow = false
        };
        Process p = new Process { StartInfo = pStartInfo };
        p.Start();
        p.WaitForExit();

        // Copy assets to the place where they belong to
        SendOutput("Copy files over...");
        HelperMethods.DirectoryCopy(extractDirectory, assetsDir);

        // Edit config and plist to change display name
        string modName;
        FileInfo datafile = new FileInfo(extractDirectory + "/game.ios");
        using (FileStream fs = datafile.OpenRead())
        {
            UndertaleData gmData = UndertaleIO.Read(fs, SendOutput, SendOutput);
            modName = gmData.GeneralInfo.DisplayName.Content;
        }
        // Escape invalid xml characters
        modName = SecurityElement.Escape(modName);
        SendOutput("Editing Runner references to AM2R...");
        string textFile = File.ReadAllText(assetsDir + "/yoyorunner.config");
        textFile = textFile.Replace("YoYo Runner", modName);
        File.WriteAllText(assetsDir + "/yoyorunner.config", textFile);

        textFile = File.ReadAllText(contentsDir + "/Info.plist");
        textFile = textFile.Replace("YoYo Runner", modName);
        File.WriteAllText(contentsDir + "/Info.plist", textFile);

        // Create a .app directory and move contents in there
        Directory.CreateDirectory(appDirectory);
        Directory.Move(contentsDir, appDirectory + "/Contents");

        Directory.Delete(extractDirectory, true);

        //zip the result
        SendOutput("Creating Mac zip...");
        ZipFile.CreateFromDirectory(baseTempDirectory, outputRawZipPath);

        // Clean up
        Directory.Delete(TempDir, true);
    }
}