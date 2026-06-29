using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using System.Collections.Generic;
using Windows.Management.Deployment;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Core
{
    public class handleWindowsApp
    {

        public static Dictionary<string, string> fileDirectoryCache = new Dictionary<string, string>();

        private static PackageManager pkgManger = new PackageManager();
        public static Bitmap getWindowsAppIcon(String file, bool alreadyAppID = false)
        {
            // Get the app's ID from its shortcut target file (Ex. 4DF9E0F8.Netflix_mcm4njqhnhss8!Netflix.app)
            String microsoftAppName = (!alreadyAppID) ? GetLnkTarget(file)[0] : file;

            // Split the string to get the app name from the beginning (Ex. 4DF9E0F8.Netflix)
            String subAppName = microsoftAppName.Split('!')[0];

            // Loop through each of the folders with the app name to find the one with the manifest + logos
            String appPath = findWindowsAppsFolder(subAppName);

            // Load and read manifest to get the logo path
            XmlDocument appManifest = new XmlDocument();
            appManifest.Load(Path.Combine(appPath, "AppxManifest.xml"));

            XmlNamespaceManager appManifestNamespace = new XmlNamespaceManager(new NameTable());
            appManifestNamespace.AddNamespace("sm", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

            String logoLocation = (appManifest.SelectSingleNode("/sm:Package/sm:Properties/sm:Logo", appManifestNamespace).InnerText).Replace("\\", @"\");
            String logoPNG = "";

            if (logoLocation != null && logoLocation != "")
            {
                // Get the last instance or usage of \ to cut out the path of the logo just to have the path leading to the general logo folder
                int lastIndexOf = logoLocation.LastIndexOf(@"\");
                String logoLocationFullPath;

                if (lastIndexOf != -1)
                {
                    logoPNG = logoLocation.Substring(lastIndexOf + 1, logoLocation.LastIndexOf(@".") - lastIndexOf - 1);
                    logoLocation = logoLocation.Substring(0, lastIndexOf);
                    logoLocationFullPath = Path.GetFullPath(Path.Combine(appPath, logoLocation));
                } else
                {
                    logoPNG = logoLocation;
                    logoLocationFullPath = Path.GetFullPath(appPath + "\\");
                }
                

                // Search for all files with 150x150 in its name and use the first result
                DirectoryInfo logoDirectory = new DirectoryInfo(logoLocationFullPath);
                

                String[] keysToTest = { logoPNG, "scale-200","StoreLogo"};


                for (int i=0; i<keysToTest.Length; i++)
                {
                    FileInfo[] filesInDir = getLogoFolder(keysToTest[i], logoDirectory);

                    if (filesInDir.Length != 0)
                    {
                        // Pick the largest matching logo (heaviest file ≈ highest resolution).
                        FileInfo best = filesInDir.OrderByDescending(f => f.Length).First();
                        return getLogo(best.FullName, file);
                    }
                }
                return HighResIcon.GetIcon(file) ?? Icon.ExtractAssociatedIcon(file).ToBitmap();

            } else
            {
                return HighResIcon.GetIcon(file) ?? Icon.ExtractAssociatedIcon(file).ToBitmap();
            }
        }

        private static FileInfo[] getLogoFolder(String keyname, DirectoryInfo logoDirectory)
        {
            // Search for all files with the keyname in its name and use the first result
            FileInfo[] filesInDir = logoDirectory.GetFiles("*" + keyname + "*.*");
            return filesInDir;
        }

        private static Bitmap getLogo(String logoPath, String defaultFile)
        {
            if (File.Exists(logoPath))
            {
                using (MemoryStream ms = new MemoryStream(System.IO.File.ReadAllBytes(logoPath)))
                {
                    var logo = new Bitmap(Bitmap.FromStream(ms));
                    // Keep native resolution; only cap oversized logos to 256 for cache size.
                    if (logo.Width > 256 || logo.Height > 256)
                    {
                        Bitmap capped = ImageFunctions.ResizeImage(logo, 256, 256);
                        logo.Dispose();
                        return capped;
                    }
                    return logo;
                }
            }
            else
            {
                return HighResIcon.GetIcon(defaultFile) ?? Icon.ExtractAssociatedIcon(defaultFile).ToBitmap();
            }
        }

        public static string[] GetLnkTarget(string lnkPath)
        {
            // Late-bound COM (Shell.Application) so no COMReference/tlbimp is needed,
            // which the .NET SDK build (dotnet build) does not support.
            lnkPath = System.IO.Path.GetFullPath(lnkPath);
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            dynamic shl = Activator.CreateInstance(shellType);
            dynamic dir = shl.NameSpace(System.IO.Path.GetDirectoryName(lnkPath));
            dynamic itm = dir.ParseName(System.IO.Path.GetFileName(lnkPath));
            dynamic lnk = itm.GetLink;
            string targetPath = lnk.Path;
            return new String[] { targetPath, "" };
        }


        public static string findWindowsAppsFolder(string subAppName)
        {
            
            if (!fileDirectoryCache.ContainsKey(subAppName))
            {
                try
                {
                    IEnumerable<Windows.ApplicationModel.Package> packages = pkgManger.FindPackagesForUser("", subAppName);

                    // TODO: This should probably use Path.Combine().
                    var test = packages.First();
                    //String finalPath = Environment.ExpandEnvironmentVariables("%ProgramW6432%") + $@"\WindowsApps\" + packages.First().InstalledLocation.DisplayName + @"\";
                    String finalPath = packages.First().InstalledLocation.Path;
                    fileDirectoryCache[subAppName] = finalPath;
                    return finalPath;
                }
                catch (UnauthorizedAccessException) { };
                return "";
            }
            else
            {
                return fileDirectoryCache[subAppName];
            }
        }

        public static string findWindowsAppsName(string AppName)
        {
            String subAppName = AppName.Split('!')[0];
            String appPath = findWindowsAppsFolder(subAppName);

            

                // Load and read manifest to get the logo path
                XmlDocument appManifest = new XmlDocument();
            appManifest.Load(Path.Combine(appPath, "AppxManifest.xml"));

            XmlNamespaceManager appManifestNamespace = new XmlNamespaceManager(new NameTable());
            appManifestNamespace.AddNamespace("sm", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            appManifestNamespace.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");

            try
            {
                return appManifest.SelectSingleNode("/sm:Package/sm:Properties/sm:DisplayName", appManifestNamespace).InnerText;
            } catch (Exception)
            {
                return appManifest.SelectSingleNode("/sm:Package/sm:Applications/sm:Application/uap:VisualElements", appManifestNamespace).Attributes.GetNamedItem("DisplayName").InnerText;
            }
        }
    }
}

