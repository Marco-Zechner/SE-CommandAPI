using System;
using MarcoZechner.CommandApi.Core;
using MarcoZechner.CommandApi.Networking;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandMessageCodecTests
    {
        [Fact]
        public void RequestRoundTripPreservesCommandData()
        {
            var request = new CommandRequestMessage(
                "request-001",
                "/IME",
                "EcHo",
                new[]
                {
                    "Hello World",
                    "MiXeD"
                }
            );

            byte[] payload =
                CommandMessageCodec.SerializeRequest(request);

            CommandRequestMessage decoded =
                CommandMessageCodec.DeserializeRequest(payload);

            Assert.Equal("request-001", decoded.RequestId);
            Assert.Equal("/ime", decoded.Prefix);
            Assert.Equal("EcHo", decoded.CommandName);

            Assert.Equal(
                new[]
                {
                    "Hello World",
                    "MiXeD"
                },
                decoded.Arguments
            );
        }

        [Fact]
        public void ResultRoundTripPreservesStructuredResult()
        {
            var result = new CommandResultMessage(
                "request-002",
                false,
                "Permission denied",
                "You do not have permission.",
                new[]
                {
                    "Required level: 3",
                    "Actual level: 1"
                },
                CommandSeverity.Error,
                "/cmd help"
            );

            byte[] payload =
                CommandMessageCodec.SerializeResult(result);

            CommandResultMessage decoded =
                CommandMessageCodec.DeserializeResult(payload);

            Assert.Equal("request-002", decoded.RequestId);
            Assert.False(decoded.IsSuccess);
            Assert.Equal("Permission denied", decoded.Title);

            Assert.Equal(
                "You do not have permission.",
                decoded.Summary
            );

            Assert.Equal(
                new[]
                {
                    "Required level: 3",
                    "Actual level: 1"
                },
                decoded.DetailLines
            );

            Assert.Equal(
                CommandSeverity.Error,
                decoded.Severity
            );

            Assert.Equal("/cmd help", decoded.UsageHint);
        }

        [Fact]
        public void RequestCopiesArguments()
        {
            string[] arguments =
            {
                "Original"
            };

            var request = new CommandRequestMessage(
                "request-copy",
                "echo",
                arguments
            );

            arguments[0] = "Changed";

            string[] firstRead =
                request.Arguments;

            firstRead[0] = "Changed again";

            Assert.Equal(
                "Original",
                request.Arguments[0]
            );
        }

        [Fact]
        public void ResultCopiesDetailLines()
        {
            string[] details =
            {
                "Original"
            };

            var result = new CommandResultMessage(
                "result-copy",
                true,
                "Completed",
                "Done.",
                details,
                CommandSeverity.Success,
                null
            );

            details[0] = "Changed";

            string[] firstRead =
                result.DetailLines;

            firstRead[0] = "Changed again";

            Assert.Equal(
                "Original",
                result.DetailLines[0]
            );
        }

        [Fact]
        public void DeserializeRequestRejectsResultPayload()
        {
            byte[] payload =
                CommandMessageCodec.SerializeResult(
                    new CommandResultMessage(
                        "request-kind",
                        true,
                        "Completed",
                        "Done.",
                        new string[0],
                        CommandSeverity.Success,
                        null
                    )
                );

            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    CommandMessageCodec.DeserializeRequest(
                        payload
                    );
                }
            );
        }

        [Fact]
        public void DeserializeRequestRejectsTruncatedPayload()
        {
            byte[] payload =
                CommandMessageCodec.SerializeRequest(
                    new CommandRequestMessage(
                        "request-truncated",
                        "ping",
                        new string[0]
                    )
                );

            var truncated =
                new byte[payload.Length - 1];

            Array.Copy(
                payload,
                truncated,
                truncated.Length
            );

            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    CommandMessageCodec.DeserializeRequest(
                        truncated
                    );
                }
            );
        }

        [Fact]
        public void DeserializeRequestRejectsTrailingData()
        {
            byte[] payload =
                CommandMessageCodec.SerializeRequest(
                    new CommandRequestMessage(
                        "request-trailing",
                        "ping",
                        new string[0]
                    )
                );

            var extended =
                new byte[payload.Length + 1];

            Array.Copy(
                payload,
                extended,
                payload.Length
            );

            extended[extended.Length - 1] = 99;

            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    CommandMessageCodec.DeserializeRequest(
                        extended
                    );
                }
            );
        }

        [Fact]
        public void SerializeRequestRejectsExcessiveArgumentCount()
        {
            var arguments =
                new string[
                    CommandMessageCodec.MaximumArguments + 1
                ];

            for (
                int index = 0;
                index < arguments.Length;
                index++
            )
            {
                arguments[index] = "value";
            }

            var request = new CommandRequestMessage(
                "request-large",
                "echo",
                arguments
            );

            Assert.Throws<ArgumentException>(
                delegate
                {
                    CommandMessageCodec.SerializeRequest(
                        request
                    );
                }
            );
        }
    }
}
