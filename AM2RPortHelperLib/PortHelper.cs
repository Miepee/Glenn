using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace AM2RPortHelperLib;

public enum LauncherModTargets
{
    Windows,
    Linux,
    Mac
}

public static partial class PortHelper
{    
    /// <summary>
    /// The current version of <see cref="AM2RPortHelperLib"/>.
    /// </summary>
    public const string Version = "1.4";
    public delegate void OutputHandlerDelegate(string output);

    private static OutputHandlerDelegate outputHandler;

    private static void SendOutput(string output)
    {
        outputHandler?.Invoke(output);
    }

    /// <summary>
    /// A temporary directory
    /// </summary>
    private static readonly string tmp = Path.GetTempPath();
    
    /// <summary>
    /// The current directory of the AM2RPortHelper program.
    /// </summary>
    private static readonly string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
    
    /// <summary>
    /// The "utils" folder that's shipped with the AM2RPortHelper.
    /// </summary>
    private static readonly string utilDir = currentDir + "/utils";

    /// <summary>
    /// Ports a Mod zip intended to be installed via the AM2RLauncher to other operating systems.
    /// </summary>
    /// <param name="inputLauncherZipPath">The path to the AM2RLauncher mod zip that should be ported.</param>
    /// <param name="targetOS">The target operating system to port the </param>
    /// <param name="includeAndroid">Whether Android should be inlcuded in the port.</param>
    /// <param name="outputLauncherZipPath">The path where the ported AM2RLauncher mod zip should be saved.</param>
    /// <param name="am2r11ZipPath">The path to an AM2R 1.1 zip path. This is *required* if the input launcher zip is for Mac and will be ignored if the input zip is for anything else.</param>
    /// <param name="outputDelegate">The function that should handle in-progress output messages.</param>
    /// <exception cref="NotSupportedException">WIP</exception>
    /// TODO: other exceptions
    public static void PortLauncherMod(string inputLauncherZipPath, LauncherModTargets targetOS, bool includeAndroid, string outputLauncherZipPath, string am2r11ZipPath = null, OutputHandlerDelegate outputDelegate = null)
    {
        outputHandler = outputDelegate;  
        string extractDirectory = tmp + "/" + Path.GetFileNameWithoutExtension(inputLauncherZipPath);
        string filesToCopyDir = extractDirectory + "/files_to_copy";
        
        // Check if temp folder exists, delete if yes, extract zip to there
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        SendOutput("Extracting Launcher mod...");
        ZipFile.ExtractToDirectory(inputLauncherZipPath, extractDirectory);

        var profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(extractDirectory + "/profile.xml"));
        if (profile.UsesYYC)
            throw new NotSupportedException("Launcher Mod is YYC, cannot port!");
        
        string currentOS = profile.OperatingSystem;
        bool isAndroidIncluded = profile.SupportsAndroid;

        if (targetOS.ToString() == profile.OperatingSystem)
        {
            SendOutput("Target OS and Launcher OS are the same; exiting.");
            return;
        }

        // Run sha256 hash on runner to see if it's supported!        
        string runnerHash = CalculateSHA256(extractDirectory + "/AM2R.xdelta");
        string[] allowedHashes = new[] { "b78c4fd2dc481f97b60440a5c89786da284b4aaeeba9fb2e3b48ac369cfe50d5", "243509f4270f448411c8405b71d7bc4f5d4fe5f3ecc1638d9c1218bf76b69f1f", "852b9a9466f99a53260b8147c6d286b81c145b2c10b00bb5c392b40b035811b5"};
        // Don't check has on Windows, because the runner there has icons embedded in it, screwing off the hashes
        // TODO: find a way around that
        if (!(profile.OperatingSystem == "Windows" || allowedHashes.Contains(runnerHash)))
            throw new NotSupportedException("Invalid GM:S version! Porting Launcher mods is only supported for mods build with GM:S 1.4.1763!");
        
        // TODO: Not sure if this is ever gonna be possible, since it requires one to shift back the patch.
        // We'd need a 1.1 file to apply the patch to, run that with umtlib to shift it back, and then apply a new patch.
        if (profile.OperatingSystem == "Mac")
            throw new NotSupportedException("Porting Mac mods is currently not supported!");
        
        switch (targetOS)
        {
            case LauncherModTargets.Windows:
            {
                // We have a non-windows launcher mod, where data file patch is guaranteed to be game.xdelta
                File.Move(extractDirectory + "/game.xdelta", extractDirectory + "/data.xdelta");
                
                // get proper runner
                File.Delete(extractDirectory + "/AM2R.xdelta");
                File.Copy(utilDir + "/windowsRunner.xdelta", extractDirectory + "/AM2R.xdelta");
                
                // Windows doesn't care about capitalization and because I can't predict how it originally was, I'm going to ignore it.
                
                // Windows doesn't have icons/splashes, so we remove those if they exist.
                if (!File.Exists(filesToCopyDir + "/icon.png"))
                    File.Delete(filesToCopyDir + "/icon.png");
                if (!File.Exists(filesToCopyDir + "/splash.png"))
                    File.Delete(filesToCopyDir + "/splash.png");

                // Properly set profile.xml variables.
                profile.OperatingSystem = "Windows";
                profile.SaveLocation = currentOS switch
                {
                    "Linux" => profile.SaveLocation.Replace("~/.config", "%localappdata%"),
                    "Mac" => profile.SaveLocation.Replace("~/Library/Application Support", "%localappdata%"),
                    _ => throw new NotSupportedException("Unsupported OS: " + currentOS)
                };
                File.WriteAllText(extractDirectory + "/profile.xml",Serializer.Serialize<ProfileXML>(profile));
                break;
            }
            
            case LauncherModTargets.Linux:
            {
                if (currentOS == "Windows")
                    File.Move(extractDirectory + "/data.xdelta", extractDirectory + "/game.xdelta");

                // get proper runner
                File.Delete(extractDirectory + "/AM2R.xdelta");
                File.Copy(utilDir + "/linuxRunner.xdelta", extractDirectory + "/AM2R.xdelta");
                
                // Linux needs everything lowercased. Only needed if we're coming from Windows
                if (currentOS == "Windows")
                    LowercaseFolder(extractDirectory + "/files_to_copy");                  
                
                // Windows doesn't have icon/splash, so we copy them over from here
                if (!File.Exists(filesToCopyDir + "/icon.png"))
                    File.Copy(utilDir + "/icon.png", filesToCopyDir + "/icon.png");
                if (!File.Exists(filesToCopyDir + "/splash.png"))
                    File.Copy(utilDir + "/splash.png", filesToCopyDir + "/splash.png");

                // Properly set profile.xml variables
                profile.OperatingSystem = "Linux";
                profile.SaveLocation = currentOS switch
                {
                    "Windows" => profile.SaveLocation.Replace("%localappdata%", "~/.config"),
                    "Mac" => profile.SaveLocation.Replace("~/Library/Application Support", "~/.config"),
                    _ => throw new NotSupportedException("Unsupported OS " + currentOS)
                };
                File.WriteAllText(extractDirectory + "/profile.xml",Serializer.Serialize<ProfileXML>(profile)); 
                break;
            }

            case LauncherModTargets.Mac:
            {
                // TODO: Not sure if this is ever gonna be possible, since it requires one to shift up the patch.
                // We'd need a 1.1 file to apply the patch to, run that with umtlib to shift it up, and then apply a new patch.
                throw new NotSupportedException("Porting Mac mods is currently not supported!");
            }
            default: throw new ArgumentOutOfRangeException(nameof(targetOS), targetOS, "Unknown target to port to!");
        }

        if (!includeAndroid)
        {
            if (File.Exists(extractDirectory + "/droid.xdelta"))
                File.Delete(extractDirectory + "/droid.xdelta");
            if (Directory.Exists(extractDirectory + "/android")) 
                Directory.Delete(extractDirectory + "/android", true);
            profile.SupportsAndroid = false;
        }
        else
        {
            // If APK is not there, we need to create the APK ourselves.
            if (!isAndroidIncluded)
            {
                //TODO: see above         
            }
            profile.SupportsAndroid = true;
        }
        
        //zip the result
        SendOutput($"Creating Launcher zip for {targetOS}...");
        ZipFile.CreateFromDirectory(extractDirectory, outputLauncherZipPath);

        // Clean up
        Directory.Delete(extractDirectory, true);
    }
    
    // TODO: Make these not windows -> OS, but Raw -> OS
    public static void PortWindowsToLinux(string inputRawZipPath, string outputRawZipPath, OutputHandlerDelegate outputDelegate = null)
    {
        outputHandler = outputDelegate;
        string extractDirectory = tmp + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string assetsDir = extractDirectory + "/assets";

        // Check if temp folder exists, delete if yes, extract zip to there
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        SendOutput("Extracting Linux...");
        ZipFile.ExtractToDirectory(inputRawZipPath, extractDirectory);

        // Move everything into assets folder
        SendOutput("Moving into Linux assets folder...");
        Directory.CreateDirectory(assetsDir);
        foreach (var file in new DirectoryInfo(extractDirectory).GetFiles())
            file.MoveTo(assetsDir + "/" + file.Name);

        foreach (var dir in new DirectoryInfo(extractDirectory).GetDirectories())
        {
            if (dir.Name == "assets") continue;
            dir.MoveTo(assetsDir + "/" + dir.Name);
        }

        // Delete unnecessary files, rename data.win, move in the new runner
        SendOutput("Delete unnecessary files for Linux and lowercase them...");
        File.Delete(assetsDir + "/AM2R.exe");
        File.Delete(assetsDir + "/D3DX9_43.dll");
        File.Move(assetsDir + "/data.win", assetsDir + "/game.unx");
        File.Copy(utilDir + "/runner", extractDirectory + "/runner");
        if (!File.Exists(assetsDir + "/icon.png"))
            File.Copy(utilDir + "/icon.png", assetsDir + "/icon.png");
        if (!File.Exists(assetsDir + "/splash.png"))
            File.Copy(utilDir + "/splash.png", assetsDir + "/splash.png");

        //recursively lowercase everything in the assets folder
        LowercaseFolder(assetsDir);

        //zip the result
        SendOutput("Creating Linux zip...");
        ZipFile.CreateFromDirectory(extractDirectory, outputRawZipPath);

        // Clean up
        Directory.Delete(assetsDir, true);
    }

    // TODO: try to figure out if its possible to extract the name from the data.win file and then just offer a "use custom save directory" option that decides whether to use it or not.
    public static void PortWindowsToAndroid(string inputRawZipPath, string outputRawApkPath, string modName = null, bool usesInternet = false, OutputHandlerDelegate outputDelegate = null)
    {
        outputHandler = outputDelegate;
        string extractDirectory = tmp + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string unzipDir = extractDirectory + "/zip";
        string apkDir = extractDirectory + "/apk";
        string apkAssetsDir = apkDir + "/assets";
        string bin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "java";
        string args = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/C java -jar " : "-jar ";
        string apktool = currentDir + "/utils/apktool.jar";
        string signer = currentDir + "/utils/uber-apk-signer.jar";
        string finalApkBuild = extractDirectory + "/build-aligned-debugSigned.apk";

        // Check if temp folder exists, delete if yes, extract zip to there
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        Directory.CreateDirectory(extractDirectory);
        SendOutput("Extracting...");
        ZipFile.ExtractToDirectory(inputRawZipPath, unzipDir);

        // Run APKTOOL and decompress the file
        SendOutput("Decompiling apk...");
        ProcessStartInfo pStartInfo = new ProcessStartInfo
        {
            FileName = bin,
            Arguments = args + "\"" + apktool + "\" d -f -o \"" + apkDir + "\" \"" + currentDir + "/utils/AM2RWrapper.apk" + "\"",
            CreateNoWindow = true
        };
        Process p = new Process { StartInfo = pStartInfo };
        p.Start();
        p.WaitForExit();
        
        // Move everything into assets folder
        SendOutput("Move into Android assets folder...");
        foreach (var file in new DirectoryInfo(unzipDir).GetFiles())
            file.MoveTo(apkAssetsDir + "/" + file.Name);

        foreach (var dir in new DirectoryInfo(unzipDir).GetDirectories())
            dir.MoveTo(apkAssetsDir + "/" + dir.Name);

        // Delete unnecessary files, rename data.win, move in the new runner
        SendOutput("Delete unnecessary files for Android and lowercase them...");
        File.Delete(apkAssetsDir + "/AM2R.exe");
        File.Delete(apkAssetsDir + "/D3DX9_43.dll");
        File.Move(apkAssetsDir + "/data.win", apkAssetsDir + "/game.droid");
        File.Copy(utilDir + "/splashAndroid.png", apkAssetsDir + "/splash.png", true);

        //recursively lowercase everything in the assets folder
        LowercaseFolder(apkAssetsDir);

        // Edit apktool.yml to not compress music
        string yamlFile = File.ReadAllText(apkDir + "/apktool.yml");
        yamlFile = yamlFile.Replace("doNotCompress:", "doNotCompress:\n- ogg");
        File.WriteAllText(apkDir + "/apktool.yml", yamlFile);

        // Edit the icons in the apk
        string resPath = apkDir + "/res";
        string origPath = utilDir + "/icon.png";
        SaveAndroidIcon(origPath, 96, resPath + "/drawable/icon.png");
        SaveAndroidIcon(origPath, 72, resPath + "/drawable-hdpi-v4/icon.png");
        SaveAndroidIcon(origPath, 36, resPath + "/drawable-ldpi-v4/icon.png");
        SaveAndroidIcon(origPath, 48, resPath + "/drawable-mdpi-v4/icon.png");
        SaveAndroidIcon(origPath, 96, resPath + "/drawable-xhdpi-v4/icon.png");
        SaveAndroidIcon(origPath, 144, resPath + "/drawable-xxhdpi-v4/icon.png");
        SaveAndroidIcon(origPath, 192, resPath + "/drawable-xxxhdpi-v4/icon.png");
        
        // Hermite probably the best
        
        // On certain occasions, we need to modify the manifest file.
        if (modName != null || usesInternet)
        {
            string manifestFile = File.ReadAllText(apkDir + "/AndroidManifest.xml");

            // If a custom name was given, replace it everywhere.
            //TODO: handle errors:
            // A-Z, a-z, digits, underscore and needs to start with letters
            if (modName != null)
            {
                // first in the manifest
                manifestFile = manifestFile.Replace("com.companyname.AM2RWrapper", $"com.companyname.{modName}");
                
                // then in the rest
                // TODO: create some sort of function for it to avoid copy paste
                foreach (var file in Directory.GetFiles($"{apkDir}/smali/com/yoyogames/runner"))
                {
                    var content = File.ReadAllText(file);
                    content = content.Replace("com.companyname.AM2RWrapper", $"com.companyname.{modName}")
                        .Replace("com/companyname/AM2RWrapper", $"com/companyname/{modName}");
                    File.WriteAllText(file, content);
                }
                var am2rWrapperDir = new DirectoryInfo($"{apkDir}/smali/com/companyname/AM2RWrapper");
                foreach (var file in am2rWrapperDir.GetFiles())
                {
                    var content = File.ReadAllText(file.FullName);
                    content = content.Replace("com.companyname.AM2RWrapper", $"com.companyname.{modName}")
                        .Replace("com/companyname/AM2RWrapper", $"com/companyname/{modName}")
                        .Replace("com$companyname$AM2RWrapper", $"com$companyname${modName}");
                    File.WriteAllText(file.FullName, content);
                }
                am2rWrapperDir.MoveTo($"{apkDir}/smali/com/companyname/{modName}");

                var layoutContent = File.ReadAllText($"{apkDir}/res/layout/main.xml");
                layoutContent = layoutContent.Replace("com.companyname.AM2RWrapper", $"com.companyname.{modName}");
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
        Directory.Delete(extractDirectory, true);
    }
    
    //TODO: try to figure out if its possible to extract the name from the data.win file? They do have a displayname option last time I checked...
    public static void PortWindowsToMac(string inputRawZipPath, string outputRawZipPath, string modName, OutputHandlerDelegate outputDelegate = null)
    {
        outputHandler = outputDelegate;
        string baseTempDirectory = tmp + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string extractDirectory = baseTempDirectory + "/extract";
        string appDirectory = baseTempDirectory + "/AM2R.app";
        string contentsDir = baseTempDirectory + "/Contents";
        string assetsDir = contentsDir + "/Resources";

        // Get name from user
        //TODO: handle error on special characters

        // Check if temp folder exists, delete if yes, copy bare runner to there
        if (Directory.Exists(baseTempDirectory))
            Directory.Delete(baseTempDirectory, true);
        SendOutput("Copying Runner...");
        Directory.CreateDirectory(contentsDir);
        DirectoryCopy(utilDir + "/Contents", contentsDir, true);

        // Extract mod to temp location
        SendOutput("Extracting Mac...");
        ZipFile.ExtractToDirectory(inputRawZipPath, extractDirectory);

        // Delete unnecessary files, rename data.win, move in the new runner
        SendOutput("Delete unnecessary files for Mac and lowercase them...");
        File.Delete(extractDirectory + "/AM2R.exe");
        File.Delete(extractDirectory + "/D3DX9_43.dll");
        File.Move(extractDirectory + "/data.win", extractDirectory + "/game.ios");
        if (!File.Exists(assetsDir + "/icon.png"))
            File.Copy(utilDir + "/icon.png", extractDirectory + "/icon.png");
        if (!File.Exists(assetsDir + "/splash.png"))
            File.Copy(utilDir + "/splash.png", extractDirectory + "/splash.png");
        // Delete fonts folder if it exists, because I need to convert bytecode version from game and newer version doesn't support font loading
        if (Directory.Exists(extractDirectory + "/lang/fonts"))
            Directory.Delete(extractDirectory + "/lang/fonts", true);

        // Lowercase every file first
        LowercaseFolder(extractDirectory);

        // Convert data.win to BC16 and get rid of not needed functions anymore
        SendOutput("Editing data.win to change data.win BC version and functions...");
        string bin;
        string args;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            bin = "\"" + utilDir + "/UTMTCli/UndertaleModCli.exe\"";
            args = "";
        }
        else
        {
            // First chmod the file, just in case
            Process.Start("chmod", "+x \"" + utilDir + "/UTMTCli/UndertaleModCli.dll\"");
            bin = "dotnet";
            args = "\"" + utilDir + "/UTMTCli/UndertaleModCli.dll\" ";
            // Also chmod the runner. Just in case.
            Process.Start("chmod", "+x \"" + contentsDir + "/MacOS/Mac_Runner");
        }

        ProcessStartInfo pStartInfo = new ProcessStartInfo
        {
            FileName = bin,
            Arguments = args + "load \"" + extractDirectory + "/game.ios\" -s \"" + utilDir + "/bc16AndRemoveFunctions.csx\" -o \"" + extractDirectory + "/game.ios\"",
            CreateNoWindow = false
        };
        Process p = new Process { StartInfo = pStartInfo };
        p.Start();
        p.WaitForExit();

        // Copy assets to the place where they belong to
        SendOutput("Copy files over...");
        DirectoryCopy(extractDirectory, assetsDir, true);

        // Edit config and plist to change display name
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
        Directory.Delete(baseTempDirectory, true);
    }
}