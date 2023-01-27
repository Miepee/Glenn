using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using GlennLib;

namespace GlennCLI;


internal static class Program
{
// TODO: implement launcher flag -u launcher

    private static void OutputHandlerDelegate(string output) => Console.WriteLine(output);

    private static int Main(string[] args)
    {
        //LauncherMods.PortLauncherMod("/home/narr/Downloads/UnofficialMultitroidAPKTest1_6b.zip", Core.ModOS.Linux, true, "./foo.zip");
        var interactiveOption = new Option<bool>(new[] { "-i", "--interactive" }, "Use an interactive mode. This will ignore all other options.");
        var fileOption = new Option<FileInfo>(new[] { "-f", "--file" }, "The file path to the raw mod that should be ported. *REQUIRED IN NON-INTERACTIVE*");
        var windowsOption = new Option<FileInfo>(new[] { "-w", "--windows" }, "The output file path for the Windows mod. None given equals to no Windows port.");
        var linuxOption = new Option<FileInfo>(new[] { "-l", "--linux" }, "The output file path for the Linux mod. None given equals to no Linux port.");
        var androidOption = new Option<FileInfo>(new[] { "-a", "--android" }, "The output file path for the Android mod. None given equals to no Android port.");
        var macOption = new Option<FileInfo>(new[] { "-m", "--mac" }, "The output file path for the Mac mod. None given equals to no Mac port.");
        var iconOption = new Option<FileInfo>(new[] { "-c", "--icon" }, "The file path to an icon PNG that should be used for the taskbar/dock/home screen. " +
                                                                         "If this is not set, it will read \"icon.png\" from the config folder. If that file does not exist, a stock icon will be used.");
        var splashOption = new Option<FileInfo>(new[] { "-p", "--splash" }, "The file path to a splash PNG that should be used when booting the game. " + 
                                                                            "If this is not set, it will read \"splash.png\" from the config folder. " +
                                                                         "If that file does not exist, a stock splash will be used.");
        var customSaveOption = new Option<bool>(new[] { "-s", "--customsave" }, "Whether the Android Port should use a custom save location. Has no effect on other OS.");
        
        var internetOption = new Option<bool>(new[] { "-n", "--internet" }, "Add internet usage permissions to the Android mod. Has no effect on other OS.");
        var verboseOption = new Option<bool>(new[] { "-v", "--verbose" }, "Whether to show verbose output.");

        RootCommand rootCommand = new RootCommand("A utility to port AM2R Mods to other operating systems.")
        {
            interactiveOption,
            fileOption,
            windowsOption,
            linuxOption,
            androidOption,
            macOption,
            iconOption,
            splashOption,
            customSaveOption,
            internetOption
        };

        rootCommand.SetHandler(context =>
        {
            bool interactive = context.ParseResult.GetValueForOption(interactiveOption);
            FileInfo inputModPath = context.ParseResult.GetValueForOption(fileOption);
            FileInfo windowsPath = context.ParseResult.GetValueForOption(windowsOption);
            FileInfo linuxPath = context.ParseResult.GetValueForOption(linuxOption);
            FileInfo androidPath = context.ParseResult.GetValueForOption(androidOption);
            FileInfo macPath = context.ParseResult.GetValueForOption(macOption);
            FileInfo splashPath = context.ParseResult.GetValueForOption(splashOption);
            bool useCustomSave = context.ParseResult.GetValueForOption(customSaveOption);
            bool usesInternet = context.ParseResult.GetValueForOption(internetOption);
            bool beVerbose = context.ParseResult.GetValueForOption(verboseOption);
            FileInfo iconPath = context.ParseResult.GetValueForOption(iconOption);

            return RootMethod(interactive, inputModPath, windowsPath, linuxPath, androidPath, macPath, splashPath, iconPath, useCustomSave, usesInternet, beVerbose);
        });
        
        return rootCommand.InvokeAsync(args).Result;
    }
#pragma warning disable CS1998
    private static async Task<int> RootMethod(bool interactive, FileInfo inputModPath, FileInfo windowsPath, FileInfo linuxPath, FileInfo androidPath, FileInfo macPath,
                                              FileInfo iconPath, FileInfo splashPath, bool useCustomSave, bool usesInternet, bool beVerbose)
#pragma warning restore CS1998
    {
        if (interactive || beVerbose)
            Console.WriteLine("GlennCLI v" + Core.Version);
        
        if (interactive)
        {
            RunInteractive();
            return 0;
        }
        
        if (!IsValidInputZip(inputModPath))
        {
            Console.Error.WriteLine("Input path does not exist, or does not point to a zip file!");
            return 1;
        }
        
        void LocalOutput(string output)
        {
            if (beVerbose)
                OutputHandlerDelegate(output);
        }
        
        iconPath = new FileInfo(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), iconPath?.FullName));
        splashPath = new FileInfo(RawMods.GetProperPathToBuiltinIcons(nameof(Resources.splash), splashPath?.FullName));

        if (beVerbose)
        {
            Console.WriteLine("Use " + iconPath.FullName + " as a path for icons.");
            Console.WriteLine("Use " + splashPath.FullName + " as a path for splash screen.");
        }
        
        if (windowsPath is not null)
        {
            RawMods.PortToWindows(inputModPath.FullName, windowsPath.FullName, LocalOutput);
        }
        if (linuxPath is not null)
        {
            RawMods.PortToLinux(inputModPath.FullName, linuxPath.FullName, iconPath?.FullName, splashPath?.FullName, LocalOutput);
        }
        if (androidPath is not null)
        {
            RawMods.PortToAndroid(inputModPath.FullName, androidPath.FullName,
                                  iconPath?.FullName, splashPath?.FullName, useCustomSave, usesInternet, LocalOutput);
        }
        if (macPath is not null)
        {
            RawMods.PortToMac(inputModPath.FullName, macPath.FullName, iconPath?.FullName, splashPath?.FullName, LocalOutput);
        }
        if (beVerbose)
            Console.WriteLine("Done.");
        return 0;
    }
    private static void RunInteractive()
    {
        Console.WriteLine("THIS TOOL ONLY WORKS FOR MODS BASED ON THE COMMUNITY UPDATES! MODS BASED ON 1.1 WILL NOT WORK!");
        
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
        bool windowsSelected = false;
        bool linuxSelected = false;
        bool androidSelected = false;
        bool macSelected = false;
        bool invalidOS = true;
        do
        {
            Console.WriteLine("Select the platforms you want to port to:");
            Console.WriteLine($"1 - Windows (currently {(windowsSelected ? "on" : "off")})");
            Console.WriteLine($"2 - Linux (currently {(linuxSelected ? "on" : "off")})");
            Console.WriteLine($"3 - Android (currently {(androidSelected ? "on" : "off")})");
            Console.WriteLine($"4 - Mac (currently {(macSelected ? "on" : "off")})");
            Console.WriteLine("5 - Port!");
            var input = Console.ReadKey().Key.ToString();
            Console.WriteLine();

            switch (input)
            {
                case "D1": windowsSelected = !windowsSelected; break;
                
                case "D2": linuxSelected = !linuxSelected; break;

                case "D3": androidSelected = !androidSelected; break;

                case "D4": macSelected = !macSelected; break;
                
                case "D5":
                    if (windowsSelected || linuxSelected || androidSelected || macSelected)
                        invalidOS = false;
                    else
                        Console.WriteLine("You have to select at least one OS!");
                    break;
            }
        } while (invalidOS);
        
        // Ask for Icon
        Console.WriteLine("Insert the path to your custom PNG icon. If an empty or nonexistant file is provided, the defaults will be used instead.");
        string iconPath = Console.ReadLine();
        if (String.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath)) iconPath = null;

        // Ask for Splash
        Console.WriteLine("Insert the path to your custom PNG splash. If an empty or nonexistant file is provided, the defaults will be used instead.");
        string splashPath = Console.ReadLine();
        if (String.IsNullOrWhiteSpace(splashPath) || !File.Exists(splashPath)) splashPath = null;
        
        // If any of the paths were null, we fix them here to use defaults
        iconPath = RawMods.GetProperPathToBuiltinIcons(nameof(Resources.icon), iconPath);
        splashPath = RawMods.GetProperPathToBuiltinIcons(nameof(Resources.splash), splashPath);
        
        // Port everything
        string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        string linuxPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_LINUX.zip";
        string androidPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_ANDROID.apk";
        string macPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_MACOS.zip";

        if (linuxSelected)
        {
            if (File.Exists(linuxPath))
                File.Delete(linuxPath);
            
            RawMods.PortToLinux(modZipPath, linuxPath, iconPath, splashPath, OutputHandlerDelegate);
        }

        if (androidSelected)
        {
            if (File.Exists(androidPath))
                File.Delete(androidPath);
            
            bool? internetSelected = null;
            do
            {
                Console.WriteLine("Does your mod require internet access (y/n)?");
                var input = Console.ReadKey().Key;
                switch (input)
                {
                    case ConsoleKey.Y: internetSelected = true; break;
                    case ConsoleKey.N: internetSelected = false; break;
                    default: Console.WriteLine("Invalid input!"); break;
                }
                Console.WriteLine();
            }
            while (internetSelected == null);
            
            bool? customSaveSelected = null;
            do
            {
                Console.WriteLine("Do you want to use a custom save location for Android (y/n)?");
                var input = Console.ReadKey().Key;
                switch (input)
                {
                    case ConsoleKey.Y: customSaveSelected = true; break;
                    case ConsoleKey.N: customSaveSelected = false; break;
                    default: Console.WriteLine("Invalid input!"); break;
                }
                Console.WriteLine();
            }
            while (customSaveSelected == null);

            RawMods.PortToAndroid(modZipPath, androidPath, iconPath, splashPath, customSaveSelected.Value, 
                                  customSaveSelected.Value, OutputHandlerDelegate);
        }
        if (macSelected)
        {
            if (File.Exists(macPath))
                File.Delete(macPath);
            
            RawMods.PortToMac(modZipPath, macPath,  iconPath, splashPath, OutputHandlerDelegate);
        }
        
        Console.WriteLine("Successfully finished!");
    }
    
    // We want people to also provide zips that don't end in .zip. If it turns out to not be a zip, it'll still throw later.
    private static bool IsValidInputZip(string path)
    {
        return File.Exists(path);
    }

    private static bool IsValidInputZip(FileSystemInfo path) => IsValidInputZip(path?.FullName);
    
}