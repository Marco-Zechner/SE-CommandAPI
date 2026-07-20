using System;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace MarcoZechner.CommandApi.Chat
{
    public sealed class SpaceEngineersVanillaChatOutput :
        IVanillaChatOutput
    {
        public void WriteLine(
            string author,
            string message
        )
        {
            if (author == null)
                throw new ArgumentNullException(nameof(author));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            IMyUtilities utilities =
                MyAPIGateway.Utilities;

            if (utilities == null)
            {
                throw new InvalidOperationException(
                    "Space Engineers utilities are unavailable."
                );
            }

            utilities.ShowMessage(
                author,
                message
            );
        }
    }
}
