# AM2RPortHelper
A simple tool to make porting Windows builds of self-created AM2R-Mods to other platforms easier. This will *only* work with VM-mods of the Community Updates, neither YYC mods nor mods of the original 1.1 will work.  
You need to have at least .NET Core 6 installed in order to run this.
 
## How do I use this?
First you need a zip of the self-created mod. If you're using GameMaker: Studio, you can just use the zip it created, if you're using UndertaleModTool you need to zip the files first (your files should not be in a subfolder in the zip!). After that, just drag-n-drop it into this tool and, select if you want to have a port made for Linux, Android or Mac.  
It will then create a new zip/apk, which can be used for the [AM2RModPacker](https://github.com/AM2R-Community-Developers/AM2RModpacker-Mac), in the directory next to the program. For the Mac build, you need to use my ModPacker-Mac version which can be found [here](https://github.com/AM2R-Community-Developers/AM2RModpacker-Mac).

To create Android builds, you need to have Java installed.

By default, the ports will use stock splash and icon images. *It is recommended to replace `icon.png`, `splash.png` and `splashAndroid.png` in the `utils` folder with your own*.

## Porting notes
- Keep Game Maker: Studio documentation in mind, using any functions that only work on one OS or function differently on different OS could lead to the ports having unexpected behaviour or even crashing.
- Use `/` for folder seperation, instead of `\`. `\` only works on Windows, `/` works on every OS.
- Every OS except Android will only write and read from "lowercase" inside of `working_directory`. This means, that if you create a directory called `MyCoolDirectory` and read from it with Game Maker functions, it will actually create the directory `mycooldirectory` instead and read from there. This will only become a problem if you create files/folders *outside* of Game Maker. If you do so make sure that those are all in *lowercase*! As Windows is case-insensitive, it doesn't care for the case of files.

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