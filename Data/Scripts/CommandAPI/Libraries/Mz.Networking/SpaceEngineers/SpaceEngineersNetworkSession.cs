using System;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Owns one fixed Space Engineers secure-message channel and exposes its
    /// transport-independent network endpoint.
    /// </summary>
    public sealed class SpaceEngineersNetworkSession :
        IDisposable
    {
        private readonly ISpaceEngineersNetworkGateway _gateway;
        private readonly Action<
            ushort,
            byte[],
            ulong,
            bool
        > _secureMessageHandler;

        private readonly Action<
            SpaceEngineersNetworkReceiveFailure
        > _receiveFailureHandler;

        private bool _disposed;

        /// <summary>
        /// Creates a session using the active Space Engineers ModAPI.
        /// </summary>
        public SpaceEngineersNetworkSession(
            ushort channelId,
            Action<SpaceEngineersNetworkReceiveFailure>
                receiveFailureHandler
        )
            : this(
                new SpaceEngineersNetworkGateway(),
                channelId,
                receiveFailureHandler
            )
        {
        }

        /// <summary>
        /// Creates a session over an explicit Space Engineers gateway.
        /// </summary>
        public SpaceEngineersNetworkSession(
            ISpaceEngineersNetworkGateway gateway,
            ushort channelId,
            Action<SpaceEngineersNetworkReceiveFailure>
                receiveFailureHandler
        )
        {
            if (gateway == null)
                throw new ArgumentNullException(nameof(gateway));

            if (receiveFailureHandler == null)
            {
                throw new ArgumentNullException(
                    nameof(receiveFailureHandler)
                );
            }

            _gateway = gateway;
            _receiveFailureHandler = receiveFailureHandler;
            ChannelId = channelId;

            Transport =
                new SpaceEngineersNetworkTransport(
                    gateway,
                    channelId
                );

            Endpoint = new NetworkEndpoint(Transport);
            _secureMessageHandler = ReceiveSecureMessage;

            _gateway.RegisterSecureMessageHandler(
                ChannelId,
                _secureMessageHandler
            );
        }

        /// <summary>
        /// Gets the owned secure-message channel.
        /// </summary>
        public ushort ChannelId { get; }

        /// <summary>
        /// Gets the concrete Space Engineers transport.
        /// </summary>
        public SpaceEngineersNetworkTransport Transport { get; }

        /// <summary>
        /// Gets the transport-independent mod-facing endpoint.
        /// </summary>
        public NetworkEndpoint Endpoint { get; }

        /// <summary>
        /// Removes the exact secure-message handler registration.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _gateway.UnregisterSecureMessageHandler(
                ChannelId,
                _secureMessageHandler
            );
        }

        private void ReceiveSecureMessage(
            ushort channelId,
            byte[] serialized,
            ulong senderPeerId,
            bool senderIsServer
        )
        {
            try
            {
                if (channelId != ChannelId)
                {
                    throw new InvalidOperationException(
                        "The secure-message handler received channel "
                        + channelId
                        + " instead of its configured channel "
                        + ChannelId
                        + "."
                    );
                }

                if (serialized == null)
                {
                    throw new ArgumentNullException(
                        nameof(serialized)
                    );
                }

                NetworkEnvelope envelope =
                    _gateway.Deserialize(serialized);

                NetworkReceiveContext ignored;

                Endpoint.Receive(
                    envelope,
                    senderPeerId,
                    senderIsServer,
                    out ignored
                );
            }
            catch (Exception exception)
            {
                byte[] failedMessage =
                    serialized ?? new byte[0];

                _receiveFailureHandler(
                    new SpaceEngineersNetworkReceiveFailure(
                        channelId,
                        failedMessage,
                        senderPeerId,
                        senderIsServer,
                        exception
                    )
                );
            }
        }
    }
}
