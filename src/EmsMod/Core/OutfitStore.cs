using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Rage;
using Rage.Native;
using EmsMod.Config.Schema;
using EmsMod.Utils;

namespace EmsMod.Core
{
    /// <summary>
    /// Loads/saves player-created wardrobe outfits (SavedOutfits.xml) and applies
    /// them to a ped. Outfits persist across sessions so a look built once is
    /// always available as a Duty-menu uniform.
    /// </summary>
    public static class OutfitStore
    {
        private static List<Outfit> _outfits;

        public static List<Outfit> Outfits => _outfits ?? (_outfits = Load());

        public static void Add(Outfit outfit)
        {
            Outfits.Add(outfit);
            Save();
        }

        public static void Reload()
        {
            _outfits = Load();
        }

        private static string FilePath => Path.Combine(ModPaths.ConfigDirectory, "SavedOutfits.xml");

        private static List<Outfit> Load()
        {
            return Safe.Run(() =>
            {
                if (!File.Exists(FilePath))
                {
                    return new List<Outfit>();
                }
                using (FileStream stream = File.OpenRead(FilePath))
                {
                    var serializer = new XmlSerializer(typeof(OutfitList));
                    var list = (OutfitList)serializer.Deserialize(stream);
                    return list?.Outfits ?? new List<Outfit>();
                }
            }, new List<Outfit>(), "OutfitStore.Load");
        }

        private static void Save()
        {
            Safe.Run(() =>
            {
                Directory.CreateDirectory(ModPaths.ConfigDirectory);
                using (FileStream stream = File.Create(FilePath))
                {
                    var serializer = new XmlSerializer(typeof(OutfitList));
                    serializer.Serialize(stream, new OutfitList { Outfits = _outfits });
                }
                Log.Info($"OutfitStore: saved {_outfits.Count} outfit(s).");
            }, "OutfitStore.Save");
        }

        /// <summary>Applies an outfit's pieces to a ped (components + props).</summary>
        public static void Apply(Ped ped, Outfit outfit)
        {
            if (ped == null || !ped.Exists() || outfit == null)
            {
                return;
            }

            Safe.Run(() =>
            {
                foreach (OutfitPiece piece in outfit.Pieces)
                {
                    if (piece.IsProp)
                    {
                        if (piece.Drawable < 0)
                        {
                            NativeFunction.Natives.CLEAR_PED_PROP(ped, piece.Id);
                        }
                        else
                        {
                            NativeFunction.Natives.SET_PED_PROP_INDEX(ped, piece.Id, piece.Drawable, piece.Texture, true);
                        }
                    }
                    else
                    {
                        NativeFunction.Natives.SET_PED_COMPONENT_VARIATION(ped, piece.Id, piece.Drawable, piece.Texture, 0);
                    }
                }
            }, "OutfitStore.Apply");
        }
    }
}
