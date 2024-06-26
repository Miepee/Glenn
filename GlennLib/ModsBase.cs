namespace GlennLib;

public abstract class ModsBase
{
    public delegate void OutputHandlerDelegate(string output);
    
    // Create before accessing anything
    static ModsBase()
    {
        Directory.CreateDirectory(TempDir);
        Directory.CreateDirectory(UtilDir);
    }

    /// <summary>
    /// A temporary directory
    /// </summary>
    protected static string TempDir => CreateAndReturnTempPath();

    private static string CreateAndReturnTempPath()
    {
        string path = Path.GetTempPath() + "/GlennPortHelper/";
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// The current directory of the program.
    /// </summary>
    protected static readonly string CurrentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
    
    /// <summary>
    /// The "utils" folder that's shipped with the tool.
    /// </summary>
    protected static readonly string UtilDir = CurrentDir + "/utils";
}