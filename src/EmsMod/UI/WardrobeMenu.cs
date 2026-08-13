using System.Drawing;
using Rage;
using Rage.Native;
using EmsMod.Config.Schema;
using EmsMod.Core;
using EmsMod.Input;
using EmsMod.Utils;
using RageGraphics = Rage.Graphics;

namespace EmsMod.UI
{
    /// <summary>
    /// Live clothing browser ("wardrobe"): switch to a freemode ped and scroll
    /// every clothing slot + props (hat, glasses) on your character, applied
    /// instantly so you can see the outfit come together. Works on base-game
    /// clothing now; once EUP is installed, all the EUP uniforms/accessories show
    /// up in these same slots. Saving outfits into the duty loadout comes next.
    /// </summary>
    public static class WardrobeMenu
    {
        private const string FreemodeModel = "mp_m_freemode_01";

        // Slot table: label, isProp, id (component id or prop id).
        private static readonly string[] Labels =
        {
            "Top / Jacket", "Undershirt", "Arms / Torso", "Legs", "Shoes",
            "Vest", "Decal / Badge", "Mask",
            "Hat / Helmet", "Glasses"
        };
        private static readonly bool[] IsProp =
        {
            false, false, false, false, false,
            false, false, false,
            true, true
        };
        private static readonly int[] Ids =
        {
            11, 8, 3, 4, 6,
            9, 10, 1,
            0, 1
        };

        private static readonly int[] Drawable = new int[Labels.Length];
        private static readonly int[] Texture = new int[Labels.Length];

        private static bool _initialized;
        private static bool _visible;
        private static int _row; // 0..Labels.Length-1 = slots; then Save, Exit

        private static int RowCount => Labels.Length + 2; // + Save + Exit
        private static int SaveRow => Labels.Length;
        private static int ExitRow => Labels.Length + 1;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            Game.FrameRender += OnFrameRender;
            _initialized = true;
        }

        public static void Toggle()
        {
            if (_visible)
            {
                Close();
                return;
            }

            Safe.Run(() =>
            {
                DutyMenu.ForceClose(); // only one menu open at a time
                EnsureFreemodePed();
                ReadCurrentFromPed();
                _row = 0;
                _visible = true;
            }, "WardrobeMenu.Open");
        }

        public static void ForceClose()
        {
            _visible = false;
        }

        /// <summary>Closes the wardrobe and, if the player is on duty, restores
        /// their duty uniform + tools (the wardrobe swapped them to a freemode
        /// ped, which cleared both).</summary>
        private static void Close()
        {
            _visible = false;
            if (DutyManager.OnDuty)
            {
                DutyManager.ReapplyLoadout();
            }
        }

        public static void Tick()
        {
            if (!_visible)
            {
                return;
            }

            Safe.Run(HandleNavigation, "WardrobeMenu.HandleNavigation");
        }

        private static void HandleNavigation()
        {
            if (InputManager.IsActionPressed(InputAction.MenuBack))
            {
                Close();
                return;
            }

            if (InputManager.IsActionPressed(InputAction.MenuUp))
            {
                _row = (_row + RowCount - 1) % RowCount;
            }
            else if (InputManager.IsActionPressed(InputAction.MenuDown))
            {
                _row = (_row + 1) % RowCount;
            }

            if (_row == SaveRow)
            {
                if (InputManager.IsActionPressed(InputAction.MenuAccept))
                {
                    SaveCurrentOutfit();
                }
                return;
            }

            if (_row == ExitRow)
            {
                if (InputManager.IsActionPressed(InputAction.MenuAccept))
                {
                    Close();
                }
                return;
            }

            if (InputManager.IsActionPressed(InputAction.MenuLeft))
            {
                ChangeDrawable(-1);
            }
            else if (InputManager.IsActionPressed(InputAction.MenuRight))
            {
                ChangeDrawable(1);
            }
            else if (InputManager.IsActionPressed(InputAction.MenuAccept))
            {
                ChangeTexture(1);
            }
        }

        private static Outfit BuildOutfit(string name)
        {
            var outfit = new Outfit { Name = name };
            for (int i = 0; i < Labels.Length; i++)
            {
                outfit.Pieces.Add(new OutfitPiece
                {
                    IsProp = IsProp[i],
                    Id = Ids[i],
                    Drawable = Drawable[i],
                    Texture = Texture[i]
                });
            }
            return outfit;
        }

        private static void SaveCurrentOutfit()
        {
            string name = $"Outfit {OutfitStore.Outfits.Count + 1}";
            OutfitStore.Add(BuildOutfit(name));
            Safe.Run(() => Game.DisplayNotification($"Saved as ~b~{name}~s~. Pick it in the Duty menu (F7) under Uniform."), "WardrobeMenu.saveNotify");
        }

        /// <summary>Console-command entry: save the player's current look as a
        /// named outfit (reads the current ped, no need to open the wardrobe).</summary>
        public static void SaveCurrentAs(string name)
        {
            Safe.Run(() =>
            {
                ReadCurrentFromPed();
                string finalName = string.IsNullOrWhiteSpace(name) ? $"Outfit {OutfitStore.Outfits.Count + 1}" : name.Trim();
                OutfitStore.Add(BuildOutfit(finalName));
                Game.DisplayNotification($"Saved as ~b~{finalName}~s~. Pick it in the Duty menu (F7) under Uniform.");
            }, "WardrobeMenu.SaveCurrentAs");
        }

        private static Ped Player => Game.LocalPlayer.Character;

        private static void EnsureFreemodePed()
        {
            if (Player.Model == new Model(FreemodeModel))
            {
                return;
            }

            var model = new Model(FreemodeModel);
            model.LoadAndWait();
            NativeFunction.Natives.SET_PLAYER_MODEL(Game.LocalPlayer, model.Hash);
            NativeFunction.Natives.SET_PED_DEFAULT_COMPONENT_VARIATION(Game.LocalPlayer.Character);
            model.Dismiss();
        }

        private static void ReadCurrentFromPed()
        {
            Ped ped = Player;
            for (int i = 0; i < Labels.Length; i++)
            {
                if (IsProp[i])
                {
                    Drawable[i] = Safe.Run(() => (int)NativeFunction.Natives.GET_PED_PROP_INDEX<int>(ped, Ids[i]), 0, "Wardrobe.readProp");
                    Texture[i] = Safe.Run(() => (int)NativeFunction.Natives.GET_PED_PROP_TEXTURE_INDEX<int>(ped, Ids[i]), 0, "Wardrobe.readPropTex");
                }
                else
                {
                    Drawable[i] = Safe.Run(() => (int)NativeFunction.Natives.GET_PED_DRAWABLE_VARIATION<int>(ped, Ids[i]), 0, "Wardrobe.readComp");
                    Texture[i] = Safe.Run(() => (int)NativeFunction.Natives.GET_PED_TEXTURE_VARIATION<int>(ped, Ids[i]), 0, "Wardrobe.readCompTex");
                }
            }
        }

        private static void ChangeDrawable(int dir)
        {
            Ped ped = Player;
            int i = _row;

            int count = IsProp[i]
                ? Safe.Run(() => (int)NativeFunction.Natives.GET_NUMBER_OF_PED_PROP_DRAWABLE_VARIATIONS<int>(ped, Ids[i]), 0, "Wardrobe.propCount")
                : Safe.Run(() => (int)NativeFunction.Natives.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS<int>(ped, Ids[i]), 0, "Wardrobe.compCount");

            if (IsProp[i])
            {
                // Props allow -1 (none) up to count-1.
                int min = -1;
                int max = count - 1;
                Drawable[i] += dir;
                if (Drawable[i] < min) { Drawable[i] = max; }
                if (Drawable[i] > max) { Drawable[i] = min; }
            }
            else
            {
                if (count <= 0) { return; }
                Drawable[i] = (Drawable[i] + dir + count) % count;
            }

            Texture[i] = 0;
            Apply(i);
        }

        private static void ChangeTexture(int dir)
        {
            Ped ped = Player;
            int i = _row;
            if (IsProp[i] && Drawable[i] < 0) { return; }

            int count = IsProp[i]
                ? Safe.Run(() => (int)NativeFunction.Natives.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS<int>(ped, Ids[i], Drawable[i]), 1, "Wardrobe.propTexCount")
                : Safe.Run(() => (int)NativeFunction.Natives.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS<int>(ped, Ids[i], Drawable[i]), 1, "Wardrobe.compTexCount");

            if (count <= 0) { count = 1; }
            Texture[i] = (Texture[i] + dir + count) % count;
            Apply(i);
        }

        private static void Apply(int i)
        {
            Ped ped = Player;
            Safe.Run(() =>
            {
                if (IsProp[i])
                {
                    if (Drawable[i] < 0)
                    {
                        NativeFunction.Natives.CLEAR_PED_PROP(ped, Ids[i]);
                    }
                    else
                    {
                        NativeFunction.Natives.SET_PED_PROP_INDEX(ped, Ids[i], Drawable[i], Texture[i], true);
                    }
                }
                else
                {
                    NativeFunction.Natives.SET_PED_COMPONENT_VARIATION(ped, Ids[i], Drawable[i], Texture[i], 0);
                }
            }, "WardrobeMenu.Apply");
        }

        private static void OnFrameRender(object sender, GraphicsEventArgs e)
        {
            if (!_visible)
            {
                return;
            }

            Safe.Run(() => Draw(e.Graphics), "WardrobeMenu.OnFrameRender");
        }

        private static void Draw(RageGraphics g)
        {
            const float width = 520f;
            const float rowHeight = 34f;
            const float headerHeight = 52f;
            float height = headerHeight + RowCount * rowHeight + 30f;

            Size res = Game.Resolution;
            float x = (res.Width - width) / 2f;
            float y = (res.Height - height) / 2f;

            g.DrawRectangle(new RectangleF(x, y, width, height), Color.FromArgb(220, 0, 0, 0));
            g.DrawRectangle(new RectangleF(x, y, width, headerHeight), Color.FromArgb(230, 20, 90, 150));
            g.DrawText("Wardrobe", "Arial", 22f, new PointF(x + 16f, y + 12f), Color.White);

            for (int i = 0; i < Labels.Length; i++)
            {
                string val = (IsProp[i] && Drawable[i] < 0) ? "none" : $"{Drawable[i]} (tex {Texture[i]})";
                DrawRow(g, x, y + headerHeight + i * rowHeight, rowHeight, width, i, $"{Labels[i]}:  < {val} >");
            }

            DrawRow(g, x, y + headerHeight + SaveRow * rowHeight, rowHeight, width, SaveRow, "Save Outfit  (adds it to the Duty menu)");
            DrawRow(g, x, y + headerHeight + ExitRow * rowHeight, rowHeight, width, ExitRow, "Exit Wardrobe");

            g.DrawText("Up/Down slot   Left/Right change   Enter/A texture or select   Backspace/B close",
                "Arial", 12f, new PointF(x + 16f, y + height - 20f), Color.LightGray);
        }

        private static void DrawRow(RageGraphics g, float x, float y, float rowHeight, float width, int row, string text)
        {
            bool selected = row == _row;
            if (selected)
            {
                g.DrawRectangle(new RectangleF(x + 6f, y + 3f, width - 12f, rowHeight - 6f), Color.FromArgb(180, 60, 60, 60));
            }

            g.DrawText(text, "Arial", 16f, new PointF(x + 18f, y + 7f), selected ? Color.Yellow : Color.White);
        }
    }
}
