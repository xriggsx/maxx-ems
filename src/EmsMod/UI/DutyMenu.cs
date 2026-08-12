using System.Collections.Generic;
using System.Drawing;
using Rage;
using EmsMod.Config.Schema;
using EmsMod.Core;
using EmsMod.Input;
using EmsMod.Utils;
using RageGraphics = Rage.Graphics;

namespace EmsMod.UI
{
    /// <summary>
    /// The "go on duty" hospital menu: pick Mode, Character/uniform, and Vehicle,
    /// then Go On Duty (which teleports to the nearest hospital, applies the
    /// uniform, and spawns the vehicle via DutyManager). Fully keyboard and
    /// controller driven, non-blocking (doesn't pause the game). Opens/closes
    /// with the OpenDutyMenu action (F7 / controller View button).
    /// </summary>
    public static class DutyMenu
    {
        private const int RowMode = 0;
        private const int RowCharacter = 1;
        private const int RowVehicle = 2;
        private const int RowEquipment = 3;
        private const int RowGoOnDuty = 4;
        private const int RowOffDuty = 5;
        private const int RowCount = 6;

        private static bool _initialized;
        private static bool _visible;
        private static int _row;
        private static int _mode;
        private static int _char;
        private static int _veh;
        private static int _equip;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            Game.FrameRender += OnFrameRender;
            _initialized = true;
        }

        public static void ForceClose()
        {
            _visible = false;
        }

        public static void Tick()
        {
            if (InputManager.IsActionPressed(InputAction.OpenDutyMenu))
            {
                _visible = !_visible;
                if (_visible)
                {
                    WardrobeMenu.ForceClose(); // only one menu open at a time
                    _row = 0;
                    ClampSelections();
                }
                return;
            }

            if (!_visible)
            {
                return;
            }

            Safe.Run(HandleNavigation, "DutyMenu.HandleNavigation");
        }

        private static void HandleNavigation()
        {
            if (InputManager.IsActionPressed(InputAction.MenuBack))
            {
                _visible = false;
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

            if (InputManager.IsActionPressed(InputAction.MenuLeft))
            {
                ChangeValue(-1);
            }
            else if (InputManager.IsActionPressed(InputAction.MenuRight))
            {
                ChangeValue(1);
            }

            if (InputManager.IsActionPressed(InputAction.MenuAccept))
            {
                Activate();
            }
        }

        private static void ChangeValue(int dir)
        {
            List<DutyMode> modes = DutyManager.Config.Modes;
            if (modes.Count == 0)
            {
                return;
            }

            switch (_row)
            {
                case RowMode:
                    _mode = (_mode + dir + modes.Count) % modes.Count;
                    _char = 0;
                    _veh = 0;
                    _equip = 0;
                    break;

                case RowCharacter:
                    int charCount = modes[_mode].Characters.Count;
                    if (charCount > 0)
                    {
                        _char = (_char + dir + charCount) % charCount;
                    }
                    break;

                case RowVehicle:
                    int vehCount = modes[_mode].Vehicles.Count;
                    if (vehCount > 0)
                    {
                        _veh = (_veh + dir + vehCount) % vehCount;
                    }
                    break;

                case RowEquipment:
                    int equipCount = modes[_mode].Equipment.Count;
                    if (equipCount > 0)
                    {
                        _equip = (_equip + dir + equipCount) % equipCount;
                    }
                    break;
            }
        }

        private static void Activate()
        {
            switch (_row)
            {
                case RowEquipment:
                    // Grab the highlighted tool right now (must be on duty).
                    DutyManager.EquipItem(_equip);
                    break;

                case RowGoOnDuty:
                    DutyManager.GoOnDuty(_mode, _char, _veh);
                    _visible = false;
                    break;

                case RowOffDuty:
                    DutyManager.OffDuty();
                    _visible = false;
                    break;
            }
        }

        private static void ClampSelections()
        {
            List<DutyMode> modes = DutyManager.Config.Modes;
            if (modes.Count == 0)
            {
                _mode = _char = _veh = 0;
                return;
            }

            if (_mode >= modes.Count) { _mode = 0; }
            if (_char >= modes[_mode].Characters.Count) { _char = 0; }
            if (_veh >= modes[_mode].Vehicles.Count) { _veh = 0; }
            if (_equip >= modes[_mode].Equipment.Count) { _equip = 0; }
        }

        private static void OnFrameRender(object sender, GraphicsEventArgs e)
        {
            if (!_visible)
            {
                return;
            }

            Safe.Run(() => Draw(e.Graphics), "DutyMenu.OnFrameRender");
        }

        private static void Draw(RageGraphics g)
        {
            List<DutyMode> modes = DutyManager.Config.Modes;

            const float width = 480f;
            const float rowHeight = 42f;
            const float headerHeight = 54f;
            float height = headerHeight + RowCount * rowHeight + 12f;

            Size res = Game.Resolution;
            float x = (res.Width - width) / 2f;
            float y = (res.Height - height) / 2f;

            g.DrawRectangle(new RectangleF(x, y, width, height), Color.FromArgb(220, 0, 0, 0));
            g.DrawRectangle(new RectangleF(x, y, width, headerHeight), Color.FromArgb(230, 150, 20, 20));
            g.DrawText("EMS - Go On Duty", "Arial", 22f, new PointF(x + 16f, y + 12f), Color.White);

            string modeName = modes.Count > 0 ? modes[_mode].Name : "(none)";
            string charName = (modes.Count > 0 && modes[_mode].Characters.Count > 0) ? modes[_mode].Characters[_char] : "(none)";
            string vehName = (modes.Count > 0 && modes[_mode].Vehicles.Count > 0) ? modes[_mode].Vehicles[_veh] : "(none)";
            string equipName = (modes.Count > 0 && modes[_mode].Equipment.Count > 0) ? modes[_mode].Equipment[_equip] : "(none)";

            DrawRow(g, x, y + headerHeight, rowHeight, RowMode, $"Mode:  < {modeName} >");
            DrawRow(g, x, y + headerHeight + rowHeight, rowHeight, RowCharacter, $"Uniform:  < {charName} >");
            DrawRow(g, x, y + headerHeight + rowHeight * 2, rowHeight, RowVehicle, $"Vehicle:  < {vehName} >");
            DrawRow(g, x, y + headerHeight + rowHeight * 3, rowHeight, RowEquipment, $"Equipment:  < {equipName} >   (Enter=grab)");
            DrawRow(g, x, y + headerHeight + rowHeight * 4, rowHeight, RowGoOnDuty, "Go On Duty");
            DrawRow(g, x, y + headerHeight + rowHeight * 5, rowHeight, RowOffDuty, "Off Duty");

            g.DrawText("Up/Down move   Left/Right change   Enter/A select   Backspace/B close",
                "Arial", 13f, new PointF(x + 16f, y + height - 22f), Color.LightGray);
        }

        private static void DrawRow(RageGraphics g, float x, float y, float rowHeight, int row, string text)
        {
            bool selected = row == _row;
            if (selected)
            {
                g.DrawRectangle(new RectangleF(x + 6f, y + 4f, 468f, rowHeight - 8f), Color.FromArgb(180, 60, 60, 60));
            }

            Color color = selected ? Color.Yellow : Color.White;
            g.DrawText(text, "Arial", 18f, new PointF(x + 18f, y + 8f), color);
        }
    }
}
