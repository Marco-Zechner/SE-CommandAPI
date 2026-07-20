using MarcoZechner.CommandApi.Smoke;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace MarcoZechner.CommandApi.Smoke.Alpha
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class CommandApiSmokeAlphaSession :
        MySessionComponentBase
    {
        private CommandApiSmokeConsumer _consumer;

        public override void BeforeStart()
        {
            if (
                MyAPIGateway.Multiplayer == null
                || !MyAPIGateway.Multiplayer.IsServer
            )
            {
                return;
            }

            _consumer =
                new CommandApiSmokeConsumer(
                    "MarcoZechner.CommandApiSmoke.Alpha",
                    "CommandAPI Smoke Alpha",
                    "smoke.alpha",
                    "Alpha"
                );

            _consumer.Start();
        }

        protected override void UnloadData()
        {
            if (_consumer == null)
                return;

            _consumer.Dispose();
            _consumer = null;
        }
    }
}
