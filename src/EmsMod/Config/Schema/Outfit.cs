using System.Collections.Generic;

namespace EmsMod.Config.Schema
{
    /// <summary>
    /// A saved wardrobe outfit: a name plus the component/prop pieces that make
    /// it up, applied on top of a freemode ped. Built in the wardrobe and saved
    /// so it shows as a pickable uniform in the Duty menu (this is how EUP
    /// outfits become on-duty uniforms).
    /// </summary>
    public class Outfit
    {
        public string Name { get; set; } = "";
        public List<OutfitPiece> Pieces { get; set; } = new List<OutfitPiece>();
    }

    public class OutfitPiece
    {
        public bool IsProp { get; set; }
        public int Id { get; set; }
        public int Drawable { get; set; }
        public int Texture { get; set; }
    }

    /// <summary>Serialization wrapper so the saved-outfits file has a root element.</summary>
    public class OutfitList
    {
        public List<Outfit> Outfits { get; set; } = new List<Outfit>();
    }
}
