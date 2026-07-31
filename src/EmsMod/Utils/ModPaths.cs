using System.Diagnostics;
using System.IO;

namespace EmsMod.Utils
{
    public static class ModPaths
    {
        /// <summary>
        /// Deliberately NOT based on Assembly.GetExecutingAssembly().Location -
        /// RPH loads each plugin into its own AppDomain from a temp shadow-copy
        /// location, so that would resolve to a temp folder, not the real
        /// Plugins folder. Deriving from the game process's own exe path is
        /// stable regardless of how RPH loads the plugin assembly itself.
        /// </summary>
        public static string PluginDirectory => Path.Combine(
            Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
            "Plugins");

        public static string DataDirectory => Path.Combine(PluginDirectory, "EmsMod");

        public static string ConfigDirectory => Path.Combine(DataDirectory, "Config");

        public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");
    }
}
