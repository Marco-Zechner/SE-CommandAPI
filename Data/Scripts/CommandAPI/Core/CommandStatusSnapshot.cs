using System;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandStatusSnapshot
    {
        public string CommandApiVersion { get; }

        public string ProtocolVersion { get; }

        public string NetworkState { get; }

        public bool RichHudChatAvailable { get; }

        public string PresentationAdapter { get; }

        public int ExternalProviderCount { get; }

        public CommandStatusSnapshot(
            string commandApiVersion,
            string protocolVersion,
            string networkState,
            bool richHudChatAvailable,
            string presentationAdapter,
            int externalProviderCount
        )
        {
            if (commandApiVersion == null)
                throw new ArgumentNullException(
                    nameof(commandApiVersion)
                );

            if (protocolVersion == null)
                throw new ArgumentNullException(
                    nameof(protocolVersion)
                );

            if (networkState == null)
                throw new ArgumentNullException(
                    nameof(networkState)
                );

            if (presentationAdapter == null)
                throw new ArgumentNullException(
                    nameof(presentationAdapter)
                );

            CommandApiVersion = commandApiVersion;
            ProtocolVersion = protocolVersion;
            NetworkState = networkState;
            RichHudChatAvailable = richHudChatAvailable;
            PresentationAdapter = presentationAdapter;
            ExternalProviderCount = externalProviderCount;
        }
    }
}
