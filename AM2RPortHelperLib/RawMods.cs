using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using UndertaleModLib;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;
using static AM2RPortHelperLib.Core;

namespace AM2RPortHelperLib;

// TODO: rebrand

public abstract class RawMods : ModsBase
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
        // Since exe's can be differently named, we'll search for exactly one exe in no subdirectories.
        var exeList = archive.Entries.Where(f => f.FullName.EndsWith(".exe")).ToList();
        if (exeList.Count == 1 && !exeList[0].FullName.Contains('/') && archive.Entries.Any(f => f.FullName == "data.win"))
            return ModOS.Windows;
        
        if (archive.Entries.Any(f => f.FullName == "runner") && archive.Entries.Any(f => f.FullName == "assets/game.unx"))
            return ModOS.Linux;
        
        // I probably *should* use fullpaths for these, but the .app file could technically be different and don't want to thinka bout how to circumvent it
        if (archive.Entries.Any(f => f.FullName.Contains("Contents/MacOS/Mac_Runner")) && archive.Entries.Any(f => f.FullName.Contains("Contents/Resources/game.ios")))
            return ModOS.Mac;
        
        throw new NotSupportedException("The OS of the mod zip is unknown and thus not supported");
    }

    /// <summary>
    /// Gets the file path of a PNG resource, either from the embedded assembly, or from an overwrite in a specific path.
    /// </summary>
    /// <param name="nameOfResource">The name of a resource, without any extension.</param>
    /// <param name="userResourcePath">A custom resource path, that should be used instead if it exists.</param>
    /// <returns>If <paramref name="userResourcePath"/>exists, it will return that, otherwise an accessible file path to the resource.</returns>
    /// <exception cref="InvalidDataException"><paramref name="nameOfResource"/> does not exist as an embedded resource.</exception>
    public static string GetProperPathToBuiltinIcons(string nameOfResource, string userResourcePath)
    {
        string SubCaseFunction(string resource)
        {
            if (File.Exists(userResourcePath))
                return userResourcePath;

            var byteArray = resource switch
            {
                nameof(Resources.icon) + ".png" => Resources.icon,
                nameof(Resources.splash) + ".png" => Resources.splash,
                _ => throw new InvalidDataException("SubCaseFunction was called with an improper resource!")
            };


            string resPath = TempDir + "/" + resource;
            if (File.Exists(resPath))
                File.Delete(resPath);
            Image.Load(byteArray).SaveAsPng(resPath);
            userResourcePath = resPath;
            return userResourcePath;
        }
        
        switch (nameOfResource)
        {
            case nameof(Resources.icon):
                return SubCaseFunction(nameof(Resources.icon) + ".png");
            case nameof(Resources.splash):
                return SubCaseFunction(nameof(Resources.splash) + ".png");
            default: throw new InvalidDataException(nameOfResource + " is an unknown Icon!");
        }
    }

    /// <summary>
    /// Ports a raw AM2R mod zip for Linux. 
    /// </summary>
    /// <param name="inputRawZipPath">The path to the raw mod zip.</param>
    /// <param name="outputRawZipPath">The path where the ported Windows mod zip should be saved to.</param>
    /// <param name="outputDelegate">A delegate to post output info to.</param>
    /// <exception cref="NotSupportedException">The raw mod zip was made for an OS that can't be determined.</exception>
    public static void PortToWindows(string inputRawZipPath, string outputRawZipPath, OutputHandlerDelegate outputDelegate = null)
    {
        ModOS currentOS = GetModOSOfRawZip(inputRawZipPath);
        outputDelegate.SendOutput("Zip Recognized as " + currentOS);

        if (currentOS == ModOS.Windows)
        {
            outputDelegate.SendOutput("Zip is already a raw Windows zip. Copying to output directory...");
            File.Copy(inputRawZipPath, outputRawZipPath, true);
            return;
        }
        
        string extractDirectory = TempDir + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);

        // Check if temp folder exists, delete if yes, extract zip to there
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        outputDelegate.SendOutput("Extracting for Raw Windows...");
        Directory.CreateDirectory(extractDirectory);
        ZipFile.ExtractToDirectory(inputRawZipPath, extractDirectory);
        
        // Delete unnecessary files, rename data.win, move in the new runner
        outputDelegate.SendOutput("Delete unnecessary files for Windows...");
        switch (currentOS)
        {
            case ModOS.Linux:
                File.Delete(extractDirectory + "/runner");
                HelperMethods.DirectoryCopy(extractDirectory + "/assets", extractDirectory);
                Directory.Delete(extractDirectory + "/assets", true);
                File.Move(extractDirectory + "/game.unx", extractDirectory + "/data.win");
                break;
            case ModOS.Mac:
                var appDir = new DirectoryInfo(extractDirectory).GetDirectories().First(n => n.Name.EndsWith(".app"));
                HelperMethods.DirectoryCopy(extractDirectory + "/" + appDir.Name + "/Contents/Resources", extractDirectory);
                File.Delete(extractDirectory + "/gamecontrollerdb.txt");
                File.Delete(extractDirectory + "/yoyorunner.config");
                Directory.Delete(extractDirectory + "/English.lproj", true);
                Directory.Delete(extractDirectory + "/" + appDir.Name, true);
                File.Move(extractDirectory + "/game.ios", extractDirectory + "/data.win");
                break;
            default: throw new NotSupportedException("The OS of the mod zip is unknown and thus not supported.");
        }
        
        // TODO: is missing the d3dx9_dll
        File.Copy(UtilDir + "/executable.exe", extractDirectory + "/AM2R.exe");

        //zip the result
        outputDelegate.SendOutput("Creating raw Windows zip...");
        ZipFile.CreateFromDirectory(extractDirectory, outputRawZipPath);

        // Clean up
        Directory.Delete(TempDir, true);
    }

    /// <summary>
    /// Ports a raw AM2R mod zip for Linux. 
    /// </summary>
    /// <param name="inputRawZipPath">The path to the raw mod zip.</param>
    /// <param name="outputRawZipPath">The path where the ported Linux mod zip should be saved to.</param>
    /// <param name="pathToIcon">The path to an icon PNG image that should be used on Linux for i.e. the taskbar.
    /// If this is <see langword="null"/>, a default stock icon is used.</param>
    /// <param name="pathToSplashScreen">The path to an splash PNG image that should be used on Linux when starting the game.
    /// If this is <see langword="null"/>, a default stock splash screen is used.</param>
    /// <param name="outputDelegate">A delegate to post output info to.</param>
    /// <exception cref="NotSupportedException">The raw mod zip was made for an OS that can't be determined.</exception>
    public static void PortToLinux(string inputRawZipPath, string outputRawZipPath, string pathToIcon = null, string pathToSplashScreen = null,
                                   OutputHandlerDelegate outputDelegate = null)
    {
        ModOS currentOS = GetModOSOfRawZip(inputRawZipPath);
        outputDelegate.SendOutput("Zip Recognized as " + currentOS);

        if (currentOS == ModOS.Linux)
        {
            outputDelegate.SendOutput("Zip is already a raw Linux zip. Copying to output directory...");
            File.Copy(inputRawZipPath, outputRawZipPath, true);
            return;
        }

        string extractDirectory = TempDir + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string assetsDir = extractDirectory + "/assets";

        // Check if temp folder exists, delete if yes, extract zip to there
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        outputDelegate.SendOutput("Extracting for Raw Linux...");
        Directory.CreateDirectory(assetsDir);
        ZipFile.ExtractToDirectory(inputRawZipPath, assetsDir);
        
        // Delete unnecessary files, rename data.win, move in the new runner
        outputDelegate.SendOutput("Delete unnecessary files for Linux...");
        switch (currentOS)
        {
            case ModOS.Windows:
                var exeFile = new DirectoryInfo(assetsDir).GetFiles().First(f => f.Name.EndsWith(".exe"));
                exeFile.Delete();
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
        File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.icon), pathToIcon), assetsDir + "/icon.png");
        File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.splash), pathToSplashScreen), assetsDir + "/splash.png");

        //recursively lowercase everything in the assets folder
        outputDelegate.SendOutput("Lowercase everything in the assets folder...");
        HelperMethods.LowercaseFolder(assetsDir);

        //zip the result
        outputDelegate.SendOutput("Creating raw Linux zip...");
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
    /// <param name="pathToIcon">The path to an icon PNG image that should be used on Android for i.e. the home screen.
    /// If this is <see langword="null"/>, a default stock icon is used.</param>
    /// <param name="pathToSplashScreen">The path to an splash PNG image that should be used on Android when starting the game.
    /// If this is <see langword="null"/>, a default stock splash screen is used.</param>
    /// <param name="outputDelegate">A delegate to post output info to.</param>
    /// <exception cref="NotSupportedException">The raw mod zip was made for an OS that can't be determined.</exception>
    /// <exception cref="InvalidDataException"><paramref name="useCustomSaveDirectory"/> was given, but the display name of the mod is unsuitable as a name for the directory.</exception>
    public static void PortToAndroid(string inputRawZipPath, string outputRawApkPath, string pathToIcon = null, string pathToSplashScreen = null, 
                                     bool useCustomSaveDirectory = false, bool usesInternet = false, OutputHandlerDelegate outputDelegate = null)
    {
        ModOS currentOS = GetModOSOfRawZip(inputRawZipPath);
        outputDelegate.SendOutput("Zip Recognized as " + currentOS);
        
        string extractDirectory = TempDir + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string apkDir = extractDirectory + "/apk";
        string apkAssetsDir = apkDir + "/assets";
        string bin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "java";
        string args = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/C java -jar " : "-jar ";
        string apktool = CurrentDir + "/utils/apktool.jar";
        string signer = CurrentDir + "/utils/uber-apk-signer.jar";
        string signedApkBuild = extractDirectory + "/build-aligned-debugSigned.apk";

        // Check if temp folder exists, delete if yes, extract zip to there
        if (Directory.Exists(extractDirectory))
            Directory.Delete(extractDirectory, true);
        Directory.CreateDirectory(extractDirectory);

        // Run APKTOOL and decompress the file
        outputDelegate.SendOutput("Decompiling apk...");
        ProcessStartInfo pStartInfo = new ProcessStartInfo
        {
            FileName = bin,
            Arguments = args + "\"" + apktool + "\" d -f -o \"" + apkDir + "\" \"" + UtilDir + "/AM2RWrapper.apk" + "\"",
            CreateNoWindow = true
        };
        Process p = new Process { StartInfo = pStartInfo };
        p.Start();
        p.WaitForExit();
        
        outputDelegate.SendOutput("Extracting for Raw Android...");
        ZipFile.ExtractToDirectory(inputRawZipPath, apkAssetsDir);
        
        // Delete unnecessary files, rename data.win, move in the new runner
        outputDelegate.SendOutput("Delete unnecessary files for Android...");
        switch (currentOS)
        {
            case ModOS.Windows:
                var exeFile = new DirectoryInfo(apkAssetsDir).GetFiles().First(f => f.Name.EndsWith(".exe"));
                exeFile.Delete();
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
        File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.splash), pathToSplashScreen), apkAssetsDir + "/splash.png", true);

        //recursively lowercase everything in the assets folder
        outputDelegate.SendOutput("Lowercase everything in the assets folder...");
        HelperMethods.LowercaseFolder(apkAssetsDir);

        // Edit apktool.yml to not compress music
        outputDelegate.SendOutput("Edit settings file to not compress OGGs...");
        string yamlFile = File.ReadAllText(apkDir + "/apktool.yml");
        yamlFile = yamlFile.Replace("doNotCompress:", "doNotCompress:\n- ogg");
        File.WriteAllText(apkDir + "/apktool.yml", yamlFile);
        
        outputDelegate.SendOutput("Save new icons");
        // Edit the icons in the apk. Wrapper always has these, so we need to overwrite these too.
        string resPath = apkDir + "/res";
        // Icon should only be read from if its there, otherwise default frog icon should be in the assembly
        string origPath = GetProperPathToBuiltinIcons(nameof(Resources.icon), pathToIcon);
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
                outputDelegate.SendOutput("Get display name...");
                string modName;
                FileInfo datafile = new FileInfo(apkAssetsDir + "/game.droid");
                using (FileStream fs = datafile.OpenRead())
                {
                    UndertaleData gmData = UndertaleIO.Read(fs, outputDelegate.SendOutput, outputDelegate.SendOutput);
                    modName = gmData.GeneralInfo.DisplayName.Content;
                }
                modName = modName.Replace(" ", "").Replace(":", "");
                
                // rules for name: A-Z, a-z, digits, underscore and needs to start with letters
                Regex nameReg = new Regex(@"^[a-zA-Z][a-zA-Z0-9_]*$");
                if (!nameReg.Match(modName).Success)
                    throw new InvalidDataException("The display name " + modName + " is invalid! The name has to start with letters (a-z), and can only contain letters, digits, space, colon and underscore!");
                
                outputDelegate.SendOutput("Replace Android save directory...");
                
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
                outputDelegate.SendOutput("Replace Internet permission...");
                const string bluetoothPermission = "<uses-permission android:name=\"android.permission.BLUETOOTH\"/>";
                const string internetPermission = "<uses-permission android:name=\"android.permission.INTERNET\"/>";
                manifestFile = manifestFile.Replace(bluetoothPermission, internetPermission + "\n    " + bluetoothPermission);
            }
            File.WriteAllText(apkDir + "/AndroidManifest.xml", manifestFile);
        }

        // Run APKTOOL and build the apk
        outputDelegate.SendOutput("Rebuild apk...");
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
        outputDelegate.SendOutput("Sign apk...");
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
        File.Move(signedApkBuild, outputRawApkPath);

        // Clean up
        Directory.Delete(TempDir, true);
    }
    
    /// <summary>
    /// Ports a raw AM2R mod zip for macOS.
    /// </summary>
    /// <param name="inputRawZipPath">The path to the raw mod zip.</param>
    /// <param name="outputRawZipPath">he path where the ported Mac mod zip should be saved to.</param>
    /// <param name="pathToIcon">The path to an icon PNG image that should be used on Mac for i.e. the dock.
    /// If this is <see langword="null"/>, a default stock icon is used.</param>
    /// <param name="pathToSplashScreen">The path to an splash PNG image that should be used on macOS when starting the game.
    /// If this is <see langword="null"/>, a default stock splash screen is used.</param>
    /// <param name="outputDelegate">A delegate to post output info to.</param>
    /// <exception cref="NotSupportedException">The raw mod zip was made for an OS that can't be determined.</exception>
    public static void PortToMac(string inputRawZipPath, string outputRawZipPath, string pathToIcon = null, string pathToSplashScreen = null,
                                 OutputHandlerDelegate outputDelegate = null)
    {
        ModOS currentOS = GetModOSOfRawZip(inputRawZipPath);
        outputDelegate.SendOutput("Zip Recognized as " + currentOS);

        if (currentOS == ModOS.Mac)
        {
            outputDelegate.SendOutput("Zip is already a raw Mac zip. Copying to output dir...");
            File.Copy(inputRawZipPath, outputRawZipPath, true);
            return;
        }
        
        string baseTempDirectory = TempDir + "/" + Path.GetFileNameWithoutExtension(inputRawZipPath);
        string extractDirectory = baseTempDirectory + "/extract";
        string appDirectory = baseTempDirectory + "/AM2R.app";
        string contentsDir = baseTempDirectory + "/Contents";
        string assetsDir = contentsDir + "/Resources";
        
        // Check if temp folder exists, delete if yes, copy bare runner to there
        if (Directory.Exists(baseTempDirectory))
            Directory.Delete(baseTempDirectory, true);
        outputDelegate.SendOutput("Copying Mac Runner...");
        Directory.CreateDirectory(contentsDir);
        HelperMethods.DirectoryCopy(UtilDir + "/Contents", contentsDir);

        // Extract mod to temp location
        outputDelegate.SendOutput("Extracting Mac...");
        ZipFile.ExtractToDirectory(inputRawZipPath, extractDirectory);

        // Delete unnecessary files, rename data.win, move in the new runner
        outputDelegate.SendOutput("Delete unnecessary files for Mac...");
        switch (currentOS)
        {
            case ModOS.Windows:
                var exeFile = new DirectoryInfo(extractDirectory).GetFiles().First(f => f.Name.EndsWith(".exe"));
                exeFile.Delete();
                File.Delete(extractDirectory + "/D3DX9_43.dll");
                File.Move(extractDirectory + "/data.win", extractDirectory + "/game.ios");
                break;
            case ModOS.Linux:
                File.Delete(extractDirectory + "/runner");
                HelperMethods.DirectoryCopy(extractDirectory + "/assets", extractDirectory);
                Directory.Delete(extractDirectory + "/assets", true);
                File.Move(extractDirectory + "/game.unx", extractDirectory + "/game.ios");
                break;
            default: throw new NotSupportedException("The OS of the mod zip is unknown and thus not supported");
        }

        File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.icon), pathToIcon), extractDirectory + "/icon.png", true);
        File.Copy(GetProperPathToBuiltinIcons(nameof(Resources.splash), pathToSplashScreen), extractDirectory + "/splash.png", true);
        
        // Delete fonts folder if it exists, because I need to convert bytecode version from game and newer version doesn't support font loading
        if (Directory.Exists(extractDirectory + "/lang/fonts"))
            Directory.Delete(extractDirectory + "/lang/fonts", true);

        // Lowercase every file first
        outputDelegate.SendOutput("Lowercase everything in the assets folder...");
        HelperMethods.LowercaseFolder(extractDirectory);

        // Convert data.win to BC16 and get rid of not needed functions anymore
        outputDelegate.SendOutput("Editing data.win to change ByteCode version and functions...");
        
        string modName;
        FileInfo datafile = new FileInfo(extractDirectory + "/game.ios");

        // Convert data file to ByteCode 16
        {
            UndertaleData gmData;
            using (FileStream fs = datafile.OpenRead())
            {
                gmData = UndertaleIO.Read(fs, outputDelegate.SendOutput, outputDelegate.SendOutput);
                modName = gmData.GeneralInfo.DisplayName.Content;

                ChangeToByteCode16(gmData, outputDelegate.SendOutput);
            }

            using (FileStream fs = new FileInfo(extractDirectory + "/game.ios").OpenWrite())
            {
                UndertaleIO.Write(fs, gmData, outputDelegate.SendOutput);
            }
        }

        // Also chmod the runner. Just in case.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start("chmod", "+x \"" + contentsDir + "/MacOS/Mac_Runner");

        // Copy assets to the place where they belong to
        outputDelegate.SendOutput("Copy files over...");
        HelperMethods.DirectoryCopy(extractDirectory, assetsDir);

        // Edit config and plist to change display name
        // Escape invalid xml characters
        modName = SecurityElement.Escape(modName);
        outputDelegate.SendOutput("Editing Runner references to AM2R...");
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
        outputDelegate.SendOutput("Creating Mac zip...");
        ZipFile.CreateFromDirectory(baseTempDirectory, outputRawZipPath);

        // Clean up
        Directory.Delete(TempDir, true);
    }
    
    /// <summary>
    /// Converts a GameMaker data file to bytecode version 16
    /// </summary>
    /// <param name="Data">The GameMaker data file.</param>
    /// <param name="output">Delegate on where to send output messages to.</param>
    /// <exception cref="NotSupportedException"><paramref name="Data"/> has a not supported Bytecode version (13, 14, GM2.3+).</exception>
    private static void ChangeToByteCode16(UndertaleData Data, OutputHandlerDelegate output)
    {
        if (Data is null) return;
        
        byte? bcVersion = Data.GeneralInfo.BytecodeVersion;
        void ScriptMessage(string s) => output?.Invoke(s);
        
        if (!Data.FORM.Chunks.ContainsKey("AGRP"))
            throw new NotSupportedException("Bytecode 13 is not supported.");
        if (bcVersion == 14)
            throw new NotSupportedException("Bytecode 14 is not supported.");
        if (bcVersion == 17)
            throw new NotSupportedException("Bytecode 17 is not supported.");
        if (!((Data.GMS2_3 == false) && (Data.GMS2_3_1 == false) && (Data.GMS2_3_2 == false)))
            throw new NotSupportedException("GMS 2.3+ is not supported.");
        if (bcVersion != 14 && bcVersion != 15 && bcVersion != 16)
            throw new NotSupportedException("Unknown Bytecode version!");
        
        if ((bcVersion == 14) || (bcVersion == 15))
        {
            // For BC 14
            if (bcVersion <= 14)
            {
                foreach (UndertaleCode code in Data.Code)
                {
                    UndertaleCodeLocals locals = new UndertaleCodeLocals();
                    locals.Name = code.Name;

                    UndertaleCodeLocals.LocalVar argsLocal = new UndertaleCodeLocals.LocalVar();
                    argsLocal.Name = Data.Strings.MakeString("arguments");
                    argsLocal.Index = 0;

                    locals.Locals.Add(argsLocal);

                    code.LocalsCount = 1;
                    code.GenerateLocalVarDefinitions(code.FindReferencedLocalVars(), locals); // Dunno if we actually need this line, but it seems to work?
                    Data.CodeLocals.Add(locals);
                }
            }
            // For BC 13
            if (!Data.FORM.Chunks.ContainsKey("AGRP"))
            {
                Data.FORM.Chunks["AGRP"] = new UndertaleChunkAGRP();
                var previous = -1;
                var j = 0;
                for (var i = -1; i < Data.Sounds.Count - 1; i++)
                {
                    UndertaleSound sound = Data.Sounds[i + 1];
                    bool flagCompressed = sound.Flags.HasFlag(UndertaleSound.AudioEntryFlags.IsCompressed);
                    bool flagEmbedded = sound.Flags.HasFlag(UndertaleSound.AudioEntryFlags.IsEmbedded);
                    if (i == -1)
                    {
                        if (!flagCompressed && !flagEmbedded)
                        {
                            sound.AudioID = -1;
                        }
                        else
                        {
                            sound.AudioID = 0;
                            previous = 0;
                            j = 1;
                        }
                    }
                    else
                    {
                        if (!flagCompressed && !flagEmbedded)
                            sound.AudioID = previous;
                        else
                        {
                            sound.AudioID = j;
                            previous = j;
                            j++;
                        }
                    }
                }
                foreach (UndertaleSound sound in Data.Sounds)
                {
                    if ((sound.AudioID >= 0) && (sound.AudioID < Data.EmbeddedAudio.Count))
                    {
                        sound.AudioFile = Data.EmbeddedAudio[sound.AudioID];
                    }
                    sound.GroupID = 0;
                }
                Data.GeneralInfo.Build = 1804;
                var newProductID = new byte[] { 0xBA, 0x5E, 0xBA, 0x11, 0xBA, 0xDD, 0x06, 0x60, 0xBE, 0xEF, 0xED, 0xBA, 0x0B, 0xAB, 0xBA, 0xBE };
                Data.FORM.EXTN.productIdData.Add(newProductID);
                Data.Options.Constants.Clear();
                Data.Options.Constants.Add(new UndertaleOptions.Constant { Name = Data.Strings.MakeString("@@SleepMargin"), Value = Data.Strings.MakeString(1.ToString()) });
                Data.Options.Constants.Add(new UndertaleOptions.Constant { Name = Data.Strings.MakeString("@@DrawColour"), Value = Data.Strings.MakeString(0xFFFFFFFF.ToString()) });
            }
            Data.FORM.Chunks["LANG"] = new UndertaleChunkLANG();
            Data.FORM.LANG.Object = new UndertaleLanguage();
            Data.FORM.Chunks["GLOB"] = new UndertaleChunkGLOB();
            string[] order = { "GEN8", "OPTN", "LANG", "EXTN", "SOND", "AGRP", "SPRT", "BGND", "PATH", "SCPT", "GLOB", "SHDR", "FONT", "TMLN", "OBJT", "ROOM", "DAFL", "TPAG", "CODE", "VARI", "FUNC", "STRG", "TXTR", "AUDO" };
            Dictionary<string, UndertaleChunk> newChunks = new Dictionary<string, UndertaleChunk>();
            foreach (string name in order)
                newChunks[name] = Data.FORM.Chunks[name];
            Data.FORM.Chunks = newChunks;
            Data.GeneralInfo.BytecodeVersion = 16;
            ScriptMessage("Upgraded from " + bcVersion + " to 16 successfully.");
        }
        else if (bcVersion == 16)
        {
            ScriptMessage("This is already bytecode 16.");
        }

        ScriptMessage("Trying to remove functions \"immersion_play_effect\", \"immersion_stop\" and \"font_replace\"!");
        foreach (UndertaleFunction func in Data.Functions.ToList())
        {
            if (func.ToString() == "immersion_play_effect" || func.ToString() == "immersion_stop" || func.ToString() == "font_replace")
                Data.Functions.Remove(func);
        }
    }
}