using System;

namespace Mz.Networking
{
    /// <summary>
    /// Exposes transport-independent network send, receive, registration, and
    /// relay operations to a mod.
    /// </summary>
    public sealed class NetworkEndpoint
    {
        private readonly INetworkTransport _transport;
        private readonly NetworkMessageDispatcher _dispatcher;

        /// <summary>
        /// Creates a network endpoint over the supplied transport.
        /// </summary>
        public NetworkEndpoint(INetworkTransport transport)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));

            _transport = transport;
            _dispatcher = new NetworkMessageDispatcher();
        }

        /// <summary>
        /// Registers one application message handler.
        /// </summary>
        public NetworkMessageSubscription RegisterHandler(
            string messageType,
            Action<NetworkReceiveContext> handler
        )
        {
            return _dispatcher.RegisterHandler(
                messageType,
                handler
            );
        }

        /// <summary>
        /// Sends an application message to the authoritative server.
        /// </summary>
        public void SendToServer(
            string messageType,
            byte[] payload
        )
        {
            var envelope = new NetworkEnvelope(
                messageType,
                _transport.LocalPeerId,
                false,
                payload
            );

            if (_transport.IsServer)
            {
                NetworkReceiveContext ignored;

                Receive(
                    envelope,
                    _transport.LocalPeerId,
                    true,
                    out ignored
                );

                return;
            }

            _transport.SendToServer(envelope);
        }

        /// <summary>
        /// Sends an application message from the server to one peer.
        /// </summary>
        public void SendToPlayer(
            string messageType,
            byte[] payload,
            ulong peerId
        )
        {
            EnsureServer();

            var envelope = new NetworkEnvelope(
                messageType,
                _transport.LocalPeerId,
                false,
                payload
            );

            _transport.SendToPeer(
                envelope,
                peerId
            );
        }

        /// <summary>
        /// Processes an envelope received from the concrete transport.
        /// </summary>
        public bool Receive(
            NetworkEnvelope envelope,
            ulong transportSenderId,
            bool transportSenderIsServer,
            out NetworkReceiveContext context
        )
        {
            if (!_transport.IsServer
                && !transportSenderIsServer)
            {
                throw new InvalidOperationException(
                    "A client can only accept network messages sent "
                    + "by the authoritative server."
                );
            }

            bool dispatched =
                _dispatcher.TryDispatch(
                    envelope,
                    transportSenderId,
                    _transport.IsServer,
                    transportSenderIsServer,
                    out context
                );

            if (!dispatched)
                return false;

            if (_transport.IsServer)
                Relay(context);

            return true;
        }

        private void Relay(NetworkReceiveContext context)
        {
            if (context.RelayMode == NetworkRelayMode.None)
                return;

            NetworkEnvelope relayEnvelope =
                context.Envelope.WithRelay(true);

            switch (context.RelayMode)
            {
                case NetworkRelayMode.ToOthers:
                    _transport.SendToOthers(
                        relayEnvelope,
                        context.Envelope.OriginalSenderId
                    );
                    break;

                case NetworkRelayMode.ToEveryone:
                    _transport.SendToEveryone(relayEnvelope);
                    break;

                case NetworkRelayMode.ReturnToSender:
                    _transport.SendToPeer(
                        relayEnvelope,
                        context.Envelope.OriginalSenderId
                    );
                    break;

                default:
                    throw new InvalidOperationException(
                        "The receive handler selected an unsupported "
                        + "relay mode."
                    );
            }
        }

        private void EnsureServer()
        {
            if (!_transport.IsServer)
            {
                throw new InvalidOperationException(
                    "Only the authoritative server can send messages "
                    + "directly to another player."
                );
            }
        }
    }
}
