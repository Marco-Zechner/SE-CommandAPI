using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace MarcoZechner.CommandApi.Chat
{
    public static class SpaceEngineersExecutionContextProvider
    {
        public static CommandExecutionContext Create(
            ulong senderId
        )
        {
            return Create(
                senderId,
                Guid.NewGuid().ToString("N")
            );
        }

        public static CommandExecutionContext Create(
            ulong senderId,
            string requestId
        )
        {
            if (MyAPIGateway.Players == null)
            {
                throw new InvalidOperationException(
                    "Space Engineers player information is unavailable."
                );
            }

            if (MyAPIGateway.Session == null)
            {
                throw new InvalidOperationException(
                    "Space Engineers session information is unavailable."
                );
            }

            var players =
                new List<IMyPlayer>();

            MyAPIGateway.Players.GetPlayers(
                players,
                delegate(IMyPlayer player)
                {
                    return player != null
                        && player.SteamUserId == senderId;
                }
            );

            if (players.Count != 1)
            {
                throw new InvalidOperationException(
                    "The command requester could not be resolved."
                );
            }

            IMyPlayer requester =
                players[0];

            bool isServer =
                MyAPIGateway.Multiplayer != null
                && MyAPIGateway.Multiplayer.IsServer;

            return new CommandExecutionContext(
                requestId,
                requester.SteamUserId,
                requester.IdentityId,
                requester.DisplayName ?? string.Empty,
                (int)MyAPIGateway.Session
                    .GetUserPromoteLevel(senderId),
                isServer
            );
        }
    }
}
