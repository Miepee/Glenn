namespace GlennLib;

public static class Core
{
    private static string ReturnAndCreateConfigDir()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create) + "/GlennPortHelper/";
        Directory.CreateDirectory(path);
        return path;
    }
    
    /// <summary>
    /// The full path to the tools's place in Configuration Application Data.
    /// </summary>
    public static readonly string ConfigDir = ReturnAndCreateConfigDir();
    
    /// <summary>
    /// The current version of <see cref="GlennLib"/>.
    /// </summary>
    public const string Version = "1.4";

    public enum ModOS
    {
        Windows,
        Linux,
        Mac,
        Android
    }
}