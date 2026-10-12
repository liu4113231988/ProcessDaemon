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

        public int Type {  get; set; }

        public bool Enabled {  get; set; }

        // Memory monitoring (Type=2 only)
        public int MaxMemoryMB { get; set; }              // Maximum allowed memory in MB (0 = disabled)

        public int MemoryCheckIntervalSeconds { get; set; } // Interval between memory checks

        public int MemoryThresholdCount { get; set; }       // Number of breaches within time window before restart

        public int MemoryTimeWindowSeconds { get; set; }    // Time window for counting breaches
    }

    public class Config
    {
        public List<Profile> Profiles { get; set; }
    }
}
