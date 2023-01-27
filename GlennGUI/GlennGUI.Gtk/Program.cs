using System;
using Eto.Forms;

namespace GlennGUI.Gtk;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        new Application(Eto.Platforms.Gtk).Run(new MainForm());
    }
}