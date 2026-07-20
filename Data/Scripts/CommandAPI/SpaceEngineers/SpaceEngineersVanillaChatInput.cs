using System;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace MarcoZechner.CommandApi.Chat
{
    public sealed class SpaceEngineersVanillaChatInput :
        IVanillaChatInput,
        IDisposable
    {
        private readonly IMyUtilities _utilities;

        private bool _disposed;

        public event VanillaChatMessageEnteredHandler
            MessageEntered;

        public SpaceEngineersVanillaChatInput()
        {
            _utilities =
                MyAPIGateway.Utilities;

            if (_utilities == null)
            {
                throw new InvalidOperationException(
                    "Space Engineers utilities are unavailable."
                );
            }

            _utilities.MessageEnteredSender +=
                OnMessageEntered;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _utilities.MessageEnteredSender -=
                OnMessageEntered;

            MessageEntered = null;
        }

        private void OnMessageEntered(
            ulong senderId,
            string message,
            ref bool sendToOthers
        )
        {
            if (_disposed)
                return;

            VanillaChatMessageEnteredHandler handler =
                MessageEntered;

            if (handler == null)
                return;

            handler(
                senderId,
                message,
                ref sendToOthers
            );
        }
    }
}
