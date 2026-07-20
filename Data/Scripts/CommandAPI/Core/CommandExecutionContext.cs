using System;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandExecutionContext
    {
        public string RequestId { get; }

        public ulong RequesterSteamId { get; }

        public long RequesterIdentityId { get; }

        public string RequesterDisplayName { get; }

        public int PermissionLevel { get; }

        public bool IsServer { get; }

        public CommandExecutionContext(
            string requestId,
            ulong requesterSteamId,
            long requesterIdentityId,
            string requesterDisplayName,
            int permissionLevel,
            bool isServer
        )
        {
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException(
                    "Request ID is required.",
                    nameof(requestId)
                );

            if (requesterDisplayName == null)
                throw new ArgumentNullException(
                    nameof(requesterDisplayName)
                );

            RequestId = requestId;
            RequesterSteamId = requesterSteamId;
            RequesterIdentityId = requesterIdentityId;
            RequesterDisplayName = requesterDisplayName;
            PermissionLevel = permissionLevel;
            IsServer = isServer;
        }
    }
}
