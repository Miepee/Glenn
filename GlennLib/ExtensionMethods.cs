namespace GlennLib;

public static class ExtensionMethods
{
    public static void SendOutput(this ModsBase.OutputHandlerDelegate outputDelegate, string output)
    {
        outputDelegate?.Invoke(output);
    }
}