using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AM2RPortHelper
{
    class Program
    {
        static string version = "1.2";
        static string tmp = Path.GetTempPath();
        static string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        static string utilDir = currentDir + "/utils";

        static void Main(string[] args)
        {

            Console.WriteLine("AM2RPortHelper v" + version);

            if (args == null || args.Length == 0)
            {
                Console.WriteLine("Please drag-n-drop a Zip of your mod or provide it as an argument.");
                return;
            }

            FileInfo modZipPath = new (args[0]);
            if (!modZipPath.Exists && modZipPath.Extension.ToLower() != "zip")
            {
                Console.WriteLine("Path does not point to a mod zip");
                return;
            }

            Console.WriteLine("THIS ONLY WORKS FOR MODS BASED ON THE COMMUNITY UPDATES! MODS BASED ON 1.1 WILL NOT WORK!");
            Console.WriteLine("To which platform do you want to port to?\n1 - Linux\n2 - Android");

            var input = Console.ReadKey().Key.ToString();
            Console.WriteLine();
            switch (input)
            {
                case "D1": PortForLinux(modZipPath); break;

                case "D2": PortForAndroid(modZipPath); break;

                default: Console.WriteLine("Unacceptable input. Aborting..."); return;
            }

            Console.WriteLine("Exiting in 5 seconds...");
            Thread.Sleep(5000);
        }

        static void PortForLinux(FileInfo modZipPath)
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
            Console.WriteLine("Delete unnecesary files and lowercase them...");
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
                Console.WriteLine(linuxModPath + " already exists! Please move it somewhere else.");
                return;
            }
            Console.WriteLine("Creating zip...");
            ZipFile.CreateFromDirectory(extractDirectory, linuxModPath);

            // Clean up
            Directory.Delete(assetsDir, true);

            Console.WriteLine("Successfully finished!");
            Console.WriteLine("\n**Make sure to replace the icon.png and splash.png with custom ones if you don't want to have placeholders**\n");
        }

        static void PortForAndroid(FileInfo modZipPath)
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
            Process p = new Process() { StartInfo = pStartInfo };
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

            // Run APKTOOL and build the apk
            Console.WriteLine("Rebuild apk...");
            pStartInfo = new ProcessStartInfo
            {
                FileName = bin,
                Arguments = args + "\"" + apktool + "\" b \"" + apkDir + "\" -o \"" + extractDirectory + "/build.apk" + "\"",
                CreateNoWindow = true
            };
            p = new Process() { StartInfo = pStartInfo };
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
            p = new Process() { StartInfo = pStartInfo };
            p.Start();
            p.WaitForExit();

            //Move apk if it doesn't exist already
            if (File.Exists(apkModPath))
            {
                Console.WriteLine(apkModPath + " already exists! Please move it somewhere else.");
                return;
            }
            File.Move(finalApkBuild, apkModPath);

            // Clean up
            Directory.Delete(extractDirectory, true);

            Console.WriteLine("Successfully finished!");
            Console.WriteLine("\n**This will currently use default splashes and icons, if you don't like them don't forget to replace them in the apk**\n");
        }

        static void LowercaseFolder(string directory)
        {
            DirectoryInfo dir = new(directory);

            foreach(var file in dir.GetFiles())
            {
                if (file.Name == file.Name.ToLower()) continue;
                file.MoveTo(file.DirectoryName + "/" + file.Name.ToLower());
            }

            foreach(var subdir in dir.GetDirectories())
            {
                if (subdir.Name == subdir.Name.ToLower()) continue;
                subdir.MoveTo(subdir.Parent.FullName + "/" + subdir.Name.ToLower());
                LowercaseFolder(subdir.FullName);
            }
        }
    }
}
