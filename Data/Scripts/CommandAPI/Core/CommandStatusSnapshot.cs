using System;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandStatusSnapshot
    {
        public string ModVersion { get; }

        public string ApiVersion { get; }

        public string ProtocolVersion { get; }

        public string NetworkState { get; }

        public string PresentationAdapter { get; }

        public int ExternalProviderCount { get; }

        public CommandStatusSnapshot(string modVersion, string apiVersion, string protocolVersion, string networkState, string presentationAdapter, int externalProviderCount)
        {
            if (modVersion == null)
                throw new ArgumentNullException(nameof(modVersion));

            if (apiVersion == null)
                throw new ArgumentNullException(nameof(apiVersion));

            if (protocolVersion == null)
                throw new ArgumentNullException(nameof(protocolVersion));

            if (networkState == null)
                throw new ArgumentNullException(nameof(networkState));

            if (presentationAdapter == null)
                throw new ArgumentNullException(nameof(presentationAdapter));

            ModVersion = modVersion;
            ApiVersion = apiVersion;
            ProtocolVersion = protocolVersion;
            NetworkState = networkState;
            PresentationAdapter = presentationAdapter;
            ExternalProviderCount = externalProviderCount;
        }
    }
}