using System.IO;
using System.Xml.Serialization;
using EmsMod.Config.Schema;
using EmsMod.Utils;

namespace EmsMod.Config
{
    public static class ConfigLoader
    {
        private static GeneralConfig _general;

        public static GeneralConfig General => _general ?? (_general = LoadGeneral());

        public static void ReloadGeneral()
        {
            _general = LoadGeneral();
            Log.Info("General config reloaded.");
        }

        private static GeneralConfig LoadGeneral()
        {
            string path = Path.Combine(ModPaths.ConfigDirectory, "General.xml");
            GeneralConfig loaded = Safe.Run(() => DeserializeGeneral(path), null, "ConfigLoader.LoadGeneral");
            return ConfigValidator.ValidateGeneral(loaded);
        }

        private static GeneralConfig DeserializeGeneral(string path)
        {
            if (!File.Exists(path))
            {
                Log.Warn($"ConfigLoader: General.xml not found at '{path}', falling back to defaults.");
                return null;
            }

            using (FileStream stream = File.OpenRead(path))
            {
                var serializer = new XmlSerializer(typeof(GeneralConfig));
                return (GeneralConfig)serializer.Deserialize(stream);
            }
        }
    }
}
