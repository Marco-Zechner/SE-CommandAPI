using System;
using System.Text;
using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Networking
{
    public static class CommandMessageCodec
    {
        public const int MaximumArguments = 32;

        public const int MaximumDetailLines = 64;

        public const int MaximumPayloadBytes = 60000;

        private const int Magic = 0x434D4441;
        private const byte WireVersion = 2;
        private const byte RequestKind = 1;
        private const byte ResultKind = 2;

        private const int MaximumRequestIdBytes = 128;
        private const int MaximumPrefixBytes = 128;
        private const int MaximumCommandNameBytes = 128;
        private const int MaximumTextBytes = 4096;

        private static readonly Encoding Utf8 =
            new UTF8Encoding(false, true);

        public static byte[] SerializeRequest(
            CommandRequestMessage message
        )
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            string[] arguments = message.Arguments;

            if (arguments.Length > MaximumArguments)
            {
                throw new ArgumentException(
                    "The command request contains too many arguments.",
                    nameof(message)
                );
            }

            var writer =
                new MessageWriter(MaximumPayloadBytes);

            WriteHeader(writer, RequestKind);

            writer.WriteString(
                message.RequestId,
                MaximumRequestIdBytes,
                "request ID"
            );

            writer.WriteString(
                message.Prefix,
                MaximumPrefixBytes,
                "command prefix"
            );

            writer.WriteString(
                message.CommandName,
                MaximumCommandNameBytes,
                "command name"
            );

            writer.WriteInt32(arguments.Length);

            for (
                int index = 0;
                index < arguments.Length;
                index++
            )
            {
                writer.WriteString(
                    arguments[index],
                    MaximumTextBytes,
                    "command argument"
                );
            }

            return writer.ToArray();
        }

        public static CommandRequestMessage DeserializeRequest(
            byte[] payload
        )
        {
            ValidatePayload(payload);

            try
            {
                var reader =
                    new MessageReader(payload);

                ReadHeader(reader, RequestKind);

                string requestId =
                    reader.ReadString(
                        MaximumRequestIdBytes,
                        "request ID"
                    );

                string prefix =
                    reader.ReadString(
                        MaximumPrefixBytes,
                        "command prefix"
                    );

                string commandName =
                    reader.ReadString(
                        MaximumCommandNameBytes,
                        "command name"
                    );

                int argumentCount =
                    reader.ReadCount(
                        MaximumArguments,
                        "argument"
                    );

                var arguments =
                    new string[argumentCount];

                for (
                    int index = 0;
                    index < argumentCount;
                    index++
                )
                {
                    arguments[index] =
                        reader.ReadString(
                            MaximumTextBytes,
                            "command argument"
                        );
                }

                reader.EnsureComplete();

                return new CommandRequestMessage(
                    requestId,
                    prefix,
                    commandName,
                    arguments
                );
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw InvalidPayload(exception);
            }
        }

        public static byte[] SerializeResult(
            CommandResultMessage message
        )
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            string[] detailLines =
                message.DetailLines;

            if (
                detailLines.Length
                > MaximumDetailLines
            )
            {
                throw new ArgumentException(
                    "The command result contains too many detail lines.",
                    nameof(message)
                );
            }

            if (
                !Enum.IsDefined(
                    typeof(CommandSeverity),
                    message.Severity
                )
            )
            {
                throw new ArgumentException(
                    "The command result severity is invalid.",
                    nameof(message)
                );
            }

            var writer =
                new MessageWriter(MaximumPayloadBytes);

            WriteHeader(writer, ResultKind);

            writer.WriteString(
                message.RequestId,
                MaximumRequestIdBytes,
                "request ID"
            );

            writer.WriteBoolean(message.IsSuccess);

            writer.WriteString(
                message.Title,
                MaximumTextBytes,
                "result title"
            );

            writer.WriteString(
                message.Summary,
                MaximumTextBytes,
                "result summary"
            );

            writer.WriteInt32(detailLines.Length);

            for (
                int index = 0;
                index < detailLines.Length;
                index++
            )
            {
                writer.WriteString(
                    detailLines[index],
                    MaximumTextBytes,
                    "result detail"
                );
            }

            writer.WriteInt32(
                (int)message.Severity
            );

            writer.WriteNullableString(
                message.UsageHint,
                MaximumTextBytes,
                "usage hint"
            );

            return writer.ToArray();
        }

        public static CommandResultMessage DeserializeResult(
            byte[] payload
        )
        {
            ValidatePayload(payload);

            try
            {
                var reader =
                    new MessageReader(payload);

                ReadHeader(reader, ResultKind);

                string requestId =
                    reader.ReadString(
                        MaximumRequestIdBytes,
                        "request ID"
                    );

                bool isSuccess =
                    reader.ReadBoolean();

                string title =
                    reader.ReadString(
                        MaximumTextBytes,
                        "result title"
                    );

                string summary =
                    reader.ReadString(
                        MaximumTextBytes,
                        "result summary"
                    );

                int detailCount =
                    reader.ReadCount(
                        MaximumDetailLines,
                        "detail line"
                    );

                var detailLines =
                    new string[detailCount];

                for (
                    int index = 0;
                    index < detailCount;
                    index++
                )
                {
                    detailLines[index] =
                        reader.ReadString(
                            MaximumTextBytes,
                            "result detail"
                        );
                }

                int numericSeverity =
                    reader.ReadInt32();

                if (
                    !Enum.IsDefined(
                        typeof(CommandSeverity),
                        numericSeverity
                    )
                )
                {
                    throw new InvalidOperationException(
                        "The command-result severity is invalid."
                    );
                }

                string usageHint =
                    reader.ReadNullableString(
                        MaximumTextBytes,
                        "usage hint"
                    );

                reader.EnsureComplete();

                return new CommandResultMessage(
                    requestId,
                    isSuccess,
                    title,
                    summary,
                    detailLines,
                    (CommandSeverity)numericSeverity,
                    usageHint
                );
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw InvalidPayload(exception);
            }
        }

        private static void WriteHeader(
            MessageWriter writer,
            byte messageKind
        )
        {
            writer.WriteInt32(Magic);
            writer.WriteByte(WireVersion);
            writer.WriteByte(messageKind);
        }

        private static void ReadHeader(
            MessageReader reader,
            byte expectedKind
        )
        {
            int magic =
                reader.ReadInt32();

            if (magic != Magic)
            {
                throw new InvalidOperationException(
                    "The command message has an invalid header."
                );
            }

            byte version =
                reader.ReadByte();

            if (version != WireVersion)
            {
                throw new InvalidOperationException(
                    "The command message uses an unsupported wire version."
                );
            }

            byte kind =
                reader.ReadByte();

            if (kind != expectedKind)
            {
                throw new InvalidOperationException(
                    "The command message has the wrong message kind."
                );
            }
        }

        private static void ValidatePayload(
            byte[] payload
        )
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            if (payload.Length == 0)
            {
                throw new InvalidOperationException(
                    "The command-message payload is empty."
                );
            }

            if (
                payload.Length
                > MaximumPayloadBytes
            )
            {
                throw new InvalidOperationException(
                    "The command-message payload is too large."
                );
            }
        }

        private static InvalidOperationException
            InvalidPayload(Exception exception)
        {
            return new InvalidOperationException(
                "The command-message payload is invalid.",
                exception
            );
        }

        private sealed class MessageWriter
        {
            private readonly byte[] _buffer;

            private int _position;

            public MessageWriter(int maximumBytes)
            {
                _buffer =
                    new byte[maximumBytes];
            }

            public void WriteByte(byte value)
            {
                EnsureCapacity(1);

                _buffer[_position] = value;
                _position++;
            }

            public void WriteBoolean(bool value)
            {
                WriteByte(
                    value
                        ? (byte)1
                        : (byte)0
                );
            }

            public void WriteInt32(int value)
            {
                EnsureCapacity(4);

                _buffer[_position] =
                    (byte)value;

                _buffer[_position + 1] =
                    (byte)(value >> 8);

                _buffer[_position + 2] =
                    (byte)(value >> 16);

                _buffer[_position + 3] =
                    (byte)(value >> 24);

                _position += 4;
            }

            public void WriteString(
                string value,
                int maximumBytes,
                string fieldName
            )
            {
                if (value == null)
                {
                    throw new ArgumentNullException(
                        fieldName
                    );
                }

                byte[] encoded =
                    Utf8.GetBytes(value);

                if (
                    encoded.Length
                    > maximumBytes
                )
                {
                    throw new ArgumentException(
                        "The " + fieldName + " is too long.",
                        fieldName
                    );
                }

                WriteInt32(encoded.Length);
                WriteBytes(encoded);
            }

            public void WriteNullableString(
                string value,
                int maximumBytes,
                string fieldName
            )
            {
                bool hasValue =
                    value != null;

                WriteBoolean(hasValue);

                if (hasValue)
                {
                    WriteString(
                        value,
                        maximumBytes,
                        fieldName
                    );
                }
            }

            public byte[] ToArray()
            {
                var result =
                    new byte[_position];

                for (
                    int index = 0;
                    index < _position;
                    index++
                )
                {
                    result[index] =
                        _buffer[index];
                }

                return result;
            }

            private void WriteBytes(
                byte[] bytes
            )
            {
                EnsureCapacity(bytes.Length);

                for (
                    int index = 0;
                    index < bytes.Length;
                    index++
                )
                {
                    _buffer[_position + index] =
                        bytes[index];
                }

                _position += bytes.Length;
            }

            private void EnsureCapacity(
                int additionalBytes
            )
            {
                if (
                    additionalBytes < 0
                    || additionalBytes
                        > _buffer.Length - _position
                )
                {
                    throw new ArgumentException(
                        "The serialized command message is too large."
                    );
                }
            }
        }

        private sealed class MessageReader
        {
            private readonly byte[] _payload;

            private int _position;

            public MessageReader(byte[] payload)
            {
                _payload = payload;
            }

            public byte ReadByte()
            {
                EnsureAvailable(1);

                byte value =
                    _payload[_position];

                _position++;

                return value;
            }

            public bool ReadBoolean()
            {
                byte value =
                    ReadByte();

                if (value == 0)
                    return false;

                if (value == 1)
                    return true;

                throw new InvalidOperationException(
                    "The command message contains an invalid Boolean value."
                );
            }

            public int ReadInt32()
            {
                EnsureAvailable(4);

                int value =
                    _payload[_position]
                    | (_payload[_position + 1] << 8)
                    | (_payload[_position + 2] << 16)
                    | (_payload[_position + 3] << 24);

                _position += 4;

                return value;
            }

            public string ReadString(
                int maximumBytes,
                string fieldName
            )
            {
                int length =
                    ReadInt32();

                if (
                    length < 0
                    || length > maximumBytes
                )
                {
                    throw new InvalidOperationException(
                        "The " + fieldName + " length is invalid."
                    );
                }

                EnsureAvailable(length);

                string value =
                    Utf8.GetString(
                        _payload,
                        _position,
                        length
                    );

                _position += length;

                return value;
            }

            public string ReadNullableString(
                int maximumBytes,
                string fieldName
            )
            {
                bool hasValue =
                    ReadBoolean();

                if (!hasValue)
                    return null;

                return ReadString(
                    maximumBytes,
                    fieldName
                );
            }

            public int ReadCount(
                int maximumCount,
                string itemName
            )
            {
                int count =
                    ReadInt32();

                if (
                    count < 0
                    || count > maximumCount
                )
                {
                    throw new InvalidOperationException(
                        "The " + itemName + " count is invalid."
                    );
                }

                return count;
            }

            public void EnsureComplete()
            {
                if (
                    _position
                    != _payload.Length
                )
                {
                    throw new InvalidOperationException(
                        "The command message contains trailing data."
                    );
                }
            }

            private void EnsureAvailable(
                int requiredBytes
            )
            {
                if (
                    requiredBytes < 0
                    || requiredBytes
                        > _payload.Length - _position
                )
                {
                    throw new InvalidOperationException(
                        "The command message is truncated."
                    );
                }
            }
        }
    }
}
