using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AM2RPortHelperLib;

public static partial class PortHelper
{
    /// <summary>
    /// Recursively lowercases all files and folders from a specified directory.
    /// </summary>
    /// <param name="directory">The path to the directory whose contents should be lowercased.</param>
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
            // ReSharper disable once PossibleNullReferenceException - since this is a subdirectory, it always has a parent
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
    
    /// <summary>
    /// Loads an <see cref="Image"/> via filepath, resizes it via Nearest Neighbor to a specified dimension, and then saves it to a specified path.
    /// </summary>
    /// <param name="iconPath">The path to the image to resize and save.</param>
    /// <param name="dimensions">The dimensions <paramref name="iconPath"/> should be resized to.</param>
    /// <param name="filePath">The filepath where the resized image should be saved to.</param>
    /// <example>
    /// <code>
    /// Image iconPath = Image.Load("iconPath.png");
    /// SaveAndroidIcon(iconPath, 128, "128.png");
    /// </code>
    /// </example>
    private static void SaveAndroidIcon(string iconPath, int dimensions, string filePath)
    {
        Image picture = Image.Load(iconPath);
        picture.Mutate(x => x.Resize(dimensions, dimensions, KnownResamplers.NearestNeighbor));
        picture.SaveAsPng(filePath);
    }

    /// <summary>
    /// Calculates the SHA256 hash of a specified file.
    /// </summary>
    /// <param name="filename">The full filepath to a file whose SHA256 hash should be calculated.</param>
    /// <returns>The SHA256 hash of <see cref="filename"/>.</returns>
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