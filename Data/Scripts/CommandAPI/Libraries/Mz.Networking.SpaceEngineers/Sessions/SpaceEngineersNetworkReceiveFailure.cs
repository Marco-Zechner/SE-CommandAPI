using System;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Describes a secure network message that could not be deserialized or
    /// processed.
    /// </summary>
    public sealed class SpaceEngineersNetworkReceiveFailure
    {
        private readonly byte[] _serializedMessage;

        /// <summary>
        /// Gets the secure-message channel that received the packet.
        /// </summary>
        public ushort ChannelId { get; }

        /// <summary>
        /// Gets the immediate transport sender identity.
        /// </summary>
        public ulong SenderPeerId { get; }

        /// <summary>
        /// Gets whether the transport identified the sender as the server.
        /// </summary>
        public bool SenderIsServer { get; }

        /// <summary>
        /// Gets the exception raised while processing the packet.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets a copy of the received serialized packet.
        /// </summary>
        public byte[] SerializedMessage
        {
            get
            {
                return Copy(_serializedMessage);
            }
        }

        internal SpaceEngineersNetworkReceiveFailure(
            ushort channelId,
            byte[] serializedMessage,
            ulong senderPeerId,
            bool senderIsServer,
            Exception exception
        )
        {
            if (serializedMessage == null)
            {
                throw new ArgumentNullException(
                    nameof(serializedMessage)
                );
            }

            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            ChannelId = channelId;
            SenderPeerId = senderPeerId;
            SenderIsServer = senderIsServer;
            Exception = exception;
            _serializedMessage = Copy(serializedMessage);
        }

        private static byte[] Copy(byte[] source)
        {
            var copy = new byte[source.Length];

            Array.Copy(
                source,
                copy,
                source.Length
            );

            return copy;
        }
    }
}
