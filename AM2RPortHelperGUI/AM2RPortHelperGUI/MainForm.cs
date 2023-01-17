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
        Title = $"AM2RPortHelper - v{Core.Version}";
        MinimumSize = new Size(260, 280);

        
        var mainLayout = new DynamicLayout();
        mainLayout.BeginVertical();
        mainLayout.AddRange(labelSelectMod,
                            new Label { Height = 5 },
                            filePicker,
                            labelProgress);
        mainLayout.EndVertical();
        mainLayout.AddSpace();
        mainLayout.BeginCentered();
        mainLayout.AddRow(new Label { Height = 15 });
        mainLayout.AddRow(labelOSHeader);
        mainLayout.EndCentered();
        mainLayout.BeginCentered();
        mainLayout.AddRow(checkboxLinux, checkboxAndroid, checkboxMac);
        mainLayout.AddSpace();
        mainLayout.EndCentered();
        mainLayout.BeginCentered();
        mainLayout.AddRow(new Label { Height = 15 });
        mainLayout.AddRow(labelOptionsHeader);
        mainLayout.EndCentered();
        mainLayout.BeginCentered();
        mainLayout.AddRow(checkboxAndroidRequiresInternet);
        mainLayout.EndCentered();
        mainLayout.AddRow(new Label() { Height = 5 });
        mainLayout.BeginCentered();
        mainLayout.AddRow(checkboxUseCustomSave);
        mainLayout.EndCentered();
        mainLayout.AddSpace();
        mainLayout.BeginVertical();
        mainLayout.AddRange(new Label { Height = 10 }, buttonPort, null);
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
        
        //TODO implement this properly and revert
        /*Content = new TabControl
        {
            Pages =
            {
                mainPage,
                am2rModPage
            }
        };*/
        Content = mainLayout;
        
        // events
        checkboxAndroid.CheckedChanged += ShouldButtonPortBeEnabled;
        checkboxAndroidRequiresInternet.CheckedChanged += ShouldButtonPortBeEnabled;
        checkboxLinux.CheckedChanged += ShouldButtonPortBeEnabled;
        checkboxMac.CheckedChanged += ShouldButtonPortBeEnabled;
        checkboxUseCustomSave.TextChanged += ShouldButtonPortBeEnabled;
        filePicker.FilePathChanged += ShouldButtonPortBeEnabled;
        buttonPort.Click += ButtonPortOnClick;
    }
    
    // Helper functions
    private async void ButtonPortOnClick(object sender, EventArgs e)
    {
        SetDisableStatusOfAllElements(true);

        void OutputHandlerDelegate(string output) => Application.Instance.Invoke(() => labelProgress.Text = $"Info: {output}");
        string modZipPath = filePicker.FilePath;
        string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        string linuxPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_LINUX.zip";
        string androidPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_ANDROID.apk";
        string macPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_MACOS.zip";

        if (checkboxLinux.Checked.Value)
        {
            if (File.Exists(linuxPath))
                File.Delete(linuxPath);
            
            await Task.Run(() => RawMods.PortToLinux(modZipPath, linuxPath, OutputHandlerDelegate));
        }
        if (checkboxAndroid.Checked.Value)
        {
            if (File.Exists(androidPath))
                File.Delete(androidPath);

            bool useCustomSave = checkboxUseCustomSave.Checked.Value;
            bool useInternet = checkboxAndroidRequiresInternet.Checked.Value;
            await Task.Run(() => RawMods.PortToAndroid(modZipPath, androidPath, useCustomSave, useInternet, OutputHandlerDelegate));
        }
        if (checkboxMac.Checked.Value)
        {
            if (File.Exists(macPath))
                File.Delete(macPath);
            
            string modName = checkboxUseCustomSave.Text;
            await Task.Run(() => RawMods.PortToMac(modZipPath, macPath, OutputHandlerDelegate));
        }

        labelProgress.Text = "Done!";
        OpenFolder(currentDir);
        
        SetDisableStatusOfAllElements(false);
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
        // there needs to be a selected mod + any checkbox (if mac, then there needs to be a name)
        if ((!String.IsNullOrWhiteSpace(filePicker.FilePath) 
            && ((checkboxAndroid.Checked.Value && !checkboxMac.Checked.Value)) 
            || (checkboxLinux.Checked.Value && !checkboxMac.Checked.Value) 
            || (checkboxMac.Checked.Value && !String.IsNullOrWhiteSpace(checkboxUseCustomSave.Text))))
            
            buttonPort.Enabled = true;
        else
            buttonPort.Enabled = false;
    }

    private void SetDisableStatusOfAllElements(bool disabled)
    {
        checkboxAndroid.Enabled = !disabled;
        checkboxAndroidRequiresInternet.Enabled = !disabled;
        checkboxLinux.Enabled = !disabled;
        checkboxMac.Enabled = !disabled;
        filePicker.Enabled = !disabled;
        buttonPort.Enabled = !disabled;
        checkboxUseCustomSave.Enabled = !disabled;
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
    private readonly Label labelProgress = new Label
    {
        Text = "Info: (currently no ports in progress)"
    };
    

    private readonly Label labelOSHeader = new Label
    {
        Text = "Choose the OS to port to",
        Font = new Font(SystemFont.Bold)
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

    private readonly Label labelOptionsHeader = new Label
    {
        Text = "Choose port options",
        Font = new Font(SystemFont.Bold)
    };
    private readonly CheckBox checkboxAndroidRequiresInternet = new CheckBox
    {
        Text = "Requires internet (See tooltip for info)",
        ToolTip = "Only affects Android. If your mod interacts with the internet in any way (such as multiplayer), you should check this, " +
                  "as otherwise internet functions won't work."
    };
    private readonly CheckBox checkboxUseCustomSave = new CheckBox
    {
        Text = "Use custom save location (See tooltip for info)",
        ToolTip = "Only affects Android. Determines whether Android will use a custom save location based on its display name. If you don't " + 
                  "want your mod overwriting normal AM2R on Android, you should check this."
    };

    private readonly Button buttonPort = new Button
    {
        Text = "Port!",
        Enabled = false
    };
}