namespace AM2RPortHelperLib;

public abstract class IMods
{
    public delegate void OutputHandlerDelegate(string output);

    protected static OutputHandlerDelegate outputHandler;

    protected static void SendOutput(string output)
    {
        outputHandler?.Invoke(output);
    }

    /// <summary>
    /// A temporary directory
    /// </summary>
    protected static readonly string tmp = Path.GetTempPath() + "/PortHelper/";
    
    /// <summary>
    /// The current directory of the AM2RPortHelper program.
    /// </summary>
    protected static readonly string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
    
    /// <summary>
    /// The "utils" folder that's shipped with the AM2RPortHelper.
    /// </summary>
    protected static readonly string utilDir = currentDir + "/utils";
}