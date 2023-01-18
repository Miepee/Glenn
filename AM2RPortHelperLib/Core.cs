namespace AM2RPortHelperLib;

public static class Core
{
    private static string ReturnAndCreateConfigDir()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create) + "/PortHelper/";
        Console.WriteLine(path);
        Directory.CreateDirectory(path);
        return path;
    }
    
    /// <summary>
    /// The full path to the PortHelper's place in Configuration Application Data.
    /// </summary>
    internal static readonly string ConfigDir = ReturnAndCreateConfigDir();
    
    /// <summary>
    /// The current version of <see cref="AM2RPortHelperLib"/>.
    /// </summary>
    public const string Version = "1.4";

    public enum ModOS
    {
        Windows,
        Linux,
        Mac
    }
}