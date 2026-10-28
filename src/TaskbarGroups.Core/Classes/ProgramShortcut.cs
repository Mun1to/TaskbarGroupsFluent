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


        public ProgramShortcut() // needed for XML serialization
        {

        }

    }
}

