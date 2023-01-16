using System.IO.Compression;

namespace AM2RPortHelperLib;

public abstract class LauncherMods : IMods
{
    /// <summary>
    /// Ports a Mod zip intended to be installed via the AM2RLauncher to other operating systems.
    /// </summary>
    /// <param name="inputLauncherZipPath">The path to the AM2RLauncher mod zip that should be ported.</param>
    /// <param name="modTarget">The target operating system to port the </param>
    /// <param name="includeAndroid">Whether Android should be inlcuded in the port.</param>
    /// <param name="outputLauncherZipPath">The path where the ported AM2RLauncher mod zip should be saved.</param>
    /// <param name="am2r11ZipPath">The path to an AM2R 1.1 zip path. This is *required* if the input launcher zip is for Mac and will be ignored if the input zip is for anything else.</param>
    /// <param name="outputDelegate">The function that should handle in-progress output messages.</param>
    /// <exception cref="NotSupportedException">WIP</exception>
    /// TODO: other exceptions
    public static void PortLauncherMod(string inputLauncherZipPath, Core.ModOS modTarget, bool includeAndroid, string outputLauncherZipPath, string am2r11ZipPath = null, OutputHandlerDelegate outputDelegate = null)
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

        if (modTarget.ToString() == profile.OperatingSystem)
        {
            SendOutput("Target OS and Launcher OS are the same; exiting.");
            return;
        }

        // Run sha256 hash on runner to see if it's supported!        
        string runnerHash = HelperMethods.CalculateSHA256(extractDirectory + "/AM2R.xdelta");
        string[] allowedHashes = new[] { "b78c4fd2dc481f97b60440a5c89786da284b4aaeeba9fb2e3b48ac369cfe50d5", "243509f4270f448411c8405b71d7bc4f5d4fe5f3ecc1638d9c1218bf76b69f1f", "852b9a9466f99a53260b8147c6d286b81c145b2c10b00bb5c392b40b035811b5"};
        // Don't check has on Windows, because the runner there has icons embedded in it, screwing off the hashes
        // TODO: find a way around that
        if (!(profile.OperatingSystem == "Windows" || allowedHashes.Contains(runnerHash)))
            throw new NotSupportedException("Invalid GM:S version! Porting Launcher mods is only supported for mods build with GM:S 1.4.1763!");
        
        // TODO: Not sure if this is ever gonna be possible, since it requires one to shift back the patch.
        // We'd need a 1.1 file to apply the patch to, run that with umtlib to shift it back, and then apply a new patch.
        if (profile.OperatingSystem == "Mac")
            throw new NotSupportedException("Porting Mac mods is currently not supported!");
        
        switch (modTarget)
        {
            case Core.ModOS.Windows:
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
            
            case Core.ModOS.Linux:
            {
                if (currentOS == "Windows")
                    File.Move(extractDirectory + "/data.xdelta", extractDirectory + "/game.xdelta");

                // get proper runner
                File.Delete(extractDirectory + "/AM2R.xdelta");
                File.Copy(utilDir + "/linuxRunner.xdelta", extractDirectory + "/AM2R.xdelta");
                
                // Linux needs everything lowercased. Only needed if we're coming from Windows
                if (currentOS == "Windows")
                    HelperMethods.LowercaseFolder(extractDirectory + "/files_to_copy");                  
                
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

            case Core.ModOS.Mac:
            {
                // TODO: Not sure if this is ever gonna be possible, since it requires one to shift up the patch.
                // We'd need a 1.1 file to apply the patch to, run that with umtlib to shift it up, and then apply a new patch.
                throw new NotSupportedException("Porting Mac mods is currently not supported!");
            }
            default: throw new ArgumentOutOfRangeException(nameof(modTarget), modTarget, "Unknown target to port to!");
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
        SendOutput($"Creating Launcher zip for {modTarget}...");
        ZipFile.CreateFromDirectory(extractDirectory, outputLauncherZipPath);

        // Clean up
        Directory.Delete(extractDirectory, true);
    }
}