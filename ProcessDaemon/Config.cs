using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessDaemon
{
    public class Profile
    {

        public int ProcessId {  get; set; }
        public string Name { get; set; }

        public string FileName { get; set; }

        public string Arguments { get; set; }

        public string WorkingDirectory { get; set; }

        public List<KeyValuePair<string, string>> Environment { get; set; }

        public bool AutoStart { get; set; }

        public int AfterStopped { get; set; }

        public int DelayForSeconds { get; set; }

        public bool CreateNoWindow { get; set; }

        public bool UseShellExecute { get; set; }
    }

    public class Config
    {
        public List<Profile> Profiles { get; set; }
    }
}
