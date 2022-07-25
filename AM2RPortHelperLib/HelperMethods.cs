using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AM2RPortHelperLib;

public static partial class PortHelper
{
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
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
        
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
    
    private static string CalculateSHA256(string filename)
    {
        // Check if file exists first
        if (!File.Exists(filename))
            return "";

        using FileStream stream = File.OpenRead(filename);
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}