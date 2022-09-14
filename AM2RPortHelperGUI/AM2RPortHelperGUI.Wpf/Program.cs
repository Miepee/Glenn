using System;
using Eto.Forms;

namespace AM2RPortHelperGUI.Wpf;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        new Application(Eto.Platforms.WinForms).Run(new MainForm());
    }
}