using MarcoZechner.CommandApi.Console;
using RichHudFramework.Client;
using RichHudFramework.UI.Client;
using VRage.Game;
using VRage.Game.Components;

namespace MarcoZechner.CommandApi
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class CommandApiSession : MySessionComponentBase
    {
        private const string ModDisplayName = "CommandAPI";

        private CommandConsoleWindow _window;
        private bool _richHudInitialized;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            RichHudClient.Init(ModDisplayName, OnRichHudInitialized, OnRichHudReset);
        }

        protected override void UnloadData()
        {
            OnRichHudReset();
            base.UnloadData();
        }

        private void OnRichHudInitialized()
        {
            if (_richHudInitialized)
                return;

            _richHudInitialized = true;

            _window = new CommandConsoleWindow(HudMain.HighDpiRoot)
            {
                Visible = true
            };
        }

        private void OnRichHudReset()
        {
            if (!_richHudInitialized && _window == null)
                return;

            if (_window != null)
                _window.Visible = false;

            _window = null;
            _richHudInitialized = false;
        }
    }
}
