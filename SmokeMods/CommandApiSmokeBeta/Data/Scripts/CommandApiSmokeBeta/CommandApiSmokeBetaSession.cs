using MarcoZechner.CommandApi.Smoke;
using VRage.Game.Components;

namespace MarcoZechner.CommandApi.Smoke.Beta
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class CommandApiSmokeBetaSession :
        MySessionComponentBase
    {
        private CommandApiSmokeConsumer _consumer;

        public override void BeforeStart()
        {
            _consumer =
                new CommandApiSmokeConsumer(
                    "MarcoZechner.CommandApiSmoke.Beta",
                    "CommandAPI Smoke Beta",
                    "smoke.beta",
                    "Beta"
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
