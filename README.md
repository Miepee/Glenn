# AM2RPortHelper
A simple tool to make porting Windows builds of AM2R-Mods to other platforms easier. This will *only* work with VM-mods of the Community Updates, neither YYC mods nor mods of the original 1.1 will work.  
You need to have at least .NET Core 5 installed in order to run this.
 
## How do I use this?
Simply compress your mod as a zip, and then drag-n-drop it into this tool. After that, select if you want to have a port made for Linux or Android. A zip/apk that can be used for the [AM2RModPacker](https://github.com/Miepee/AM2RModpacker-Mac) will then be created into the directory next to the program.  

To create Android builds, you need to have Java installed.

By default, the ports will use stock splash and icon images. Replacing `icon.png`, `splash.png` and `splashAndroid.png` in the utils folder with your own is recommended.

## Porting notes
- Keep Game Maker: Studio documentation in mind, using any functions that only work on one OS or function differently on different OS could lead to the ports having unexpected behaviour or even crashing.
- Use `/` for folder seperation, instead of `\`. `\` only works on Windows, `/` works on every OS.

### Linux
- Don't write to the asset folder. Linux is distributed as an AppImage, which makes that whole directory read only and such operations would lead to a crash. Create the files you need in `working_directory` on game boot if they don't exist, and then write to `working_directory` instead. This will create the files and read from them in `~/.config/<mygame>`. Do not ship those files in the asset folder if you're planning to write to them.

### Mac
-  The following functions will crash the game if you'll use them:
   - `immersion_play_effect` and `immersion_stop`, `font_replace`. Make sure to either create an OS check before using those, or don't use them entirely. ***AM2R uses these by default, make sure to remove them or create an OS check!***
- If you use a custom save directory that's *not* inside of `%localappdata%/AM2R`, but instead in `%localappdata%/MyModWithCoolName` you have to change `com.yoyogames.am2r` to `com.yoyogames.mymodwithcoolname` (needs to be all lowercase) in the following files:
    * `utils/Contents/Info.plist`, .
    * `utils/Contents/Resources/yoyorunner.config`

### Android
- If you use a custom save directory that's *not* inside of `%localappdata%/AM2R`, but instead in `%localappdata%/MyModWithCoolName` you have to change all instances of `com.companyname.AM2RWrapper` to `com.companyname.MyModWithCoolName` in the `AndroidManifest.xml` inside of the `AM2RWrapper.apk`. For this you have to decompile the apk with apktool (`java -jar apktool.jar d AM2RWrapper.apk`), edit the contents, and then rebuild it  (`java -jar b apktool.jar b AM2RWrapper`) and sign it (`java -jar uber-apk-signer.jar -a theNewApk.apk`).
