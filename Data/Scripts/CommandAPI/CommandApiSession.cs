using RichHudFramework.Client;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;

namespace MarcoZechner.CommandApi
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class CommandApiSession : MySessionComponentBase
    {
        private const string ModDisplayName = "CommandAPI";

        private bool _richHudInitialized;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            RichHudClient.Init(
                ModDisplayName,
                OnRichHudInitialized,
                OnRichHudReset
            );
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

            MyAPIGateway.Utilities.ShowNotification(
                "CommandAPI connected to Rich HUD Master.",
                3000,
                "White"
            );
        }

        private void OnRichHudReset()
        {
            _richHudInitialized = false;
        }
    }
}