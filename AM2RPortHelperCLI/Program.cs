using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AM2RPortHelperLib;

namespace AM2RPortHelper;


internal static class Program
{
// TODO: implement launcher flag -u launcher

    private static void OutputHandlerDelegate(string output) => Console.WriteLine(output);

    private static int Main(string[] args)
    {
        //PortHelper.PortLauncherMod("/home/narr/Downloads/UnofficialMultitroidAPKTest1_6a.zip", "Linux", true, "./foo.zip");
        
        var interactiveOption = new Option<bool>(new[] { "-i", "--interactive" }, "Use an interactive mode. This will ignore all other options.");
        var fileOption = new Option<FileInfo>(new[] { "-f", "--file" }, "The file path to the raw mod that should be ported. *REQUIRED IN NON-INTERACTIVE*");
        var linuxOption = new Option<FileInfo>(new[] { "-l", "--linux" }, "The output file path for the Linux mod. None given equals to no Linux port.");
        var androidOption = new Option<FileInfo>(new[] { "-a", "--android" }, "The output file path for the Android mod. None given equals to no Android port.");
        var macOption = new Option<FileInfo>(new[] { "-m", "--mac" }, "The output file path for the Mac mod. None given equals to no Mac port.");
        var nameOption = new Option<string>(new[] { "-n", "--name" }, "The name used for the Mac or Android mod. Required for the Mac option, and optional for the Android version. Has no effect on anything else.");
        var internetOption = new Option<bool>(new[] { "-w", "--internet" }, "Add internet usage permissions to the Android mod. Has no effect to other OS.");
        var verboseOption = new Option<bool>(new[] { "-v", "--verbose" }, "Whether to show verbose output.");

        RootCommand rootCommand = new RootCommand("A utility to port Windows AM2R Mods to other operating systems.")
        {
            interactiveOption,
            fileOption,
            linuxOption,
            androidOption,
            macOption,
            nameOption,
            internetOption
        };
        rootCommand.SetHandler(RootMethod, interactiveOption, fileOption, linuxOption, androidOption,
                               macOption, nameOption, internetOption, verboseOption);


        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseHelp(ctx => { ctx.HelpBuilder.CustomizeLayout(_ => HelpBuilder.Default.GetLayout().Prepend(_ => Console.WriteLine("AM2RPortHelperCLI v" + PortHelper.Version)));})
            .Build();
        
        return parser.Invoke(args);
        //return rootCommand.Invoke(args);

        /*
         TODO: show this somewhere? maybe?
        Console.WriteLine("\n**Make sure to replace the icon.png and splash.png with custom ones if you don't want to have placeholders**\n");
        Console.WriteLine("THIS ONLY WORKS FOR MODS BASED ON THE COMMUNITY UPDATES! MODS BASED ON 1.1 WILL NOT WORK!");
        */

    }
#pragma warning disable CS1998
    private static async Task<int> RootMethod(bool interactive, FileInfo inputModPath, FileInfo linuxPath, FileInfo androidPath, FileInfo macPath,
                                              string modName, bool usesInternet, bool beVerbose)
#pragma warning restore CS1998
    {
        if (interactive || beVerbose)
            Console.WriteLine("AM2RPortHelperCLI v" + PortHelper.Version);
        
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

        if (linuxPath is not null)
        {
            PortHelper.PortWindowsToLinux(inputModPath.FullName, linuxPath.FullName, beVerbose ? OutputHandlerDelegate : null);
        }
        if (androidPath is not null)
        {
            PortHelper.PortWindowsToAndroid(inputModPath.FullName, androidPath.FullName,
                                            String.IsNullOrWhiteSpace(modName) ? null : modName, usesInternet, beVerbose ? OutputHandlerDelegate : null);
        }
        if (macPath is not null)
        {
            if (modName is null)
            {
                Console.Error.WriteLine("Mac option was chosen but mod name was not given!");
                return 1;
            }
            PortHelper.PortWindowsToMac(inputModPath.FullName, macPath.FullName, modName, beVerbose ? OutputHandlerDelegate : null);
        }
        if (beVerbose)
            Console.WriteLine("Done.");
        return 0;
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
                        Console.WriteLine("You have to select at least one OS!");
                    break;
            }
        } while (invalidOS);

        // Port everything
        string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        string linuxPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_LINUX.zip";
        string androidPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_ANDROID.apk";
        string macPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_MACOS.zip";

        if (linuxSelected)
        {
            if (File.Exists(linuxPath))
                File.Delete(linuxPath);
            
            PortHelper.PortWindowsToLinux(modZipPath, linuxPath, OutputHandlerDelegate);
        }

        if (androidSelected)
        {
            if (File.Exists(androidPath))
                File.Delete(androidPath);
            
            // TODO: ask for modname
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

            PortHelper.PortWindowsToAndroid(modZipPath, androidPath, null, internetSelected.Value, OutputHandlerDelegate);
        }
        if (macSelected)
        {
            if (File.Exists(macPath))
                File.Delete(macPath);
            
            Console.WriteLine("Mac requires a name! Please enter one (no special characters!):");
            string modName = Console.ReadLine();
            PortHelper.PortWindowsToMac(modZipPath, macPath, modName, OutputHandlerDelegate);
        }
        
        Console.WriteLine("Successfully finished!");
    }
    
    // TODO: we should probably check for magic header instead of just file extension
    private static bool IsValidInputZip(string path)
    {
        return path != null && (File.Exists(path) || Path.GetExtension(path).ToLower() == ".zip");
    }

    private static bool IsValidInputZip(FileInfo path)
    {
        return IsValidInputZip(path?.FullName);
    }
}