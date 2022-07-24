using System;
using System.CommandLine;
using System.IO;
using AM2RPortHelperLib;

namespace AM2RPortHelper;

internal static class Program
{
    private const string version = "1.3";

    //TODO: add "-l" flag. transfer launcher mods to each other.
    
    private static int Main(string[] args)
    {
        Console.WriteLine("AM2RPortHelperCLI v" + version);

        //TODO!
        var interactiveOption = new Option<bool>(new[] {"-i", "--interactive"}, "Use an interactive mode");

        RootCommand rootCommand = new RootCommand();
        rootCommand.AddOption(interactiveOption);
        rootCommand.SetHandler(RootMethod, interactiveOption);

        return rootCommand.Invoke(args);
        
        /*
        Console.WriteLine("\n**Make sure to replace the icon.png and splash.png with custom ones if you don't want to have placeholders**\n");
        Console.WriteLine("THIS ONLY WORKS FOR MODS BASED ON THE COMMUNITY UPDATES! MODS BASED ON 1.1 WILL NOT WORK!");
        Console.WriteLine("To which platform do you want to port to?\n1 - Linux\n2 - Android\n3 - MacOS");
        */

    }
    private static void RootMethod(bool interactive)
    {
        if (interactive)
        {
            RunInteractive();
            return;
        }
        var x = 0;
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

            if (modZipPath == null || (!File.Exists(modZipPath) && Path.GetExtension(modZipPath).ToLower() != ".zip"))
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
}