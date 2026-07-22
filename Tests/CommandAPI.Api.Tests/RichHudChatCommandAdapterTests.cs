using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Chat;
using MarcoZechner.CommandApi.Core;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class RichHudChatCommandAdapterTests
    {
        private const long DiscoveryChannelId =
            6098967432095689633L;

        [Fact]
        public void ConnectsSubmitsAndPresentsThroughRichHudChatApi()
        {
            var bus =
                new RecordingModMessageBus();

            Action<string> submittedRoute =
                null;

            IDictionary<string, object> routeMetadata =
                null;

            int participantUnregisterCount = 0;
            int routeUnregisterCount = 0;

            var transcript =
                new List<TranscriptLine>();

            Func<
                IDictionary<string, object>,
                IDictionary<string, object>
            > registerParticipant =
                delegate(
                    IDictionary<string, object> metadata
                )
                {
                    Assert.Equal(
                        "MarcoZechner.CommandAPI",
                        metadata["OwnerId"]
                    );

                    Assert.Equal(
                        "CommandAPI",
                        metadata["DisplayName"]
                    );

                    return new Dictionary<string, object>
                    {
                        {
                            "RegistrationId",
                            "registration-1"
                        },
                        {
                            "Unregister",
                            new Action(
                                delegate
                                {
                                    participantUnregisterCount++;
                                }
                            )
                        }
                    };
                };

            Func<
                IDictionary<string, object>,
                Action<string>,
                Action<string>,
                Action
            > registerRoute =
                delegate(
                    IDictionary<string, object> metadata,
                    Action<string> submit,
                    Action<string> inputChanged
                )
                {
                    routeMetadata = metadata;
                    submittedRoute = submit;

                    Assert.Null(inputChanged);

                    return delegate
                    {
                        routeUnregisterCount++;
                    };
                };

            Action<
                IDictionary<string, object>
            > appendTranscript =
                delegate(
                    IDictionary<string, object> entry
                )
                {
                    transcript.Add(
                        new TranscriptLine(
                            (string)entry["Author"],
                            (string)entry["Message"]
                        )
                    );
                };

            var endpoints =
                new Dictionary<string, Delegate>(
                    StringComparer.Ordinal
                )
                {
                    {
                        "RegisterParticipant",
                        registerParticipant
                    },
                    {
                        "RegisterRoute",
                        registerRoute
                    },
                    {
                        "AppendTranscript",
                        appendTranscript
                    }
                };

            var provider =
                new ApiDiscoveryProvider(
                    bus,
                    DiscoveryChannelId,
                    new ApiModIdentity(
                        "MarcoZechner.RichHudChatAPI",
                        "RichHudChatAPI",
                        new SemanticVersion(
                            0,
                            1,
                            0
                        )
                    ),
                    new ApiDescriptor(
                        "MarcoZechner.RichHudChatAPI",
                        new SemanticVersion(
                            1,
                            0,
                            0
                        )
                    ),
                    endpoints
                );

            ulong observedSender = 0UL;
            CommandInput observedInput = null;
            int submissionCount = 0;

            var adapter =
                new RichHudChatCommandAdapter(
                    bus,
                    42UL,
                    delegate(
                        ulong senderId,
                        CommandInput input
                    )
                    {
                        observedSender = senderId;
                        observedInput = input;
                        submissionCount++;
                    }
                );

            provider.Start();
            adapter.Start();

            Assert.True(adapter.IsConnected);
            Assert.Null(adapter.LastError);
            Assert.NotNull(routeMetadata);
            Assert.NotNull(submittedRoute);

            Assert.Equal(
                "registration-1",
                routeMetadata["RegistrationId"]
            );

            Assert.Equal(
                "commands",
                routeMetadata["RouteId"]
            );

            Assert.Equal(
                "/cmd",
                routeMetadata["Prefix"]
            );

            Assert.Equal(
                false,
                routeMetadata["IsDefault"]
            );

            Assert.Single(transcript);
            Assert.Equal(
                "Ready. Use /cmd help.",
                transcript[0].Message
            );

            submittedRoute(
                "/cmd help"
            );

            Assert.Equal(1, submissionCount);
            Assert.Equal(42UL, observedSender);
            Assert.NotNull(observedInput);
            Assert.Equal("/cmd", observedInput.Prefix);
            Assert.Equal("help", observedInput.CommandName);
            Assert.Empty(observedInput.Arguments);

            Assert.Equal(
                "> /cmd help",
                transcript[1].Message
            );

            Assert.True(
                adapter.PresentResult(
                    new CommandResult(
                        true,
                        "Command help",
                        "Available commands: 2",
                        new[]
                        {
                            "help - Lists commands.",
                            "ping - Tests execution."
                        },
                        CommandSeverity.Information,
                        "/cmd help [command]"
                    )
                )
            );

            Assert.Equal(
                "Command help: Available commands: 2",
                transcript[2].Message
            );

            Assert.Equal(
                "help - Lists commands.",
                transcript[3].Message
            );

            Assert.Equal(
                "ping - Tests execution.",
                transcript[4].Message
            );

            Assert.Equal(
                "Usage: /cmd help [command]",
                transcript[5].Message
            );

            submittedRoute(
                "/cmd echo \"unterminated"
            );

            Assert.Equal(1, submissionCount);

            Assert.Equal(
                "> /cmd echo \"unterminated",
                transcript[6].Message
            );

            Assert.Equal(
                "Command error: Quoted argument is not terminated.",
                transcript[7].Message
            );

            provider.Stop();

            Assert.False(adapter.IsConnected);
            Assert.Equal(1, routeUnregisterCount);
            Assert.Equal(1, participantUnregisterCount);

            Assert.False(
                adapter.PresentResult(
                    new CommandResult(
                        true,
                        "Ignored",
                        "Disconnected",
                        new string[0],
                        CommandSeverity.Information,
                        null
                    )
                )
            );

            adapter.Dispose();
            provider.Dispose();
        }

        private sealed class TranscriptLine
        {
            public string Author
            {
                get;
                private set;
            }

            public string Message
            {
                get;
                private set;
            }

            public TranscriptLine(
                string author,
                string message
            )
            {
                Author = author;
                Message = message;
            }
        }

        private sealed class RecordingModMessageBus :
            IModMessageBus
        {
            private readonly Dictionary<
                long,
                List<Action<object>>
            > _handlers =
                new Dictionary<
                    long,
                    List<Action<object>>
                >();

            public void RegisterHandler(
                long channelId,
                Action<object> handler
            )
            {
                List<Action<object>> handlers;

                if (
                    !_handlers.TryGetValue(
                        channelId,
                        out handlers
                    )
                )
                {
                    handlers =
                        new List<Action<object>>();

                    _handlers.Add(
                        channelId,
                        handlers
                    );
                }

                handlers.Add(handler);
            }

            public void UnregisterHandler(
                long channelId,
                Action<object> handler
            )
            {
                List<Action<object>> handlers;

                if (
                    _handlers.TryGetValue(
                        channelId,
                        out handlers
                    )
                )
                {
                    handlers.Remove(handler);
                }
            }

            public void Send(
                long channelId,
                object payload
            )
            {
                List<Action<object>> handlers;

                if (
                    !_handlers.TryGetValue(
                        channelId,
                        out handlers
                    )
                )
                {
                    return;
                }

                Action<object>[] snapshot =
                    handlers.ToArray();

                for (
                    int index = 0;
                    index < snapshot.Length;
                    index++
                )
                {
                    snapshot[index](
                        payload
                    );
                }
            }
        }
    }
}