using System;

namespace Mz.Networking
{
    /// <summary>
    /// Applies transport-independent receive validation before dispatching a
    /// network message to application code.
    /// </summary>
    public static class NetworkMessageProcessor
    {

        /// <summary>
        /// Validates a received envelope and invokes its application handler.
        /// </summary>
        /// <param name="envelope">The received message envelope.</param>
        /// <param name="transportSenderId">
        /// The sender identity reported by the trusted transport.
        /// </param>
        /// <param name="isServer">
        /// Whether processing occurs on the authoritative server.
        /// </param>
        /// <param name="transportSenderIsServer">
        /// Whether the trusted transport identified the immediate sender as
        /// the authoritative server.
        /// </param>
        /// <param name="handler">
        /// The application handler that may select relay behavior.
        /// </param>
        /// <returns>The completed receive context.</returns>
        public static NetworkReceiveContext Process(
            NetworkEnvelope envelope,
            ulong transportSenderId,
            bool isServer,
            bool transportSenderIsServer,
            Action<NetworkReceiveContext> handler
        )
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (!isServer && !transportSenderIsServer)
            {
                throw new InvalidOperationException(
                    "A client can only accept network messages sent "
                    + "by the authoritative server."
                );
            }

            bool senderWasCorrected =
                isServer
                && envelope.OriginalSenderId
                    != transportSenderId;

            bool relayFlagWasCorrected =
                isServer
                && !transportSenderIsServer
                && envelope.IsRelay;

            NetworkEnvelope validatedEnvelope = envelope;

            if (senderWasCorrected)
            {
                validatedEnvelope =
                    validatedEnvelope.WithOriginalSenderId(
                        transportSenderId
                    );
            }

            if (relayFlagWasCorrected)
            {
                validatedEnvelope =
                    validatedEnvelope.WithRelay(false);
            }

            var context = new NetworkReceiveContext(
                validatedEnvelope,
                transportSenderId,
                isServer,
                transportSenderIsServer,
                senderWasCorrected,
                relayFlagWasCorrected
            );

            handler(context);

            ValidateRelayMode(context.RelayMode);

            return context;
        }

        private static void ValidateRelayMode(
            NetworkRelayMode relayMode
        )
        {
            if (relayMode < NetworkRelayMode.None
                || relayMode
                    > NetworkRelayMode.ReturnToSender)
            {
                throw new InvalidOperationException(
                    "The receive handler selected an unsupported "
                    + "relay mode."
                );
            }
        }
    }
}
