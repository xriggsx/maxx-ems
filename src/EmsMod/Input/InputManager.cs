using System.Collections.Generic;
using System.Windows.Forms;
using Rage;
using EmsMod.Utils;

namespace EmsMod.Input
{
    /// <summary>
    /// The single sanctioned path for reading input. No file outside this
    /// folder should call raw keyboard/controller APIs directly — going
    /// through IsActionPressed is what guarantees every interaction supports
    /// both keyboard and controller, and is single-tap (edge-triggered, no
    /// "held" primitive is exposed).
    /// </summary>
    public static class InputManager
    {
        private static readonly Dictionary<InputAction, InputBinding> _bindings = new Dictionary<InputAction, InputBinding>
        {
            { InputAction.AcceptCallout, new InputBinding(Keys.Y, ControllerButtons.A) },
            { InputAction.DeclineCallout, new InputBinding(Keys.N, ControllerButtons.B) },
            { InputAction.Interact, new InputBinding(Keys.E, ControllerButtons.X) },
            { InputAction.ConfirmMenuOption, new InputBinding(Keys.E, ControllerButtons.X) },

            { InputAction.OpenDutyMenu, new InputBinding(Keys.F7, ControllerButtons.Back) },
            { InputAction.MenuUp, new InputBinding(Keys.Up, ControllerButtons.DPadUp) },
            { InputAction.MenuDown, new InputBinding(Keys.Down, ControllerButtons.DPadDown) },
            { InputAction.MenuLeft, new InputBinding(Keys.Left, ControllerButtons.DPadLeft) },
            { InputAction.MenuRight, new InputBinding(Keys.Right, ControllerButtons.DPadRight) },
            { InputAction.MenuAccept, new InputBinding(Keys.Enter, ControllerButtons.A) },
            { InputAction.MenuBack, new InputBinding(Keys.Back, ControllerButtons.B) },
        };

        private static readonly Dictionary<InputAction, bool> _downThisFrame = new Dictionary<InputAction, bool>();
        private static readonly Dictionary<InputAction, bool> _downLastFrame = new Dictionary<InputAction, bool>();

        /// <summary>Snapshots every binding's raw down/up state for this frame.
        /// Must be called exactly once per frame, before any IsActionPressed
        /// calls - this is what makes IsActionPressed a true single-press edge
        /// check (fires once on the frame a key/button goes down) regardless of
        /// how many places query the same action in that frame, instead of
        /// firing every frame the key/button is held.</summary>
        public static void Update()
        {
            foreach (KeyValuePair<InputAction, InputBinding> pair in _bindings)
            {
                InputAction action = pair.Key;
                InputBinding binding = pair.Value;

                bool keyDown = Safe.Run(() => Game.IsKeyDown(binding.Key), false, $"InputManager.Update({action}) key");
                bool buttonDown = Safe.Run(() => Game.IsControllerButtonDown(binding.ControllerButton), false, $"InputManager.Update({action}) controller");

                _downLastFrame[action] = _downThisFrame.TryGetValue(action, out bool prev) && prev;
                _downThisFrame[action] = keyDown || buttonDown;
            }
        }

        /// <summary>True only on the single frame an action's key/button
        /// transitions from up to down - holding it down does not repeat-fire.
        /// Safe to call from multiple places in the same frame for the same
        /// action; each returns the same answer since the edge is computed once
        /// in Update(), not per-call.</summary>
        public static bool IsActionPressed(InputAction action)
        {
            if (!_bindings.ContainsKey(action))
            {
                Log.Warn($"InputManager: no binding registered for action '{action}'.");
                return false;
            }

            bool down = _downThisFrame.TryGetValue(action, out bool d) && d;
            bool wasDown = _downLastFrame.TryGetValue(action, out bool w) && w;
            return down && !wasDown;
        }
    }
}
