using RichHudFramework.UI;
using VRageMath;

namespace MarcoZechner.CommandApi.Console
{
    public sealed class CommandConsoleWindow : WindowBase
    {
        public static readonly Vector2 DefaultSize =
            new Vector2(520f, 240f);

        public static readonly Vector2 DefaultOffset =
            new Vector2(28f, 248f);

        public CommandConsoleWindow(HudParentBase parent) : base(parent)
        {
            HeaderText = "CommandAPI";

            Size = DefaultSize;
            ParentAlignment = ParentAlignments.InnerBottomLeft;
            Offset = DefaultOffset;

            AllowResizing = false;
            CanDrag = true;
            Visible = false;

            BodyColor = new Color(41, 54, 62, 180);
            BorderColor = new Color(58, 68, 77);

            header.Height = 30f;
            header.Format = new GlyphFormat(
                GlyphFormat.Blueish.Color,
                TextAlignment.Center,
                1.05f
            );

            MouseInput.RequestCursor = true;
        }

        public void ResetLayout()
        {
            Size = DefaultSize;
            ParentAlignment = ParentAlignments.InnerBottomLeft;
            Offset = DefaultOffset;
        }
    }
}
