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


        public ProgramShortcut() // needed for XML serialization
        {

        }

    }
}

