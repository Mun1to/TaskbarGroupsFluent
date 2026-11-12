namespace TaskbarGroups.Core
{
    public class ProgramShortcut
    {
        public string FilePath { get; set; }
        public bool isWindowsApp { get; set; }

        public string name { get; set; } = "";
        public string Arguments = "";
        public string WorkingDirectory = Paths.exeString;

        /// <summary>Start the program elevated. Windows asks for consent every time.</summary>
        public bool RunAsAdmin { get; set; }

        /// <summary>
        /// An icon the user picked for this entry instead of the one its app has
        /// (issue #15): the picture itself, as a PNG in base64 made by
        /// <see cref="PickedIcon.Encode"/>, so it travels with the group when it is
        /// exported, copied or renamed and survives the file it came from being
        /// deleted. Null for the automatic icon, and then the element is left out of
        /// ObjectData.xml altogether, so a file saved before it existed reads the
        /// same, and a version from before it ignores it and shows the automatic one.
        /// </summary>
        public string CustomIcon { get; set; }

        // Read-only, so the XML serializer leaves it out of ObjectData.xml.
        public bool HasCustomIcon => !string.IsNullOrEmpty(CustomIcon);

        /// <summary>
        /// Marks an entry that is another group rather than a program: clicking it
        /// opens that group's own panel. Stored by name rather than by the path of
        /// the flyout, so it survives an export to another PC.
        /// </summary>
        public const string GroupPrefix = "group:";

        public static ProgramShortcut ForGroup(string groupName)
            => new ProgramShortcut { FilePath = GroupPrefix + groupName, name = groupName };

        // Read-only, so the XML serializer leaves them out of ObjectData.xml.
        public bool IsGroup
            => FilePath != null && FilePath.StartsWith(GroupPrefix, System.StringComparison.OrdinalIgnoreCase);

        public string GroupName => IsGroup ? FilePath.Substring(GroupPrefix.Length) : null;

        /// <summary>
        /// An entry for a file or folder on disk, made the same way whether it was
        /// picked with Browse… or dropped on the editor or on a panel: a file is
        /// named after itself without its extension, a folder by Windows' own name.
        /// </summary>
        public static ProgramShortcut ForPath(string path)
            => System.IO.Directory.Exists(path)
                ? new ProgramShortcut { FilePath = path, isWindowsApp = false }
                : new ProgramShortcut
                {
                    FilePath = path,
                    isWindowsApp = false,
                    name = System.IO.Path.GetFileNameWithoutExtension(path)
                };

        /// <summary>
        /// Marks an entry that is a line across the panel rather than something to
        /// open: it splits the apps into sections, with its name as an optional
        /// title over the line. A version from before it existed reads it as a
        /// program that is not there, which shows a blank icon and opens nothing.
        /// </summary>
        public const string SeparatorPath = "separator:";

        public static ProgramShortcut ForSeparator(string title = "")
            => new ProgramShortcut { FilePath = SeparatorPath, name = title ?? "" };

        public bool IsSeparator
            => FilePath != null && FilePath.Equals(SeparatorPath, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Set on an entry read live from the folder a group shows (issue #9): the
        /// shell's name for it, which reaches what a path cannot, such as a Control
        /// Panel page. Such entries are made each time the panel opens and are never
        /// written to ObjectData.xml.
        /// </summary>
        [System.Xml.Serialization.XmlIgnore]
        public string ShellItem { get; set; }

        /// <summary>The shell entry is a folder, which may open beside the panel as a submenu.</summary>
        [System.Xml.Serialization.XmlIgnore]
        public bool IsShellFolder { get; set; }

        public bool IsShellItem => !string.IsNullOrEmpty(ShellItem);

        /// <summary>An entry for something in a folder a group shows.</summary>
        public static ProgramShortcut ForShellEntry(ShellEntry entry)
            => new ProgramShortcut
            {
                FilePath = entry.Id,
                name = entry.Name,
                ShellItem = entry.Id,
                IsShellFolder = entry.IsFolder
            };


        public ProgramShortcut() // needed for XML serialization
        {

        }

    }
}

