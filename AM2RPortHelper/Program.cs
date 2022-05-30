using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;

namespace AM2RPortHelper
{
    internal static class Program
    {
        private const string version = "1.3";
        private static readonly string tmp = Path.GetTempPath();
        private static readonly string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        private static readonly string utilDir = currentDir + "/utils";

        private static void Main(string[] args)
        {

            Console.WriteLine("AM2RPortHelper v" + version);

            if (args == null || args.Length == 0)
            {
                Console.WriteLine("Please drag-n-drop a Zip of your mod or provide it as an argument.");
                return;
            }

            FileInfo modZipPath = new FileInfo(args[0]);
            if (!modZipPath.Exists && modZipPath.Extension.ToLower() != "zip")
            {
                Console.WriteLine("Path does not point to a mod zip");
                return;
            }

            Console.WriteLine("\n**Make sure to replace the icon.png and splash.png with custom ones if you don't want to have placeholders**\n");
            Console.WriteLine("THIS ONLY WORKS FOR MODS BASED ON THE COMMUNITY UPDATES! MODS BASED ON 1.1 WILL NOT WORK!");
            Console.WriteLine("To which platform do you want to port to?\n1 - Linux\n2 - Android\n3 - MacOS");

            var input = Console.ReadKey().Key.ToString();
            Console.WriteLine();
            switch (input)
            {
                case "D1": PortForLinux(modZipPath); break;

                case "D2": PortForAndroid(modZipPath); break;

                case "D3": PortForMac(modZipPath); break;

                default: Console.WriteLine("Unacceptable input. Aborting..."); return;
            }
            Console.WriteLine("Successfully finished!");
            Console.WriteLine("Exiting in 5 seconds...");
            Thread.Sleep(5000);
        }

        private static void PortForLinux(FileInfo modZipPath)
        {
            string extractDirectory = tmp + "/" + modZipPath.Name;
            string assetsDir = extractDirectory + "/assets";
            string linuxModPath = currentDir + "/" + Path.GetFileNameWithoutExtension(modZipPath.FullName) + "_LINUX.zip";

            // Check if temp folder exists, delete if yes, extract zip to there
            if (Directory.Exists(extractDirectory))
                Directory.Delete(extractDirectory, true);
            Console.WriteLine("Extracting...");
            ZipFile.ExtractToDirectory(modZipPath.FullName, extractDirectory, true);

            // Move everything into assets folder
            Console.WriteLine("Moving into assets folder...");
            Directory.CreateDirectory(assetsDir);
            foreach (var file in new DirectoryInfo(extractDirectory).GetFiles())
                file.MoveTo(assetsDir + "/" + file.Name);

            foreach (var dir in new DirectoryInfo(extractDirectory).GetDirectories())
            {
                if (dir.Name == "assets") continue;
                dir.MoveTo(assetsDir + "/" + dir.Name);
            }

            // Delete unnecessary files, rename data.win, move in the new runner
            Console.WriteLine("Delete unnecessary files and lowercase them...");
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

            //zip the result if no 
            if (File.Exists(linuxModPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(linuxModPath + " already exists! Please move it somewhere else.");
                return;
            }
            Console.WriteLine("Creating zip...");
            ZipFile.CreateFromDirectory(extractDirectory, linuxModPath);

            // Clean up
            Directory.Delete(assetsDir, true);
        }

        private static void PortForAndroid(FileInfo modZipPath)
        {
            string extractDirectory = tmp + "/" + modZipPath.Name;
            string unzipDir = extractDirectory + "/zip";
            string apkDir = extractDirectory + "/apk";
            string apkAssetsDir = apkDir + "/assets";
            string bin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "java";
            string args = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/C java -jar " : "-jar ";
            string apktool = currentDir + "/utils/apktool.jar";
            string signer = currentDir + "/utils/uber-apk-signer.jar";
            string finalApkBuild = extractDirectory + "/build-aligned-debugSigned.apk";
            string apkModPath = currentDir + "/" + Path.GetFileNameWithoutExtension(modZipPath.FullName) + "_ANDROID.apk";


            // Check if temp folder exists, delete if yes, extract zip to there
            if (Directory.Exists(extractDirectory))
                Directory.Delete(extractDirectory, true);
            Directory.CreateDirectory(extractDirectory);
            Console.WriteLine("Extracting...");
            ZipFile.ExtractToDirectory(modZipPath.FullName, unzipDir);

            // Run APKTOOL and decompress the file
            Console.WriteLine("Decompiling apk...");
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
            Console.WriteLine("Move into assets folder...");
            foreach (var file in new DirectoryInfo(unzipDir).GetFiles())
                file.MoveTo(apkAssetsDir + "/" + file.Name);

            foreach (var dir in new DirectoryInfo(unzipDir).GetDirectories())
            {
                dir.MoveTo(apkAssetsDir + "/" + dir.Name);
            }

            // Delete unnecessary files, rename data.win, move in the new runner
            Console.WriteLine("Delete unnecessary files and lowercase them...");
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
            Image orig = Image.Load(utilDir + "/icon.png");
            SaveAndroidIcon(orig, 96, resPath + "/drawable/icon.png");
            SaveAndroidIcon(orig, 72, resPath + "/drawable-hdpi-v4/icon.png");
            SaveAndroidIcon(orig, 36, resPath + "/drawable-ldpi-v4/icon.png");
            SaveAndroidIcon(orig, 48, resPath + "/drawable-mdpi-v4/icon.png");
            SaveAndroidIcon(orig, 96, resPath + "/drawable-xhdpi-v4/icon.png");
            SaveAndroidIcon(orig, 144, resPath + "/drawable-xxhdpi-v4/icon.png");
            SaveAndroidIcon(orig, 192, resPath + "/drawable-xxxhdpi-v4/icon.png");

            // Run APKTOOL and build the apk
            Console.WriteLine("Rebuild apk...");
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
            Console.WriteLine("Sign apk...");
            pStartInfo = new ProcessStartInfo
            {
                FileName = bin,
                Arguments = args + "\"" + signer + "\" -a \"" + extractDirectory + "/build.apk" + "\"",
                CreateNoWindow = true
            };
            p = new Process { StartInfo = pStartInfo };
            p.Start();
            p.WaitForExit();

            //Move apk if it doesn't exist already
            if (File.Exists(apkModPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(apkModPath + " already exists! Please move it somewhere else.");
                return;
            }
            File.Move(finalApkBuild, apkModPath);

            // Clean up
            Directory.Delete(extractDirectory, true);
        }
        private static void PortForMac(FileInfo modZipPath)
        {
            string baseTempDirectory = tmp + "/" + modZipPath.Name;
            string extractDirectory = baseTempDirectory + "/extract";
            string appDirectory = baseTempDirectory + "/AM2R.app";
            string contentsDir = baseTempDirectory + "/Contents";
            string assetsDir = contentsDir + "/Resources";
            string macosModPath = currentDir + "/" + Path.GetFileNameWithoutExtension(modZipPath.FullName) + "_MACOS.zip";

            // Get name from user
            //TODO: handle error on special characters
            Console.WriteLine("State the name of your mod (no special characters!)");
            string input = Console.ReadLine();

            // Rename the .app "file", makes it too difficult to use with modpacker so commented out.
            //if (!String.IsNullOrWhiteSpace(input))
            //    appDirectory = appDirectory.Replace("AM2R", input);

            // Check if temp folder exists, delete if yes, copy bare runner to there
            if (Directory.Exists(baseTempDirectory))
                Directory.Delete(baseTempDirectory, true);
            Console.WriteLine("Copying Runner...");
            Directory.CreateDirectory(contentsDir);
            DirectoryCopy(utilDir + "/Contents", contentsDir, true);

            // Extract mod to temp location
            Console.WriteLine("Extracting...");
            ZipFile.ExtractToDirectory(modZipPath.FullName, extractDirectory, true);

            // Delete unnecessary files, rename data.win, move in the new runner
            Console.WriteLine("Delete unnecessary files and lowercase them...");
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
            Console.WriteLine("Editing data.win to change data.win BC version and functions...");
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
            Console.WriteLine("Copy files over...");
            DirectoryCopy(extractDirectory, assetsDir, true);

            // Edit config and plist to change display name
            Console.WriteLine("Editing Runner references to AM2R...");
            string textFile = File.ReadAllText(assetsDir + "/yoyorunner.config");
            textFile = textFile.Replace("YoYo Runner", input);
            File.WriteAllText(assetsDir + "/yoyorunner.config", textFile);

            textFile = File.ReadAllText(contentsDir + "/Info.plist");
            textFile = textFile.Replace("YoYo Runner", input);
            File.WriteAllText(contentsDir + "/Info.plist", textFile);

            // Create a .app directory and move contents in there
            Directory.CreateDirectory(appDirectory);
            Directory.Move(contentsDir, appDirectory + "/Contents");

            Directory.Delete(extractDirectory, true);

            //zip the result if no 
            if (File.Exists(macosModPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(macosModPath + " already exists! Please move it somewhere else.");
                return;
            }
            Console.WriteLine("Creating zip...");
            ZipFile.CreateFromDirectory(baseTempDirectory, macosModPath);

            // Clean up
            Directory.Delete(baseTempDirectory, true);
        }

        private static void LowercaseFolder(string directory)
        {
            DirectoryInfo dir = new DirectoryInfo(directory);

            foreach(var file in dir.GetFiles())
            {
                if (file.Name == file.Name.ToLower()) continue;
                file.MoveTo(file.DirectoryName + "/" + file.Name.ToLower());
            }

            foreach(var subDir in dir.GetDirectories())
            {
                if (subDir.Name == subDir.Name.ToLower()) continue;
                subDir.MoveTo(subDir.Parent.FullName + "/" + subDir.Name.ToLower());
                LowercaseFolder(subDir.FullName);
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, true);
            }


            if (!copySubDirs)
                return;

            // If copying subdirectories, copy them and their contents to new location.
            foreach (DirectoryInfo subDir in dirs)
            {
                string tempPath = Path.Combine(destDirName, subDir.Name);
                DirectoryCopy(subDir.FullName, tempPath, true);
            }
        }

        private static void SaveAndroidIcon(Image icon, int dimensions, string filePath)
        {
            Image picture = icon;
            picture.Mutate(x => x.Resize(dimensions, dimensions, KnownResamplers.NearestNeighbor));
            picture.SaveAsPng(filePath);
        }
    }
}
