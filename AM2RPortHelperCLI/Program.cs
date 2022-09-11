using System;
using System.CommandLine;
using System.IO;
using AM2RPortHelperLib;

namespace AM2RPortHelper;


internal static class Program
{
// TODO: implement launcher flag -u launcher
    private static int Main(string[] args)
    {
        Console.WriteLine("AM2RPortHelperCLI v" + PortHelper.Version);
        
        //PortHelper.PortLauncherMod("/home/narr/Downloads/Multitroid1_4_2VM_Linux.zip", "Windows", false, "./foo.zip");
        
        var interactiveOption = new Option<bool>(new[] { "-i", "--interactive" }, "Use an interactive mode. This will ignore all other options.");
        var fileOption = new Option<FileInfo>(new[] { "-f", "--file" }, "The file path to the raw mod that should be ported. *REQUIRED*");
        var linuxOption = new Option<FileInfo>(new[] { "-l", "--linux" }, "The output file path for the Linux mod. None given equals to no Linux port.");
        var androidOption = new Option<FileInfo>(new[] { "-a", "--android" }, "The output file path for the Android mod. None given equals to no Android port.");
        var macOption = new Option<FileInfo>(new[] { "-m", "--mac" }, "The output file path for the Mac mod. None given equals to no Mac port.");
        var nameOption = new Option<string>(new[] { "-n", "--name" }, "The name used for the Mac or Android mod. Required for the Mac option, and optional for the Android version. Has no effect on anything else.");

        RootCommand rootCommand = new RootCommand
        {
            interactiveOption,
            fileOption,
            linuxOption,
            androidOption,
            macOption,
            nameOption
        };
        rootCommand.SetHandler(RootMethod, interactiveOption, fileOption, linuxOption, androidOption, macOption, nameOption);
        
        
        return rootCommand.Invoke(args);
        
        /*
         TODO: show this somewhere? maybe?
        Console.WriteLine("\n**Make sure to replace the icon.png and splash.png with custom ones if you don't want to have placeholders**\n");
        Console.WriteLine("THIS ONLY WORKS FOR MODS BASED ON THE COMMUNITY UPDATES! MODS BASED ON 1.1 WILL NOT WORK!");
        */

    }
    private static void RootMethod(bool interactive, FileInfo inputModPath, FileInfo linuxPath, FileInfo androidPath, FileInfo macPath, string modName)
    {
        if (interactive)
        {
            RunInteractive();
            return;
        }
        if (!IsValidInputZip(inputModPath))
        {
            Console.Error.WriteLine("Input path does not exist, or does not point to a zip file!");
            return;
        }

        if (linuxPath is not null)
        {
            PortHelper.PortWindowsToLinux(inputModPath.FullName, linuxPath.FullName);
        }
        if (androidPath is not null)
        {
            PortHelper.PortWindowsToAndroid(inputModPath.FullName, androidPath.FullName, string.IsNullOrWhiteSpace(modName) ? null : modName);
        }
        if (macPath is not null)
        {
            if (modName is null)
            {
                Console.Error.WriteLine("Mac option was chosen but mod name was not given!");
                return;
            }
            PortHelper.PortWindowsToMac(inputModPath.FullName, macPath.FullName, modName);
        }
        Console.WriteLine("Done.");
    }
    private static void RunInteractive()
    {
        // Path to zip
        bool invalidZip = true;
        string modZipPath;
        do
        {
            Console.WriteLine("Please provide the full path to the raw mod zip!");
            modZipPath = Console.ReadLine();

            if (!IsValidInputZip(modZipPath))
                Console.WriteLine("Path does not exist, or does not point to a zip file!");
            else 
                invalidZip = false;
        } while (invalidZip);
        
        // OS choice
        bool linuxSelected = false;
        bool androidSelected = false;
        bool macSelected = false;
        bool invalidOS = true;
        do
        {
            Console.WriteLine("Select the platforms you want to port to:");
            Console.WriteLine($"1 - Linux (currently {(linuxSelected ? "on" : "off")})");
            Console.WriteLine($"2 - Android (currently {(androidSelected ? "on" : "off")})");
            Console.WriteLine($"3 - Mac (currently {(macSelected ? "on" : "off")})");
            Console.WriteLine("4 - Port!");
            var input = Console.ReadKey().Key.ToString();
            Console.WriteLine();

            switch (input)
            {
                case "D1": linuxSelected = !linuxSelected; break;

                case "D2": androidSelected = !androidSelected; break;

                case "D3": macSelected = !macSelected; break;
                
                case "D4":
                    if (linuxSelected || androidSelected || macSelected)
                        invalidOS = false;
                    else
                        Console.WriteLine("You have to at least select one OS!");
                    break;
            }
        } while (invalidOS);

        // Port everything
        string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        string linuxPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_LINUX.zip";
        string androidPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_ANDROID.apk";
        string macPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_MACOS.zip";
            
        if (File.Exists(linuxPath))
            File.Delete(linuxPath);
        if (File.Exists(androidPath))
            File.Delete(androidPath);
        if (File.Exists(macPath))
            File.Delete(macPath);
            
        if (linuxSelected)
            PortHelper.PortWindowsToLinux(modZipPath,linuxPath);
        if (androidSelected) 
            PortHelper.PortWindowsToAndroid(modZipPath, androidPath);
        if (macSelected)
        {
            Console.WriteLine("Mac requires a name! Please enter one (no special characters!):");
            string modName = Console.ReadLine();
            PortHelper.PortWindowsToMac(modZipPath, macPath, modName);
        }
        
        Console.WriteLine("Successfully finished!");
    }
    
    private static bool IsValidInputZip(string path)
    {
        return path != null && (File.Exists(path) || Path.GetExtension(path).ToLower() == ".zip");
    }

    private static bool IsValidInputZip(FileInfo path)
    {
        return path is not null && IsValidInputZip(path.FullName);
    }
}