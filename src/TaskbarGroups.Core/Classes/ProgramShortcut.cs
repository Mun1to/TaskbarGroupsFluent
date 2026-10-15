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


        public ProgramShortcut() // needed for XML serialization
        {

        }

    }
}

