using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Uses the active Space Engineers ModAPI multiplayer and binary
    /// serialization services.
    /// </summary>
    public sealed class SpaceEngineersNetworkGateway :
        ISpaceEngineersNetworkGateway
    {
        /// <inheritdoc />
        public bool IsServer
        {
            get
            {
                return GetMultiplayer().IsServer;
            }
        }

        /// <inheritdoc />
        public ulong LocalPeerId
        {
            get
            {
                return GetMultiplayer().MyId;
            }
        }

        /// <inheritdoc />
        public void RegisterSecureMessageHandler(
            ushort channelId,
            Action<ushort, byte[], ulong, bool> handler
        )
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            GetMultiplayer().RegisterSecureMessageHandler(
                channelId,
                handler
            );
        }

        /// <inheritdoc />
        public void UnregisterSecureMessageHandler(
            ushort channelId,
            Action<ushort, byte[], ulong, bool> handler
        )
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            GetMultiplayer().UnregisterSecureMessageHandler(
                channelId,
                handler
            );
        }

        /// <inheritdoc />
        public byte[] Serialize(NetworkEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            var wire = new NetworkEnvelopeWire
            {
                MessageType = envelope.MessageType,
                OriginalSenderId = envelope.OriginalSenderId,
                IsRelay = envelope.IsRelay,
                Payload = envelope.Payload
            };

            return GetUtilities().SerializeToBinary(wire);
        }

        /// <inheritdoc />
        public NetworkEnvelope Deserialize(byte[] serialized)
        {
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            NetworkEnvelopeWire wire =
                GetUtilities()
                    .SerializeFromBinary<NetworkEnvelopeWire>(
                        serialized
                    );

            if (wire == null)
            {
                throw new InvalidOperationException(
                    "The serialized network envelope was empty."
                );
            }

            if (wire.Payload == null)
            {
                throw new InvalidOperationException(
                    "The serialized network envelope had no payload."
                );
            }

            return new NetworkEnvelope(
                wire.MessageType,
                wire.OriginalSenderId,
                wire.IsRelay,
                wire.Payload
            );
        }

        /// <inheritdoc />
        public bool SendToServer(
            ushort channelId,
            byte[] serialized
        )
        {
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            return GetMultiplayer().SendMessageToServer(
                channelId,
                serialized,
                true
            );
        }

        /// <inheritdoc />
        public bool SendToPeer(
            ushort channelId,
            byte[] serialized,
            ulong peerId
        )
        {
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            return GetMultiplayer().SendMessageTo(
                channelId,
                serialized,
                peerId,
                true
            );
        }

        /// <inheritdoc />
        public void GetPlayerIds(List<ulong> playerIds)
        {
            if (playerIds == null)
                throw new ArgumentNullException(nameof(playerIds));

            IMyPlayerCollection playerCollection =
                GetMultiplayer().Players;

            if (playerCollection == null)
            {
                throw new InvalidOperationException(
                    "Space Engineers player information is unavailable."
                );
            }

            var players = new List<IMyPlayer>();
            playerCollection.GetPlayers(players, null);

            for (var index = 0; index < players.Count; index++)
            {
                IMyPlayer player = players[index];

                if (player != null)
                    playerIds.Add(player.SteamUserId);
            }
        }

        private static IMyMultiplayer GetMultiplayer()
        {
            IMyMultiplayer multiplayer =
                MyAPIGateway.Multiplayer;

            if (multiplayer == null)
            {
                throw new InvalidOperationException(
                    "Space Engineers multiplayer is unavailable. "
                    + "Use networking during the active session "
                    + "lifecycle."
                );
            }

            return multiplayer;
        }

        private static IMyUtilities GetUtilities()
        {
            IMyUtilities utilities =
                MyAPIGateway.Utilities;

            if (utilities == null)
            {
                throw new InvalidOperationException(
                    "Space Engineers utilities are unavailable. "
                    + "Use networking during the active session "
                    + "lifecycle."
                );
            }

            return utilities;
        }
    }
}
