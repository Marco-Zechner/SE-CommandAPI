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
            ApiProtocolChannels.Discovery;

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

            Assert.False(
                routeMetadata.ContainsKey(
                    "ActivationPrefix"
                )
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

        [Fact]
        public void PublishesRegistrySuggestionsAndCompletesSelectedCommand()
        {
            var bus =
                new RecordingModMessageBus();

            var registry =
                new CommandRegistry();

            Register(
                registry,
                new CommandDefinition(
                    "help",
                    new string[0],
                    "Lists available commands.",
                    "Lists commands registered under /cmd.",
                    "help [command]",
                    "CommandAPI",
                    CommandExecutionLocation.Client,
                    0,
                    "CommandAPI.Tests",
                    NoOpHandler
                )
            );

            Register(
                registry,
                new CommandDefinition(
                    "ping",
                    new[] { "latency" },
                    "Tests command execution.",
                    "Runs the command execution smoke path.",
                    "ping",
                    "CommandAPI",
                    CommandExecutionLocation.Server,
                    0,
                    "CommandAPI.Tests",
                    NoOpHandler
                )
            );

            Action<string> inputChanged =
                null;

            Action<string> interaction =
                null;

            IDictionary<string, object> routeMetadata =
                null;

            IDictionary<string, object> companionRequest =
                null;

            IDictionary<string, object> inputRequest =
                null;

            int interactionUnregisterCount = 0;
            int routeUnregisterCount = 0;
            int participantUnregisterCount = 0;
            int clearCompanionCount = 0;

            Func<
                IDictionary<string, object>,
                IDictionary<string, object>
            > registerParticipant =
                delegate(
                    IDictionary<string, object> metadata
                )
                {
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
                    Action<string> changed
                )
                {
                    routeMetadata = metadata;
                    inputChanged = changed;

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
                };

            Func<
                IDictionary<string, object>,
                bool
            > setCompanion =
                delegate(
                    IDictionary<string, object> request
                )
                {
                    companionRequest = request;
                    return true;
                };

            Func<
                IDictionary<string, object>,
                bool
            > clearCompanion =
                delegate(
                    IDictionary<string, object> request
                )
                {
                    clearCompanionCount++;
                    return true;
                };

            Func<
                IDictionary<string, object>,
                Action<string>,
                Action
            > registerRouteInteraction =
                delegate(
                    IDictionary<string, object> metadata,
                    Action<string> handler
                )
                {
                    interaction = handler;

                    return delegate
                    {
                        interactionUnregisterCount++;
                    };
                };

            Func<
                IDictionary<string, object>,
                bool
            > setInput =
                delegate(
                    IDictionary<string, object> request
                )
                {
                    inputRequest = request;
                    return true;
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
                    },
                    {
                        "SetCompanion",
                        setCompanion
                    },
                    {
                        "ClearCompanion",
                        clearCompanion
                    },
                    {
                        "RegisterRouteInteraction",
                        registerRouteInteraction
                    },
                    {
                        "SetInput",
                        setInput
                    }
                };

            var provider =
                new ApiDiscoveryProvider(
                    bus,
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
                            3,
                            0
                        )
                    ),
                    endpoints
                );

            var adapter =
                new RichHudChatCommandAdapter(
                    bus,
                    42UL,
                    delegate(
                        ulong senderId,
                        CommandInput input
                    )
                    {
                    },
                    registry
                );

            provider.Start();
            adapter.Start();

            Assert.True(adapter.IsConnected);
            Assert.NotNull(routeMetadata);
            Assert.NotNull(inputChanged);
            Assert.NotNull(interaction);

            Assert.Equal(
                "/cmd",
                routeMetadata["Prefix"]
            );

            Assert.Equal(
                "/",
                routeMetadata["ActivationPrefix"]
            );

            inputChanged(
                "/"
            );

            Assert.NotNull(companionRequest);
            Assert.Equal(
                "registration-1",
                companionRequest["RegistrationId"]
            );
            Assert.Equal(
                "commands",
                companionRequest["RouteId"]
            );
            Assert.Equal(
                0,
                companionRequest["SelectedIndex"]
            );
            Assert.Equal(
                "/cmd <command>",
                companionRequest["HeaderText"]
            );
            Assert.Equal(
                "Up/Down selects | Tab completes",
                companionRequest["Footer"]
            );
            Assert.False(
                companionRequest.ContainsKey(
                    "ErrorText"
                )
            );

            IDictionary<string, object>[] headerSpans =
                ReadDictionaries(
                    companionRequest,
                    "HeaderSpans"
                );

            AssertSpan(
                headerSpans[0],
                0,
                4,
                "Valid"
            );

            AssertSpan(
                headerSpans[1],
                4,
                10,
                "Muted"
            );

            IDictionary<string, object>[] inputSpans =
                ReadDictionaries(
                    companionRequest,
                    "InputSpans"
                );

            Assert.Single(inputSpans);

            AssertSpan(
                inputSpans[0],
                0,
                1,
                "Valid"
            );

            IDictionary<string, object>[] items =
                ReadDictionaries(
                    companionRequest,
                    "Items"
                );

            Assert.Equal(
                2,
                items.Length
            );

            Assert.Equal(
                "/cmd help",
                items[0]["PrimaryText"]
            );
            Assert.Equal(
                "Lists available commands.",
                items[0]["SecondaryText"]
            );
            Assert.Equal(
                "/cmd help",
                items[0]["CompletionText"]
            );

            IDictionary<string, object>[] helpSpans =
                ReadDictionaries(
                    items[0],
                    "PrimarySpans"
                );

            Assert.Single(helpSpans);

            AssertSpan(
                helpSpans[0],
                0,
                4,
                "Valid"
            );

            Assert.Equal(
                "/cmd ping",
                items[1]["PrimaryText"]
            );
            Assert.Equal(
                "Tests command execution.",
                items[1]["SecondaryText"]
            );
            Assert.Equal(
                "/cmd ping",
                items[1]["CompletionText"]
            );

            interaction(
                "Next"
            );

            Assert.Equal(
                1,
                companionRequest["SelectedIndex"]
            );

            interaction(
                "Previous"
            );

            Assert.Equal(
                0,
                companionRequest["SelectedIndex"]
            );

            inputChanged(
                "/cmd p"
            );

            Assert.Equal(
                0,
                companionRequest["SelectedIndex"]
            );

            items =
                ReadDictionaries(
                    companionRequest,
                    "Items"
                );

            Assert.Single(items);

            Assert.Equal(
                "/cmd ping",
                items[0]["PrimaryText"]
            );

            inputSpans =
                ReadDictionaries(
                    companionRequest,
                    "InputSpans"
                );

            Assert.Equal(
                2,
                inputSpans.Length
            );

            AssertSpan(
                inputSpans[0],
                0,
                4,
                "Valid"
            );

            AssertSpan(
                inputSpans[1],
                5,
                1,
                "Valid"
            );

            interaction(
                "Complete"
            );

            Assert.NotNull(inputRequest);
            Assert.Equal(
                "registration-1",
                inputRequest["RegistrationId"]
            );
            Assert.Equal(
                "commands",
                inputRequest["RouteId"]
            );
            Assert.Equal(
                "/cmd ping",
                inputRequest["Input"]
            );

            inputChanged(
                "/cmd missing"
            );

            Assert.Equal(
                -1,
                companionRequest["SelectedIndex"]
            );

            Assert.Empty(
                ReadDictionaries(
                    companionRequest,
                    "Items"
                )
            );

            Assert.Equal(
                "Unknown command 'missing'.",
                companionRequest["ErrorText"]
            );

            Assert.Equal(
                "Type /cmd help for command help.",
                companionRequest["Footer"]
            );

            inputSpans =
                ReadDictionaries(
                    companionRequest,
                    "InputSpans"
                );

            Assert.Equal(
                2,
                inputSpans.Length
            );

            AssertSpan(
                inputSpans[0],
                0,
                4,
                "Valid"
            );

            AssertSpan(
                inputSpans[1],
                5,
                7,
                "Error"
            );

            provider.Stop();

            Assert.Equal(1, interactionUnregisterCount);
            Assert.Equal(1, routeUnregisterCount);
            Assert.Equal(1, participantUnregisterCount);
            Assert.True(clearCompanionCount >= 1);

            adapter.Dispose();
            provider.Dispose();
        }

        [Fact]
        public void SynchronizesLateExternalPrefixRoutesWithCompanionAndSubmission()
        {
            var bus =
                new RecordingModMessageBus();

            var registry =
                new CommandRegistry();

            Register(
                registry,
                new CommandDefinition(
                    "help",
                    new string[0],
                    "Lists commands.",
                    "Lists commands.",
                    "help",
                    "CommandAPI",
                    CommandExecutionLocation.Either,
                    0,
                    "CommandAPI.Tests",
                    NoOpHandler
                )
            );

            Action unregisterPreexisting;
            string preexistingRegistrationError;

            Assert.True(
                registry.TryRegister(
                    "/preexisting",
                    new CommandDefinition(
                        "status",
                        new string[0],
                        "Shows pre-existing provider status.",
                        "Shows pre-existing provider status.",
                        "status",
                        "PreExisting",
                        CommandExecutionLocation.Client,
                        0,
                        "CommandAPI.Tests.PreExisting",
                        NoOpHandler
                    ),
                    out unregisterPreexisting,
                    out preexistingRegistrationError
                ),
                preexistingRegistrationError
            );

            var routeSubmissions =
                new Dictionary<
                    string,
                    Action<string>
                >(
                    StringComparer.Ordinal
                );

            var routeInputChanges =
                new Dictionary<
                    string,
                    Action<string>
                >(
                    StringComparer.Ordinal
                );

            var routeIds =
                new Dictionary<
                    string,
                    string
                >(
                    StringComparer.Ordinal
                );

            var interactions =
                new Dictionary<
                    string,
                    Action<string>
                >(
                    StringComparer.Ordinal
                );

            int externalRouteUnregisterCount =
                0;

            int externalInteractionUnregisterCount =
                0;

            IDictionary<string, object> companionRequest =
                null;

            IDictionary<string, object> inputRequest =
                null;

            Func<
                IDictionary<string, object>,
                IDictionary<string, object>
            > registerParticipant =
                delegate(
                    IDictionary<string, object> metadata
                )
                {
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
                    string prefix =
                        (string)metadata["Prefix"];

                    string routeId =
                        (string)metadata["RouteId"];

                    routeSubmissions.Add(
                        prefix,
                        submit
                    );

                    routeInputChanges.Add(
                        prefix,
                        inputChanged
                    );

                    routeIds.Add(
                        prefix,
                        routeId
                    );

                    return delegate
                    {
                        routeSubmissions.Remove(prefix);
                        routeInputChanges.Remove(prefix);
                        routeIds.Remove(prefix);

                        if (
                            string.Equals(
                                prefix,
                                "/rhchat",
                                StringComparison.Ordinal
                            )
                        )
                        {
                            externalRouteUnregisterCount++;
                        }
                    };
                };

            Action<
                IDictionary<string, object>
            > appendTranscript =
                delegate(
                    IDictionary<string, object> entry
                )
                {
                };

            Func<
                IDictionary<string, object>,
                bool
            > setCompanion =
                delegate(
                    IDictionary<string, object> request
                )
                {
                    companionRequest = request;
                    return true;
                };

            Func<
                IDictionary<string, object>,
                bool
            > clearCompanion =
                delegate(
                    IDictionary<string, object> request
                )
                {
                    return true;
                };

            Func<
                IDictionary<string, object>,
                Action<string>,
                Action
            > registerRouteInteraction =
                delegate(
                    IDictionary<string, object> metadata,
                    Action<string> handler
                )
                {
                    string routeId =
                        (string)metadata["RouteId"];

                    interactions.Add(
                        routeId,
                        handler
                    );

                    return delegate
                    {
                        interactions.Remove(routeId);

                        if (
                            routeId.StartsWith(
                                "commands-external-",
                                StringComparison.Ordinal
                            )
                        )
                        {
                            externalInteractionUnregisterCount++;
                        }
                    };
                };

            Func<
                IDictionary<string, object>,
                bool
            > setInput =
                delegate(
                    IDictionary<string, object> request
                )
                {
                    inputRequest = request;
                    return true;
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
                    },
                    {
                        "SetCompanion",
                        setCompanion
                    },
                    {
                        "ClearCompanion",
                        clearCompanion
                    },
                    {
                        "RegisterRouteInteraction",
                        registerRouteInteraction
                    },
                    {
                        "SetInput",
                        setInput
                    }
                };

            var provider =
                new ApiDiscoveryProvider(
                    bus,
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
                            3,
                            0
                        )
                    ),
                    endpoints
                );

            CommandInput observedInput =
                null;

            int submissionCount =
                0;

            var adapter =
                new RichHudChatCommandAdapter(
                    bus,
                    42UL,
                    delegate(
                        ulong senderId,
                        CommandInput input
                    )
                    {
                        observedInput = input;
                        submissionCount++;
                    },
                    registry
                );

            provider.Start();
            adapter.Start();

            Assert.True(adapter.IsConnected);

            Assert.True(
                routeSubmissions.ContainsKey(
                    "/cmd"
                )
            );

            Assert.True(
                routeSubmissions.ContainsKey(
                    "/preexisting"
                )
            );

            Assert.NotNull(
                routeInputChanges["/preexisting"]
            );

            string preexistingRouteId =
                routeIds["/preexisting"];

            Assert.True(
                interactions.ContainsKey(
                    preexistingRouteId
                )
            );

            unregisterPreexisting();

            Assert.False(
                routeSubmissions.ContainsKey(
                    "/preexisting"
                )
            );

            Assert.False(
                interactions.ContainsKey(
                    preexistingRouteId
                )
            );

            Assert.Equal(
                1,
                externalInteractionUnregisterCount
            );

            Action unregisterExternal;
            string registrationError;

            Assert.True(
                registry.TryRegister(
                    "/rhchat",
                    new CommandDefinition(
                        "status",
                        new string[0],
                        "Shows RichHudChatAPI status.",
                        "Shows RichHudChatAPI status.",
                        "status",
                        "RichHudChatAPI",
                        CommandExecutionLocation.Client,
                        0,
                        "MarcoZechner.RichHudChatAPI",
                        NoOpHandler
                    ),
                    out unregisterExternal,
                    out registrationError
                ),
                registrationError
            );

            Assert.True(
                routeSubmissions.ContainsKey(
                    "/rhchat"
                )
            );

            Assert.NotNull(
                routeInputChanges["/rhchat"]
            );

            string externalRouteId =
                routeIds["/rhchat"];

            Assert.True(
                interactions.ContainsKey(
                    externalRouteId
                )
            );

            routeInputChanges["/rhchat"](
                "/rhchat s"
            );

            Assert.NotNull(companionRequest);

            Assert.Equal(
                externalRouteId,
                companionRequest["RouteId"]
            );

            Assert.Equal(
                "/rhchat <command>",
                companionRequest["HeaderText"]
            );

            IDictionary<string, object>[] items =
                ReadDictionaries(
                    companionRequest,
                    "Items"
                );

            Assert.Single(items);

            Assert.Equal(
                "/rhchat status",
                items[0]["PrimaryText"]
            );

            Assert.Equal(
                "/rhchat status",
                items[0]["CompletionText"]
            );

            interactions[externalRouteId](
                "Complete"
            );

            Assert.NotNull(inputRequest);

            Assert.Equal(
                externalRouteId,
                inputRequest["RouteId"]
            );

            Assert.Equal(
                "/rhchat status",
                inputRequest["Input"]
            );

            routeSubmissions["/rhchat"](
                "/rhchat status"
            );

            Assert.Equal(
                1,
                submissionCount
            );

            Assert.NotNull(observedInput);

            Assert.Equal(
                "/rhchat",
                observedInput.Prefix
            );

            Assert.Equal(
                "status",
                observedInput.CommandName
            );

            unregisterExternal();

            Assert.False(
                routeSubmissions.ContainsKey(
                    "/rhchat"
                )
            );

            Assert.False(
                interactions.ContainsKey(
                    externalRouteId
                )
            );

            Assert.Equal(
                1,
                externalRouteUnregisterCount
            );

            Assert.Equal(
                2,
                externalInteractionUnregisterCount
            );

            adapter.Dispose();
            provider.Dispose();
        }

        private static IDictionary<string, object>[]
            ReadDictionaries(
                IDictionary<string, object> values,
                string key
            )
        {
            object[] encoded =
                Assert.IsType<object[]>(
                    values[key]
                );

            var dictionaries =
                new IDictionary<string, object>[
                    encoded.Length
                ];

            for (
                int index = 0;
                index < encoded.Length;
                index++
            )
            {
                dictionaries[index] =
                    Assert.IsAssignableFrom<
                        IDictionary<string, object>
                    >(
                        encoded[index]
                    );
            }

            return dictionaries;
        }

        private static void AssertSpan(
            IDictionary<string, object> span,
            int start,
            int length,
            string style
        )
        {
            Assert.Equal(
                start,
                (int)span["Start"]
            );

            Assert.Equal(
                length,
                (int)span["Length"]
            );

            Assert.Equal(
                style,
                (string)span["Style"]
            );
        }

        private static void Register(
            CommandRegistry registry,
            CommandDefinition definition
        )
        {
            string errorMessage;

            Assert.True(
                registry.TryRegister(
                    CommandInputParser.Prefix,
                    definition,
                    out errorMessage
                ),
                errorMessage
            );
        }

        private static CommandResult NoOpHandler(
            CommandExecutionContext context,
            CommandInput input
        )
        {
            return new CommandResult(
                true,
                "OK",
                "Completed.",
                new string[0],
                CommandSeverity.Success,
                null
            );
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
