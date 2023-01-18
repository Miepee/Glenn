namespace AM2RPortHelperLib;

public abstract class ModsBase
{
    public delegate void OutputHandlerDelegate(string output);

    protected static OutputHandlerDelegate OutputHandler;

    protected static void SendOutput(string output)
    {
        OutputHandler?.Invoke(output);
    }

    /// <summary>
    /// A temporary directory
    /// </summary>
    protected static readonly string TempDir = Path.GetTempPath() + "/PortHelper/";
    
    /// <summary>
    /// The current directory of the AM2RPortHelper program.
    /// </summary>
    protected static readonly string CurrentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
    
    /// <summary>
    /// The "utils" folder that's shipped with the AM2RPortHelper.
    /// </summary>
    protected static readonly string UtilDir = CurrentDir + "/utils";
}