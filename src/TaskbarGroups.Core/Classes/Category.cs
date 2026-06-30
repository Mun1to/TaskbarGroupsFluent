using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace TaskbarGroups.Core
{
    public class Category
    {
        public string Name = "";
        public string ColorString = System.Drawing.ColorTranslator.ToHtml(Color.FromArgb(31, 31, 31));
        public bool allowOpenAll = false;
        public List<ProgramShortcut> ShortcutList;
        public int Width; // not used aon
        public double Opacity = 10;
        public String HoverColor;
        public int IconSize = 30;
        public int Separation = 8;

        Regex specialCharRegex = new Regex("[*'\",_&#^@]");

        private static int[] iconSizes = new int[] {16,32,64,128,256,512};
        private string path;

        public Category(string inputPath)
        {
            path = inputPath;
            // Use application's absolute path; (grabs the .exe)
            // Gets the parent folder of the exe and concats the rest of the path
            string fullPath;

            // Check if path is a full directory or part of a file name
            // Passed from the main shortcut client and the config client


            // This if won't ever be true, because the path passed in is a full path to a folder.
            /*
            if (System.IO.File.Exists(@Paths.path + @"\" + path + @"\ObjectData.xml"))
            {
                fullPath = @Paths.path + @"\" + path + @"\ObjectData.xml";
            }
            else
            {
            */
            fullPath = Path.GetFullPath(Path.Combine(inputPath, "ObjectData.xml"));
            /*
            }
            */

            System.Xml.Serialization.XmlSerializer reader =
                new System.Xml.Serialization.XmlSerializer(typeof(Category));
            using (StreamReader file = new StreamReader(fullPath))
            {
                Category category = (Category)reader.Deserialize(file);
                this.Name = category.Name;
                this.ShortcutList = category.ShortcutList;
                this.Width = category.Width;
                this.ColorString = category.ColorString;
                this.Opacity = category.Opacity;
                this.allowOpenAll = category.allowOpenAll;
                this.HoverColor = category.HoverColor;
                this.IconSize = category.IconSize;
                this.Separation = category.Separation;
            }

        }

        public String getPath()
        {
            return path;
        }

        public Category() // needed for XML serialization
        {

        }

        public void CreateConfig(Image groupImage)
        {
            try
            {
                //string filePath = path + @"\" + this.Name + "Group.exe";
                //
                // Directory and .exe
                //
                path = Path.Combine(Paths.ConfigPath, this.Name);
                System.IO.Directory.CreateDirectory(@path);

                //System.IO.File.Copy(@"config\config.exe", @filePath);


                writeXML();

                //
                // Create .ico
                //

                Image img = ImageFunctions.ResizeImage(groupImage, 256, 256); // Resize img if too big
                img.Save(Path.Combine(path, "GroupImage.png"));

                if (GetMimeType(groupImage).ToString() == "*.PNG")
                {
                    createMultiIcon(groupImage, Path.Combine(path, "GroupIcon.ico"));
                }
                else
                {
                    using (FileStream fs = new FileStream(Path.Combine(path, "GroupIcon.ico"), FileMode.Create))
                    {
                        ImageFunctions.IconFromImage(img).Save(fs);
                        fs.Close();
                    }
                }


                // Through shellLink.cs class, pass through into the function information on how to construct the icon
                // Needed due to needing to set a unique AppUserModelID so the shortcut applications don't stack on the taskbar with the main application
                // Tricks Windows to think they are from different applications even though they are from the same .exe
                // The pinned shortcut launches the background flyout with the group
                // name as its argument. A unique AppUserModelID keeps each pinned
                // group from stacking with the main app on the taskbar.
                ShellLink.InstallShortcut(
                    Paths.BackgroundApplication,
                    "tjackenpacken.taskbarGroup.menu." + this.Name,
                    "\"" + this.Name + "\"",
                    path,
                    Path.Combine(path, "GroupIcon.ico"),
                    Path.Combine(path, this.Name + ".lnk"),
                    this.Name
                );


                // Build the icon cache
                cacheIcons();

                // Move the freshly built .lnk to the shortcuts folder. overwrite:true
                // is essential when re-editing a group — otherwise File.Move throws
                // "cannot create a file that already exists" because the previous
                // save left a .lnk with the same name there.
                System.IO.File.Move(Path.Combine(path, this.Name + ".lnk"),
                    Paths.ShortcutFileFor(this.Name),
                    overwrite: true); // Move .lnk to correct directory
            }
            catch
            {
                // Surface failures to the caller (the editor) instead of swallowing them.
                throw;
            }
        }


        private void writeXML()
        {
            //
            // XML config
            //
            System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(typeof(Category));

            using (FileStream file = System.IO.File.Create(Path.Combine(@path, "ObjectData.xml")))
            {
                writer.Serialize(file, this);
                file.Close();
            }
        }

        private static void createMultiIcon(Image iconImage, string filePath)
        {


            var diffList = from number in iconSizes
                select new
                    {
                        number,
                        difference = Math.Abs(number - iconImage.Height)
                    };
            var nearestSize = (from diffItem in diffList
                          orderby diffItem.difference
                          select diffItem).First().number;

            List<Bitmap> iconList = new List<Bitmap>();

            while (nearestSize != 16)
            {
                iconList.Add(ImageFunctions.ResizeImage(iconImage, nearestSize, nearestSize));
                nearestSize = (int)Math.Round((decimal) nearestSize / 2);
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                IconFactory.SavePngsAsIcon(iconList.ToArray(), stream);
            }
        }

        public Bitmap LoadIconImage() // Needed to access img without occupying read/write
        {
            string path = Path.Combine(Paths.ConfigPath, Name, "GroupImage.png");

            using (MemoryStream ms = new MemoryStream(System.IO.File.ReadAllBytes(path)))
                return new Bitmap(ms);
        }

        // Goal is to create a folder with icons of the programs pre-cached and ready to be read
        // Avoids having the icons need to be rebuilt everytime which takes time and resources
        public void cacheIcons()
        {

            // Defines the paths for the icons folder
            string path = Path.Combine(Paths.ConfigPath, this.Name);
            string iconPath = Path.Combine(path, "Icons");

            // Check and delete current icons folder to completely rebuild the icon cache
            // Only done on re-edits of the group and isn't done usually
            if (Directory.Exists(iconPath))
            {
                Directory.Delete(iconPath, true);
            }

            // Creates the icons folder inside of existing config folder for the group
            Directory.CreateDirectory(iconPath);

            // Loops through each shortcut added by the user and gets the icon
            // Writes the icon to the new folder in a .jpg format
            // Namign scheme for the files are done through Path.GetFileNameWithoutExtension()

            for (int i = 0; i < ShortcutList.Count; i++)
            {
                ProgramShortcut ps = ShortcutList[i];
                string savePath = Path.Combine(iconPath, generateMD5Hash(ps.FilePath + ps.Arguments) + ".png");
                using (Image img = ResolveShortcutImage(ps))
                    img?.Save(savePath, ImageFormat.Png);
            }
        }

        // Resolves the icon image for a shortcut directly from its target,
        // decoupled from any UI control. Used by both cacheIcons() and the
        // on-demand cache rebuild in loadImageCache().
        public static Image ResolveShortcutImage(ProgramShortcut shortcutObject)
        {
            string programPath = shortcutObject.FilePath;

            try
            {
                if (shortcutObject.isWindowsApp)
                {
                    // Resolve the UWP app's icon from its AppxManifest via PackageManager.
                    try { return handleWindowsApp.getWindowsAppIcon(programPath, true); }
                    catch { return GetErrorImage(); }
                }

                // High-resolution (256px) icon for files, folders and shortcuts,
                // free of the shortcut-arrow overlay.
                Image highRes = HighResIcon.GetIcon(programPath);
                if (highRes != null)
                    return highRes;

                // Fallbacks if the jumbo image list isn't available.
                if (Directory.Exists(programPath))
                    return handleFolder.GetFolderIcon(programPath).ToBitmap();
                return Icon.ExtractAssociatedIcon(programPath).ToBitmap();
            }
            catch
            {
                return GetErrorImage();
            }
        }

        // Generated fallback used when an icon cannot be resolved, replacing the
        // previously UI-embedded Resources.Error bitmap.
        public static Image GetErrorImage()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(60, 60, 60));
                using (var pen = new Pen(Color.FromArgb(200, 90, 90), 3))
                {
                    g.DrawLine(pen, 8, 8, 24, 24);
                    g.DrawLine(pen, 24, 8, 8, 24);
                }
            }
            return bmp;
        }

        // Try to load an iamge from the cache
        // Takes in a programPath (shortcut) and processes it to the proper file name
        public Image loadImageCache(ProgramShortcut shortcutObject)
        {

            String programPath = shortcutObject.FilePath;

            if (System.IO.File.Exists(programPath) || Directory.Exists(programPath) || shortcutObject.isWindowsApp)
            {
                try
                {
                    // Try to construct the path like if it existed
                    // If it does, directly load it into memory and return it
                    // If not then it would throw an exception in which the below code would catch it
                    String cacheImagePath = generateCachePath(shortcutObject);

                    using (MemoryStream ms = new MemoryStream(System.IO.File.ReadAllBytes(cacheImagePath)))
                        return Image.FromStream(ms);
                    
                }
                catch (Exception)
                {
                    // Try to recreate the cache icon image and catch and missing file/icon situations that may arise

                    // Checks if the original file even exists to make sure to not do any extra operations

                    // Same processing as above in cacheIcons()
                    String path = Path.Combine(Paths.ConfigPath, this.Name, "Icons", generateMD5Hash(programPath + shortcutObject.Arguments) +".png");

                    // Resolve the icon directly from the target (decoupled from UI)
                    Image finalImage = ResolveShortcutImage(shortcutObject);

                    // Save the icon after it has been fetched by previous code,
                    // recreating the cache folder if it was removed.
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    finalImage.Save(path, ImageFormat.Png);

                    // Return the said image
                    return finalImage;
                }
            }
            else
            {
                return GetErrorImage();
            }
        }

        public String generateCachePath(ProgramShortcut ps)
        {
            /*
            return @Path.GetDirectoryName(Application.ExecutablePath) +
                        @"\config\" + this.Name + @"\Icons\" + ((shortcutObject.isWindowsApp) ? specialCharRegex.Replace(programPath, string.Empty) :
                        @Path.GetFileNameWithoutExtension(programPath)) + (Directory.Exists(programPath) ? "_FolderObjTSKGRoup.png" : ".png");
            */

            return Path.Combine(Paths.ConfigPath, this.Name, "Icons",
                        generateMD5Hash(ps.FilePath + ps.Arguments) + ".png");
        }

        public static string GetMimeType(Image i)
        {
            var imgguid = i.RawFormat.Guid;
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageDecoders())
            {
                if (codec.FormatID == imgguid)
                    return codec.FilenameExtension;
            }
            return "image/unknown";
        }

        public Color calculateHoverColor()
        {
            Color BackColor = ImageFunctions.FromString(ColorString);
            if (BackColor.R * 0.2126 + BackColor.G * 0.7152 + BackColor.B * 0.0722 > 255 / 2)
            {
                // Do prior calculations on darker colors to prevent color values going negative
                int backColorR = BackColor.R - 50 >= 0 ? BackColor.R - 50 : 0;
                int backColorG = BackColor.G - 50 >= 0 ? BackColor.G - 50 : 0;
                int backColorB = BackColor.B - 50 >= 0 ? BackColor.B - 50 : 0;

                //if backcolor is light, set hover color as darker
                return Color.FromArgb(BackColor.A, backColorR, backColorG, backColorB);
            }
            else
            {
                // Do prior calculations on darker colors to prevent color values going over 255
                int backColorR = BackColor.R + 50 <= 255 ? BackColor.R + 50 : 255;
                int backColorG = BackColor.G + 50 <= 255 ? BackColor.G + 50 : 255;
                int backColorB = BackColor.B + 50 <= 255 ? BackColor.B + 50 : 255;

                //light backcolor is light, set hover color as darker
                return Color.FromArgb(BackColor.A, (BackColor.R + 50), (BackColor.G + 50), (BackColor.B + 50));
            }
        }

        private String generateMD5Hash(String s)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(s);
                byte[] hashBytes = md5.ComputeHash(inputBytes);


                StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                     sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        //
        // END OF CLASS
        //

        public static void closeBackgroundApp(string path = "")
        {
            Process[] pname = Process.GetProcessesByName(Path.GetFileNameWithoutExtension("Taskbar Groups Background"));
            if (pname.Length != 0)
            {
                Process bkg = pname[0];

                Process p = new Process();
                if (path == "")
                {
                    path = Paths.BackgroundApplication;
                }
                p.StartInfo.FileName = path;
                p.StartInfo.Arguments = "exitApplicationModeReserved";
                p.Start();

                if(!bkg.WaitForExit(2000))
                {
                    bkg.Kill();
                }
            }
            
        }
    }
}

