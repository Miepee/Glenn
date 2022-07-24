using System;
using System.IO;
using System.Threading;
using AM2RPortHelperLib;

namespace AM2RPortHelper;

internal static class Program
{
    private const string version = "1.3";

    //TODO: add "-l" flag. transfer launcher mods to each other.
    
    private static void Main(string[] args)
    {
        Console.WriteLine("AM2RPortHelperCLI v" + version);

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
            case "D1": PortHelper.PortWindowsToLinux(modZipPath); break;

            case "D2": PortHelper.PortWindowsToAndroid(modZipPath); break;

            case "D3": PortHelper.PortWindowsToMac(modZipPath); break;

            default: Console.WriteLine("Unacceptable input. Aborting..."); return;
        }
        Console.WriteLine("Successfully finished!");
        Console.WriteLine("Exiting in 3 seconds...");
        Thread.Sleep(3000);
    }
}