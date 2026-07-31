using System.IO;
using System.Reflection;

namespace EmsMod.Utils
{
    public static class ModPaths
    {
        public static string PluginDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static string DataDirectory => Path.Combine(PluginDirectory, "EmsMod");

        public static string ConfigDirectory => Path.Combine(DataDirectory, "Config");

        public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");
    }
}
