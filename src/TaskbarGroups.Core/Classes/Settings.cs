using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TaskbarGroups.Core
{
    public static class Settings
    {
        public static string settingsPath = Path.Combine(AppPackage.DataRoot, "Settings.xml");
        public static string defaultSettingsPath;
        public static Setting settingInfo;

        static Settings()
        {
            defaultSettingsPath = settingsPath;
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xml")))
            {
                settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xml");
            }
            else if (!File.Exists(settingsPath))
            {
                Directory.CreateDirectory(AppPackage.DataRoot);
                settingInfo = new Setting();
                writeXML();
                return;
            }

            System.Xml.Serialization.XmlSerializer reader =
            new System.Xml.Serialization.XmlSerializer(typeof(Setting));
            using (StreamReader file = new StreamReader(settingsPath))
            {
                settingInfo = (Setting)reader.Deserialize(file);
                file.Close();
            }

            if (settingInfo.portableMode == true && !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xml")))
            {
                settingInfo.portableMode = false;
                writeXML();
            }
            
        }

        /// <summary>
        /// Re-reads the settings file into <see cref="settingInfo"/>. The static
        /// constructor only runs once per process, so a long-lived process such as
        /// the hover watcher would otherwise never notice a change the app made.
        /// Failures are ignored on purpose: the file can be caught half-written, and
        /// keeping the previous values is better than throwing.
        /// </summary>
        public static void Reload()
        {
            try
            {
                if (!File.Exists(settingsPath)) return;

                var reader = new XmlSerializer(typeof(Setting));
                using (StreamReader file = new StreamReader(settingsPath))
                {
                    if (reader.Deserialize(file) is Setting fresh) settingInfo = fresh;
                }
            }
            catch { /* a partial read just means we keep what we had */ }
        }

        public static void writeXML()
        {
            try
            {
                System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(typeof(Setting));

                using (FileStream file = System.IO.File.Create(settingsPath))
                {
                    writer.Serialize(file, settingInfo);
                    file.Close();
                }
            }
            catch (IOException e)
            {
                throw new IOException(
                    "Settings.xml may be open in another program.\r\n\r\nError: " + e.Message, e);
            }
        }
    }

    /// <summary>The ways the panel can come and go.</summary>
    public static class PanelAnimation
    {
        /// <summary>It appears and disappears at once.</summary>
        public const string None = "none";

        /// <summary>It rises a short way into place while the apps fade in.</summary>
        public const string Peek = "peek";

        /// <summary>It slides out from behind the taskbar, as the Start menu does.</summary>
        public const string Rise = "rise";

        public static readonly string[] All = { None, Peek, Rise };
    }

    [Serializable()]
    public class Setting
    {
        [XmlElement]
        public bool portableMode { get; set; } = false;

        /// <summary>
        /// Open a group by resting the cursor on its taskbar icon, no click needed.
        /// Off by default: it needs a resident watcher process, and nobody should
        /// end up with a background process they never asked for.
        /// </summary>
        [XmlElement]
        public bool hoverToOpen { get; set; } = false;

        /// <summary>
        /// How long the cursor must rest on the icon before the group opens.
        /// Without a delay, merely crossing the taskbar on the way to the clock
        /// would fire groups the user never meant to open.
        /// </summary>
        [XmlElement]
        public int hoverDelayMs { get; set; } = 400;

        /// <summary>
        /// How the panel comes and goes, one of <see cref="PanelAnimation"/>. None by
        /// default: it is a matter of taste, and the panel has always appeared at once.
        /// </summary>
        [XmlElement]
        public string panelAnimation { get; set; } = PanelAnimation.None;

        /// <summary>"system" to follow Windows' light or dark mode, or "light" / "dark".</summary>
        [XmlElement]
        public string theme { get; set; } = UiPreferences.System;

        /// <summary>"system" to follow Windows' display language, or "es" / "en".</summary>
        [XmlElement]
        public string language { get; set; } = UiPreferences.System;
    }
}

