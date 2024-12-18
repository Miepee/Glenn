using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GlennLib;
using Eto.Forms;
using Eto.Drawing;
using ResourcesLib = GlennLib.Resources;

namespace GlennGUI;

public partial class MainForm : Form

// TODO: clean GUI
{
    private readonly string userIconPath = Core.ConfigDir + "/icon.png";
    private readonly string userSplashPath = Core.ConfigDir + "/splash.png";
    
    private static byte[] GetByteArrayFromResource(string nameOfResource)
    {
        if (File.Exists(Core.ConfigDir + "/" + nameOfResource + ".png"))
            return File.ReadAllBytes(Core.ConfigDir + "/" + nameOfResource + ".png");
        
        return nameOfResource switch
        {
            nameof(ResourcesLib.icon) => ResourcesLib.icon,
            nameof(ResourcesLib.splash) => ResourcesLib.splash,
            _ => throw new InvalidDataException("Invalid Resource name given!")
        };
    }
    
    public MainForm()
    {
        Title = $"Glenn - v{Core.Version}";
        MinimumSize = new Size(260, 280);

        Icon = new Icon(1f, new Bitmap(Resources.applicationIcon));
        
        var mainLayout = new DynamicLayout() {Padding = 10};
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
        mainLayout.AddRow(checkboxWindows, checkboxLinux, checkboxAndroid, checkboxMac);
        mainLayout.AddSpace();
        mainLayout.EndCentered();
        mainLayout.BeginCentered();
        mainLayout.AddRow(new Label { Height = 15 });
        mainLayout.AddRow(labelOptionsHeader);
        mainLayout.EndCentered();
        mainLayout.BeginCentered();
        mainLayout.AddRow(checkboxAndroidRequiresInternet);
        mainLayout.EndCentered();
        mainLayout.AddRow(new Label { Height = 5 });
        mainLayout.BeginCentered();
        mainLayout.AddRow(checkboxUseCustomSave);
        mainLayout.EndCentered();
        mainLayout.AddRow(new Label { Height = 5 });
        mainLayout.BeginCentered();
        mainLayout.AddRow(buttonEditIcon);
        mainLayout.AddRow(new Label { Height = 5 });
        mainLayout.AddRow(imageViewIcon);
        mainLayout.AddRow(new Label { Height = 5 });
        mainLayout.EndCentered();
        mainLayout.AddSpace();
        mainLayout.AddRow(new Label { Height = 5 });
        mainLayout.BeginCentered();
        mainLayout.AddRow(buttonEditSplash);
        mainLayout.AddRow(new Label { Height = 5 });
        mainLayout.AddRow(imageViewSplash);
        mainLayout.AddRow(new Label { Height = 5 });
        mainLayout.EndCentered();
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
        buttonEditIcon.Click += ButtonEditIconClick;
        buttonEditSplash.Click += ButtonEditSplashClick;
    }

    private void ButtonEditSplashClick(object sender, EventArgs e)
    {
        ButtonEditResourceClick(imageViewSplash, nameof(ResourcesLib.splash));
    }
    
    private void ButtonEditIconClick(object sender, EventArgs e)
    {
        ButtonEditResourceClick(imageViewIcon, nameof(ResourcesLib.icon));
    }
    
    private async void ButtonPortOnClick(object sender, EventArgs e)
    {
        SetDisableStatusOfAllElements(true);

        void OutputHandlerDelegate(string output) => Application.Instance.Invoke(() => labelProgress.Text = $"Info: {output}");
        string modZipPath = filePicker.FilePath;
        string currentDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        // TODO: ask where to save them!
        string windowsPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_WINDOWS.zip"; 
        string linuxPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_LINUX.zip";
        string androidPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_ANDROID.apk";
        string macPath = $"{currentDir}/{Path.GetFileNameWithoutExtension(modZipPath)}_MACOS.zip";

        string iconPath = RawMods.GetProperPathToBuiltinIcons(nameof(ResourcesLib.icon), userIconPath);
        string splashPath = RawMods.GetProperPathToBuiltinIcons(nameof(ResourcesLib.splash), userSplashPath);

        bool checkIfOutputIsValid(string pathToZip)
        {
            bool errorThrown = false;
            try
            {
                ZipFile.OpenRead(pathToZip);
            }
            catch (Exception)
            {
                errorThrown = true;
            }

            return errorThrown;
        }
        
        // TODO: handle when porting methods throw exception
        try
        {
            if (checkboxWindows.Checked.Value)
            {
                if (File.Exists(windowsPath))
                    File.Delete(windowsPath);

                await Task.Run(() => RawMods.PortToWindows(modZipPath, windowsPath, OutputHandlerDelegate));
                if (!checkIfOutputIsValid(windowsPath))
                {
                    MessageBox.Show(this, "The Windows port output somehow got corrupted. Deleting file.", "Error", MessageBoxType.Error);
                    File.Delete(windowsPath);
                }
            }
            if (checkboxLinux.Checked.Value)
            {
                if (File.Exists(linuxPath))
                    File.Delete(linuxPath);

                await Task.Run(() => RawMods.PortToLinux(modZipPath, linuxPath, iconPath, splashPath, OutputHandlerDelegate));
                if (!checkIfOutputIsValid(linuxPath))
                {
                    MessageBox.Show(this, "The Linux port output somehow got corrupted. Deleting file.", "Error", MessageBoxType.Error);
                    File.Delete(windowsPath);
                }
            }
            if (checkboxAndroid.Checked.Value)
            {
                if (File.Exists(androidPath))
                    File.Delete(androidPath);

                bool useCustomSave = checkboxUseCustomSave.Checked.Value;
                bool useInternet = checkboxAndroidRequiresInternet.Checked.Value;
                await Task.Run(() => RawMods.PortToAndroid(modZipPath, androidPath, iconPath, splashPath, useCustomSave, useInternet, OutputHandlerDelegate));
            }
            if (checkboxMac.Checked.Value)
            {
                if (File.Exists(macPath))
                    File.Delete(macPath);
                
                await Task.Run(() => RawMods.PortToMac(modZipPath, macPath, iconPath, splashPath, OutputHandlerDelegate));
                if (!checkIfOutputIsValid(macPath))
                {
                    MessageBox.Show(this, "The Mac port output somehow got corrupted. Deleting file.", "Error", MessageBoxType.Error);
                    File.Delete(windowsPath);
                }
            }

            labelProgress.Text = "Done!";
            OpenFolder(currentDir);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "Stack Trace: " + exception, "Error", MessageBoxType.Error);
            throw;
        }

        SetDisableStatusOfAllElements(false);
    }
    
    // Helper functions
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
        // there needs to be a selected mod + any checkbox
        if (!String.IsNullOrWhiteSpace(filePicker.FilePath) && 
            (checkboxWindows.Checked.Value || checkboxLinux.Checked.Value || checkboxAndroid.Checked.Value || checkboxMac.Checked.Value))
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

    private void ButtonEditResourceClick(ImageView control, string nameOfResource)
    {
        var dialog = new OpenFileDialog() {Title = "Select new " + nameOfResource};
        if (dialog.ShowDialog(this) != DialogResult.Ok)
            return;

        string destName = nameOfResource switch
        {
            nameof(ResourcesLib.icon) => userIconPath,
            nameof(ResourcesLib.splash) => userSplashPath,
            _ => throw new Exception("You dun goofed!")
        };
        
        File.Copy(dialog.FileName, destName, true);
        control.Image = new Bitmap(GetByteArrayFromResource(nameOfResource));
    }

    // Attributes
    #region Attributes
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
    private readonly CheckBox checkboxWindows = new CheckBox
    {
        Text = "Windows"
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
    
    private readonly ImageView imageViewIcon = new ImageView
    {
        Image = new Bitmap(GetByteArrayFromResource(nameof(ResourcesLib.icon))),
        Size = new Size(64, 64)
    };
    private readonly Button buttonEditIcon = new Button
    {
        Text = "Edit Icon:"
    };
    
    private readonly ImageView imageViewSplash = new ImageView
    {
        Image = new Bitmap(GetByteArrayFromResource(nameof(ResourcesLib.splash))),
        Size = new Size(128, 96),
    };
    private readonly Button buttonEditSplash = new Button
    {
        Text = "Edit Splash:"
    };

    private readonly Button buttonPort = new Button
    {
        Text = "Port!",
        Enabled = false
    };
    #endregion
}