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
        private static string appDataRelative = System.IO.Path.Combine("Jack Schierbeck", "taskbar-groups");
        public static string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appDataRelative, "Settings.xml");
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
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appDataRelative));
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
    }
}

