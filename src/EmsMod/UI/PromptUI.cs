using System.Drawing;
using Rage;
using EmsMod.Utils;
using RageGraphics = Rage.Graphics;

namespace EmsMod.UI
{
    /// <summary>
    /// The non-blocking accept/decline popup. Purely a rendering + visibility
    /// widget — timeout/input-result logic belongs to whatever owns a given
    /// popup's lifecycle (CalloutBase, later), not to this class.
    /// </summary>
    public static class PromptUI
    {
        private static bool _initialized;
        private static bool _isVisible;
        private static string _message;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            Game.FrameRender += OnFrameRender;
            _initialized = true;
        }

        public static void Show(string message)
        {
            _message = message;
            _isVisible = true;
        }

        public static void Hide()
        {
            _isVisible = false;
        }

        private static void OnFrameRender(object sender, GraphicsEventArgs e)
        {
            if (!_isVisible)
            {
                return;
            }

            Safe.Run(() => Draw(e.Graphics), "PromptUI.OnFrameRender");
        }

        private static void Draw(RageGraphics graphics)
        {
            Size resolution = Game.Resolution;
            const float boxWidth = 520f;
            const float boxHeight = 90f;
            float x = (resolution.Width - boxWidth) / 2f;
            float y = resolution.Height - 220f;

            var box = new RectangleF(x, y, boxWidth, boxHeight);
            graphics.DrawRectangle(box, Color.FromArgb(200, 0, 0, 0));

            graphics.DrawText(_message, "Arial", 20f, new PointF(x + 20f, y + 15f), Color.White);
            graphics.DrawText("Press Y / A to Accept   -   N / B to Decline", "Arial", 16f, new PointF(x + 20f, y + 52f), Color.LightGray);
        }
    }
}
