using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using MarcoZechner.CommandApi.Networking;
using Mz.Networking;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandNetworkCoordinatorTests
    {
        [Fact]
        public void ClientSendsRequestAndCompletesMatchingResult()
        {
            var transport =
                new RecordingTransport(
                    false,
                    101UL
                );

            var endpoint =
                new NetworkEndpoint(transport);

            var observedResults =
                new List<CommandResultMessage>();

            using (
                var coordinator =
                    new CommandNetworkCoordinator(
                        endpoint,
                        new CommandExecutor(
                            new CommandRegistry()
                        ),
                        false,
                        101UL,
                        UnexpectedContext,
                        observedResults.Add
                    )
            )
            {
                coordinator.SendRequest(
                    "client-001",
                    new CommandInput(
                        "ping",
                        new string[0]
                    )
                );

                NetworkEnvelope requestEnvelope =
                    Assert.Single(
                        transport.ServerMessages
                    );

                Assert.Equal(
                    CommandNetworkCoordinator
                        .RequestMessageType,
                    requestEnvelope.MessageType
                );

                CommandRequestMessage request =
                    CommandMessageCodec
                        .DeserializeRequest(
                            requestEnvelope.Payload
                        );

                Assert.Equal(
                    "client-001",
                    request.RequestId
                );

                Assert.Equal(
                    "ping",
                    request.CommandName
                );

                byte[] resultPayload =
                    CommandMessageCodec.SerializeResult(
                        new CommandResultMessage(
                            "client-001",
                            true,
                            "Ping",
                            "Pong.",
                            new[]
                            {
                                "Server execution confirmed."
                            },
                            CommandSeverity.Success,
                            null
                        )
                    );

                endpoint.Receive(
                    new NetworkEnvelope(
                        CommandNetworkCoordinator
                            .ResultMessageType,
                        500UL,
                        false,
                        resultPayload
                    ),
                    500UL,
                    true,
                    out NetworkReceiveContext ignored
                );

                CommandResultMessage observed =
                    Assert.Single(observedResults);

                Assert.Equal(
                    "client-001",
                    observed.RequestId
                );

                Assert.Equal(
                    "Pong.",
                    observed.Summary
                );

                endpoint.Receive(
                    new NetworkEnvelope(
                        CommandNetworkCoordinator
                            .ResultMessageType,
                        500UL,
                        false,
                        resultPayload
                    ),
                    500UL,
                    true,
                    out ignored
                );

                Assert.Single(observedResults);
            }
        }

        [Fact]
        public void ClientIgnoresUnmatchedResultWithoutLosingPendingRequest()
        {
            var transport =
                new RecordingTransport(
                    false,
                    101UL
                );

            var endpoint =
                new NetworkEndpoint(transport);

            var observedResults =
                new List<CommandResultMessage>();

            using (
                var coordinator =
                    new CommandNetworkCoordinator(
                        endpoint,
                        new CommandExecutor(
                            new CommandRegistry()
                        ),
                        false,
                        101UL,
                        UnexpectedContext,
                        observedResults.Add
                    )
            )
            {
                coordinator.SendRequest(
                    "expected",
                    new CommandInput(
                        "ping",
                        new string[0]
                    )
                );

                DeliverResult(
                    endpoint,
                    "other"
                );

                Assert.Empty(observedResults);

                DeliverResult(
                    endpoint,
                    "expected"
                );

                Assert.Single(observedResults);

                Assert.Equal(
                    "expected",
                    observedResults[0].RequestId
                );
            }
        }

        [Fact]
        public void ServerExecutesUsingValidatedTransportSender()
        {
            var transport =
                new RecordingTransport(
                    true,
                    500UL
                );

            var endpoint =
                new NetworkEndpoint(transport);

            var registry =
                new CommandRegistry();

            CommandExecutionContext observedContext =
                null;

            Register(
                registry,
                "identity",
                delegate(
                    CommandExecutionContext context,
                    CommandInput input
                )
                {
                    observedContext = context;

                    return new CommandResult(
                        true,
                        "Identity",
                        "Resolved.",
                        new[]
                        {
                            context.RequesterSteamId
                                .ToString()
                        },
                        CommandSeverity.Success,
                        null
                    );
                }
            );

            ulong providerSender = 0UL;
            string providerRequestId = null;

            using (
                var coordinator =
                    new CommandNetworkCoordinator(
                        endpoint,
                        new CommandExecutor(registry),
                        true,
                        500UL,
                        delegate(
                            ulong senderId,
                            string requestId
                        )
                        {
                            providerSender = senderId;
                            providerRequestId = requestId;

                            return new CommandExecutionContext(
                                requestId,
                                senderId,
                                9001L,
                                "Remote player",
                                2,
                                true
                            );
                        },
                        UnexpectedResult
                    )
            )
            {
                byte[] requestPayload =
                    CommandMessageCodec.SerializeRequest(
                        new CommandRequestMessage(
                            "remote-001",
                            "identity",
                            new string[0]
                        )
                    );

                endpoint.Receive(
                    new NetworkEnvelope(
                        CommandNetworkCoordinator
                            .RequestMessageType,
                        999UL,
                        true,
                        requestPayload
                    ),
                    222UL,
                    false,
                    out NetworkReceiveContext receiveContext
                );

                Assert.True(
                    receiveContext
                        .OriginalSenderWasCorrected
                );

                Assert.True(
                    receiveContext
                        .RelayFlagWasCorrected
                );

                Assert.Equal(
                    222UL,
                    providerSender
                );

                Assert.Equal(
                    "remote-001",
                    providerRequestId
                );

                Assert.NotNull(observedContext);

                Assert.Equal(
                    222UL,
                    observedContext.RequesterSteamId
                );

                PeerRecord response =
                    Assert.Single(
                        transport.PeerMessages
                    );

                Assert.Equal(
                    222UL,
                    response.PeerId
                );

                Assert.Equal(
                    CommandNetworkCoordinator
                        .ResultMessageType,
                    response.Envelope.MessageType
                );

                CommandResultMessage result =
                    CommandMessageCodec
                        .DeserializeResult(
                            response.Envelope.Payload
                        );

                Assert.Equal(
                    "remote-001",
                    result.RequestId
                );

                Assert.Equal(
                    "222",
                    result.DetailLines[0]
                );
            }
        }

        [Fact]
        public void ListenServerCompletesLocalRequestWithoutPeerSend()
        {
            var transport =
                new RecordingTransport(
                    true,
                    500UL
                );

            var endpoint =
                new NetworkEndpoint(transport);

            var registry =
                new CommandRegistry();

            Register(
                registry,
                "ping",
                delegate(
                    CommandExecutionContext context,
                    CommandInput input
                )
                {
                    return new CommandResult(
                        true,
                        "Ping",
                        "Pong.",
                        new string[0],
                        CommandSeverity.Success,
                        null
                    );
                }
            );

            var observedResults =
                new List<CommandResultMessage>();

            using (
                var coordinator =
                    new CommandNetworkCoordinator(
                        endpoint,
                        new CommandExecutor(registry),
                        true,
                        500UL,
                        delegate(
                            ulong senderId,
                            string requestId
                        )
                        {
                            return new CommandExecutionContext(
                                requestId,
                                senderId,
                                500L,
                                "Host",
                                5,
                                true
                            );
                        },
                        observedResults.Add
                    )
            )
            {
                coordinator.SendRequest(
                    "host-001",
                    new CommandInput(
                        "ping",
                        new string[0]
                    )
                );

                CommandResultMessage result =
                    Assert.Single(observedResults);

                Assert.Equal(
                    "host-001",
                    result.RequestId
                );

                Assert.Equal(
                    "Pong.",
                    result.Summary
                );

                Assert.Empty(
                    transport.ServerMessages
                );

                Assert.Empty(
                    transport.PeerMessages
                );
            }
        }

        [Fact]
        public void MalformedRequestDoesNotSendResult()
        {
            var transport =
                new RecordingTransport(
                    true,
                    500UL
                );

            var endpoint =
                new NetworkEndpoint(transport);

            using (
                var coordinator =
                    new CommandNetworkCoordinator(
                        endpoint,
                        new CommandExecutor(
                            new CommandRegistry()
                        ),
                        true,
                        500UL,
                        UnexpectedContext,
                        UnexpectedResult
                    )
            )
            {
                Assert.Throws<InvalidOperationException>(
                    delegate
                    {
                        endpoint.Receive(
                            new NetworkEnvelope(
                                CommandNetworkCoordinator
                                    .RequestMessageType,
                                222UL,
                                false,
                                new byte[]
                                {
                                    1,
                                    2,
                                    3
                                }
                            ),
                            222UL,
                            false,
                            out NetworkReceiveContext ignored
                        );
                    }
                );

                Assert.Empty(
                    transport.PeerMessages
                );
            }
        }

        [Fact]
        public void DuplicatePendingRequestIdIsRejected()
        {
            var transport =
                new RecordingTransport(
                    false,
                    101UL
                );

            var endpoint =
                new NetworkEndpoint(transport);

            using (
                var coordinator =
                    new CommandNetworkCoordinator(
                        endpoint,
                        new CommandExecutor(
                            new CommandRegistry()
                        ),
                        false,
                        101UL,
                        UnexpectedContext,
                        UnexpectedResult
                    )
            )
            {
                coordinator.SendRequest(
                    "duplicate",
                    new CommandInput(
                        "ping",
                        new string[0]
                    )
                );

                Assert.Throws<InvalidOperationException>(
                    delegate
                    {
                        coordinator.SendRequest(
                            "duplicate",
                            new CommandInput(
                                "status",
                                new string[0]
                            )
                        );
                    }
                );

                Assert.Single(
                    transport.ServerMessages
                );
            }
        }

        [Fact]
        public void DisposeRemovesRequestAndResultHandlers()
        {
            var transport =
                new RecordingTransport(
                    true,
                    500UL
                );

            var endpoint =
                new NetworkEndpoint(transport);

            var coordinator =
                new CommandNetworkCoordinator(
                    endpoint,
                    new CommandExecutor(
                        new CommandRegistry()
                    ),
                    true,
                    500UL,
                    UnexpectedContext,
                    UnexpectedResult
                );

            coordinator.Dispose();
            coordinator.Dispose();

            bool requestDispatched =
                endpoint.Receive(
                    new NetworkEnvelope(
                        CommandNetworkCoordinator
                            .RequestMessageType,
                        222UL,
                        false,
                        new byte[0]
                    ),
                    222UL,
                    false,
                    out NetworkReceiveContext requestContext
                );

            bool resultDispatched =
                endpoint.Receive(
                    new NetworkEnvelope(
                        CommandNetworkCoordinator
                            .ResultMessageType,
                        500UL,
                        false,
                        new byte[0]
                    ),
                    500UL,
                    true,
                    out NetworkReceiveContext resultContext
                );

            Assert.False(requestDispatched);
            Assert.Null(requestContext);

            Assert.False(resultDispatched);
            Assert.Null(resultContext);
        }

        private static void DeliverResult(
            NetworkEndpoint endpoint,
            string requestId
        )
        {
            endpoint.Receive(
                new NetworkEnvelope(
                    CommandNetworkCoordinator
                        .ResultMessageType,
                    500UL,
                    false,
                    CommandMessageCodec.SerializeResult(
                        new CommandResultMessage(
                            requestId,
                            true,
                            "Completed",
                            "Done.",
                            new string[0],
                            CommandSeverity.Success,
                            null
                        )
                    )
                ),
                500UL,
                true,
                out NetworkReceiveContext ignored
            );
        }

        private static void Register(
            CommandRegistry registry,
            string name,
            CommandHandler handler
        )
        {
            string errorMessage;

            bool registered =
                registry.TryRegister(
                    new CommandDefinition(
                        name,
                        new string[0],
                        name + " command",
                        name + " help",
                        name,
                        "Tests",
                        CommandExecutionLocation.Server,
                        0,
                        "CommandAPI.Tests",
                        handler
                    ),
                    out errorMessage
                );

            Assert.True(
                registered,
                errorMessage
            );
        }

        private static CommandExecutionContext
            UnexpectedContext(
                ulong senderId,
                string requestId
            )
        {
            throw new InvalidOperationException(
                "Execution context was not expected."
            );
        }

        private static void UnexpectedResult(
            CommandResultMessage result
        )
        {
            throw new InvalidOperationException(
                "Command result was not expected."
            );
        }

        private sealed class RecordingTransport :
            INetworkTransport
        {
            public bool IsServer { get; }

            public ulong LocalPeerId { get; }

            public List<NetworkEnvelope>
                ServerMessages { get; } =
                    new List<NetworkEnvelope>();

            public List<PeerRecord>
                PeerMessages { get; } =
                    new List<PeerRecord>();

            public RecordingTransport(
                bool isServer,
                ulong localPeerId
            )
            {
                IsServer = isServer;
                LocalPeerId = localPeerId;
            }

            public void SendToServer(
                NetworkEnvelope envelope
            )
            {
                ServerMessages.Add(envelope);
            }

            public void SendToPeer(
                NetworkEnvelope envelope,
                ulong peerId
            )
            {
                PeerMessages.Add(
                    new PeerRecord(
                        envelope,
                        peerId
                    )
                );
            }

            public void SendToOthers(
                NetworkEnvelope envelope,
                ulong excludedPeerId
            )
            {
                throw new InvalidOperationException(
                    "Broadcast was not expected."
                );
            }

            public void SendToEveryone(
                NetworkEnvelope envelope
            )
            {
                throw new InvalidOperationException(
                    "Broadcast was not expected."
                );
            }
        }

        private sealed class PeerRecord
        {
            public NetworkEnvelope Envelope { get; }

            public ulong PeerId { get; }

            public PeerRecord(
                NetworkEnvelope envelope,
                ulong peerId
            )
            {
                Envelope = envelope;
                PeerId = peerId;
            }
        }
    }
}
