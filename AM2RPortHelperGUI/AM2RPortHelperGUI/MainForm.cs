using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AM2RPortHelperLib;
using Eto.Forms;
using Eto.Drawing;

namespace AM2RPortHelperGUI;

public partial class MainForm : Form
{
    public MainForm()
    {
        Title = $"AM2RPortHelper - v{PortHelper.Version}";
        MinimumSize = new Size(200, 200);

        
        var mainLayout = new DynamicLayout();
        mainLayout.BeginVertical();
        mainLayout.AddRange(labelSelectMod,
                            new Label { Height = 5 }, 
                            filePicker, 
                            labelProgress,
                            new Label { Height = 10 });
        mainLayout.EndVertical();
        mainLayout.BeginCentered();
        mainLayout.AddRow(checkboxLinux, checkboxAndroid, checkboxMac);
        mainLayout.AddSpace();
        mainLayout.EndCentered();
        mainLayout.BeginVertical();
        mainLayout.AddRange(new Label { Height = 10}, buttonPort, null);
        mainLayout.EndVertical();
        
        var mainPage = new TabPage
        {
            Text = "Raw Mods",
            Content = mainLayout
        };
        
        // TODO: think of a way to present this normally
        var am2rModLayout = new DynamicLayout();
        var am2rModPage = new TabPage
        {
            Text = "AM2RLauncher Mods",
            Content = am2rModLayout
        };
        
        
        Content = new TabControl
        {
            Pages =
            {
                mainPage,
                am2rModPage
            }
        };
        
        // events
        checkboxAndroid.CheckedChanged += ShouldButtonPortBeEnabled;
        checkboxLinux.CheckedChanged += ShouldButtonPortBeEnabled;
        checkboxMac.CheckedChanged += ShouldButtonPortBeEnabled;
        filePicker.FilePathChanged += ShouldButtonPortBeEnabled;
        buttonPort.Click += ButtonPortOnClick;
    }
    
    // Helper functions
    private async void ButtonPortOnClick(object sender, EventArgs e)
    {
        DisableAllElements();

        void OutputHandlerDelegate(string output) => Application.Instance.Invoke(() => labelProgress.Text = $"Info: {output}");
        string modZipPath = filePicker.FilePath;
        string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        string linuxPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_LINUX.zip";
        string androidPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_ANDROID.apk";
        string macPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_MACOS.zip";
        
        if (File.Exists(linuxPath))
            File.Delete(linuxPath);
        if (File.Exists(androidPath))
            File.Delete(androidPath);
        if (File.Exists(macPath))
            File.Delete(macPath);
        
        if (checkboxLinux.Checked.Value)
            await Task.Run(() => PortHelper.PortWindowsToLinux(modZipPath,linuxPath, OutputHandlerDelegate));
        if (checkboxAndroid.Checked.Value)
            await Task.Run(() =>PortHelper.PortWindowsToAndroid(modZipPath, androidPath, OutputHandlerDelegate));
        if (checkboxMac.Checked.Value)
        {
            string modName = "foo";//MessageBox.Show()
            await Task.Run(() => PortHelper.PortWindowsToMac(modZipPath, macPath, modName, OutputHandlerDelegate));
        }

        labelProgress.Text = "Done!";
        OpenFolder(currentDir);
        
        EnableAllElements();
    }
    
    private static void OpenFolder(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start("explorer.exe", $"\"{path}\"");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", $"\"{path}\"");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", $"\"{path}\"");
    }
    
    private void ShouldButtonPortBeEnabled(object sender, EventArgs e)
    {
        // any checkbox + selected mod
        if ((checkboxAndroid.Checked.Value || checkboxLinux.Checked.Value || checkboxMac.Checked.Value) 
            && !String.IsNullOrWhiteSpace(filePicker.FilePath))
            buttonPort.Enabled = true;
        else
            buttonPort.Enabled = false;
    }

    private void DisableAllElements()
    {
        checkboxAndroid.Enabled = false;
        checkboxLinux.Enabled = false;
        checkboxMac.Enabled = false;
        filePicker.Enabled = false;
        buttonPort.Enabled = false;
    }
    
    private void EnableAllElements()
    {
        checkboxAndroid.Enabled = true;
        checkboxLinux.Enabled = true;
        checkboxMac.Enabled = true;
        filePicker.Enabled = true;
        buttonPort.Enabled = true;
    }

    // Attributes
    private readonly Label labelSelectMod = new Label
    {
        Text = "Select Mod:"
    };
    private readonly FilePicker filePicker = new FilePicker
    {
        Filters = { new FileFilter("Zip file", "*.zip") }
    };

    private readonly CheckBox checkboxLinux = new CheckBox
    {
        Text = "Linux"
    };
    private readonly CheckBox checkboxAndroid = new CheckBox
    {
        Text = "Android"
    };
    private readonly CheckBox checkboxMac = new CheckBox
    {
        Text = "Mac"
    };

    private readonly Button buttonPort = new Button
    {
        Text = "Port!",
        Enabled = false
    };

    private readonly Label labelProgress = new Label
    {
        Text = "Info: "
    };
}