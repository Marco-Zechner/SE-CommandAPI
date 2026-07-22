using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;

namespace MarcoZechner.CommandApi.Chat
{
    public sealed class RichHudChatCommandAdapter :
        IDisposable
    {
        private const long DiscoveryChannelId =
            6098967432095689633L;

        private const string RichHudChatApiId =
            "MarcoZechner.RichHudChatAPI";

        private const string RegisterParticipantEndpoint =
            "RegisterParticipant";

        private const string RegisterRouteEndpoint =
            "RegisterRoute";

        private const string AppendTranscriptEndpoint =
            "AppendTranscript";

        private const string SetCompanionEndpoint =
            "SetCompanion";

        private const string ClearCompanionEndpoint =
            "ClearCompanion";

        private const string RegisterRouteInteractionEndpoint =
            "RegisterRouteInteraction";

        private const string SetInputEndpoint =
            "SetInput";

        private const string OwnerId =
            "MarcoZechner.CommandAPI";

        private const string DisplayName =
            "CommandAPI";

        private const string RouteId =
            "commands";

        private const int MaximumDetailLines =
            8;

        private readonly IModMessageBus _messageBus;
        private readonly ulong _localPeerId;

        private readonly VanillaChatCommandSubmitter
            _submitter;

        private readonly CommandRegistry _registry;

        private ApiDiscoveryConsumer _consumer;

        private Action _unregisterParticipant;
        private Action _unregisterRoute;
        private Action _unregisterRouteInteraction;

        private Action<
            IDictionary<string, object>
        > _appendTranscript;

        private Func<
            IDictionary<string, object>,
            bool
        > _setCompanion;

        private Func<
            IDictionary<string, object>,
            bool
        > _clearCompanion;

        private Func<
            IDictionary<string, object>,
            bool
        > _setInput;

        private CommandDefinition[] _suggestions =
            new CommandDefinition[0];

        private int _selectedSuggestionIndex;

        private string _registrationId;
        private bool _disposed;

        public bool IsConnected
        {
            get
            {
                return _registrationId != null
                    && _unregisterRoute != null
                    && _appendTranscript != null;
            }
        }

        public string LastError
        {
            get;
            private set;
        }

        public RichHudChatCommandAdapter(
            IModMessageBus messageBus,
            ulong localPeerId,
            VanillaChatCommandSubmitter submitter
        )
            : this(
                messageBus,
                localPeerId,
                submitter,
                null
            )
        {
        }

        public RichHudChatCommandAdapter(
            IModMessageBus messageBus,
            ulong localPeerId,
            VanillaChatCommandSubmitter submitter,
            CommandRegistry registry
        )
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (submitter == null)
                throw new ArgumentNullException(nameof(submitter));

            _messageBus = messageBus;
            _localPeerId = localPeerId;
            _submitter = submitter;
            _registry = registry;
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (_consumer != null)
                return;

            var dependency =
                new ApiDependencyDescriptor(
                    new ApiModIdentity(
                        OwnerId,
                        DisplayName,
                        new SemanticVersion(
                            0,
                            2,
                            0
                        )
                    ),
                    new ApiRequirement(
                        RichHudChatApiId,
                        new ApiVersionRange(
                            new SemanticVersion(
                                1,
                                0,
                                0
                            ),
                            new SemanticVersion(
                                2,
                                0,
                                0
                            )
                        )
                    ),
                    ApiDependencyKind.Optional,
                    "Uses RichHudChatAPI for command input and result presentation."
                );

            var consumer =
                new ApiDiscoveryConsumer(
                    _messageBus,
                    DiscoveryChannelId,
                    dependency
                );

            consumer.Connected += OnConnected;
            consumer.Disconnected += OnDisconnected;

            _consumer = consumer;

            try
            {
                consumer.Start();
                consumer.RequestDiscovery();
            }
            catch
            {
                _consumer = null;

                consumer.Connected -= OnConnected;
                consumer.Disconnected -= OnDisconnected;
                consumer.Dispose();

                throw;
            }
        }

        public bool PresentResult(
            CommandResult result
        )
        {
            if (_disposed || !IsConnected)
                return false;

            if (result == null)
            {
                WriteLine(
                    "Command failed: "
                        + "The command could not be completed."
                );

                return true;
            }

            WriteLine(
                result.Title
                    + ": "
                    + result.Summary
            );

            string[] detailLines =
                result.DetailLines;

            int detailCount =
                Math.Min(
                    detailLines.Length,
                    MaximumDetailLines
                );

            for (
                int index = 0;
                index < detailCount;
                index++
            )
            {
                WriteLine(
                    detailLines[index]
                        ?? string.Empty
                );
            }

            int remainingDetailCount =
                detailLines.Length - detailCount;

            if (remainingDetailCount > 0)
            {
                WriteLine(
                    "... "
                        + remainingDetailCount
                        + " more lines."
                );
            }

            if (
                !string.IsNullOrWhiteSpace(
                    result.UsageHint
                )
            )
            {
                WriteLine(
                    "Usage: " + result.UsageHint
                );
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ReleaseConnection();

            ApiDiscoveryConsumer consumer =
                _consumer;

            _consumer = null;

            if (consumer != null)
            {
                consumer.Connected -= OnConnected;
                consumer.Disconnected -= OnDisconnected;
                consumer.Dispose();
            }
        }

        private void OnConnected(
            ApiConnectedEventArgs eventArgs
        )
        {
            ReleaseConnection();
            LastError = null;

            try
            {
                Connect(
                    eventArgs.Connection
                );
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                ReleaseConnection();
            }
        }

        private void OnDisconnected(
            ApiDisconnectedEventArgs eventArgs
        )
        {
            ReleaseConnection();
        }

        private void Connect(
            ApiConnection connection
        )
        {
            Func<
                IDictionary<string, object>,
                IDictionary<string, object>
            > registerParticipant;

            Func<
                IDictionary<string, object>,
                Action<string>,
                Action<string>,
                Action
            > registerRoute;

            Action<
                IDictionary<string, object>
            > appendTranscript;

            Func<
                IDictionary<string, object>,
                bool
            > setCompanion;

            Func<
                IDictionary<string, object>,
                bool
            > clearCompanion;

            Func<
                IDictionary<string, object>,
                Action<string>,
                Action
            > registerRouteInteraction;

            Func<
                IDictionary<string, object>,
                bool
            > setInput;

            if (
                !connection.TryGetEndpoint(
                    RegisterParticipantEndpoint,
                    out registerParticipant
                )
            )
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI is missing RegisterParticipant."
                );
            }

            if (
                !connection.TryGetEndpoint(
                    RegisterRouteEndpoint,
                    out registerRoute
                )
            )
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI is missing RegisterRoute."
                );
            }

            if (
                !connection.TryGetEndpoint(
                    AppendTranscriptEndpoint,
                    out appendTranscript
                )
            )
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI is missing AppendTranscript."
                );
            }

            bool hasSetCompanion =
                connection.TryGetEndpoint(
                    SetCompanionEndpoint,
                    out setCompanion
                );

            bool hasClearCompanion =
                connection.TryGetEndpoint(
                    ClearCompanionEndpoint,
                    out clearCompanion
                );

            bool hasRouteInteraction =
                connection.TryGetEndpoint(
                    RegisterRouteInteractionEndpoint,
                    out registerRouteInteraction
                );

            bool hasSetInput =
                connection.TryGetEndpoint(
                    SetInputEndpoint,
                    out setInput
                );

            bool supportsCompanion =
                _registry != null
                && hasSetCompanion
                && hasClearCompanion
                && hasRouteInteraction
                && hasSetInput;

            IDictionary<string, object> registration =
                registerParticipant(
                    new Dictionary<string, object>(
                        StringComparer.Ordinal
                    )
                    {
                        {
                            "OwnerId",
                            OwnerId
                        },
                        {
                            "DisplayName",
                            DisplayName
                        }
                    }
                );

            string registrationId =
                ReadRegistrationId(
                    registration
                );

            Action unregisterParticipant =
                ReadUnregisterAction(
                    registration
                );

            Action unregisterRoute =
                null;

            Action unregisterRouteInteraction =
                null;

            Action<string> inputChanged =
                null;

            if (supportsCompanion)
                inputChanged = OnInputChanged;

            try
            {
                unregisterRoute =
                    registerRoute(
                        new Dictionary<string, object>(
                            StringComparer.Ordinal
                        )
                        {
                            {
                                "RegistrationId",
                                registrationId
                            },
                            {
                                "RouteId",
                                RouteId
                            },
                            {
                                "Prefix",
                                CommandInputParser.Prefix
                            },
                            {
                                "IsDefault",
                                false
                            }
                        },
                        OnSubmittedInput,
                        inputChanged
                    );

                if (unregisterRoute == null)
                {
                    throw new InvalidOperationException(
                        "RichHudChatAPI returned no route registration."
                    );
                }

                if (supportsCompanion)
                {
                    unregisterRouteInteraction =
                        registerRouteInteraction(
                            new Dictionary<string, object>(
                                StringComparer.Ordinal
                            )
                            {
                                {
                                    "RegistrationId",
                                    registrationId
                                },
                                {
                                    "RouteId",
                                    RouteId
                                }
                            },
                            OnInteraction
                        );

                    if (unregisterRouteInteraction == null)
                    {
                        throw new InvalidOperationException(
                            "RichHudChatAPI returned no interaction registration."
                        );
                    }
                }
            }
            catch
            {
                TryInvoke(
                    unregisterRouteInteraction
                );

                TryInvoke(
                    unregisterRoute
                );

                TryInvoke(
                    unregisterParticipant
                );

                throw;
            }

            _registrationId = registrationId;
            _unregisterParticipant =
                unregisterParticipant;

            _unregisterRoute =
                unregisterRoute;

            _unregisterRouteInteraction =
                unregisterRouteInteraction;

            _appendTranscript =
                appendTranscript;

            _setCompanion =
                supportsCompanion
                    ? setCompanion
                    : null;

            _clearCompanion =
                supportsCompanion
                    ? clearCompanion
                    : null;

            _setInput =
                supportsCompanion
                    ? setInput
                    : null;

            WriteLine(
                "Ready. Use /cmd help."
            );
        }

        private void OnInputChanged(
            string text
        )
        {
            if (
                _disposed
                || !IsConnected
                || _registry == null
                || _setCompanion == null
            )
            {
                return;
            }

            string normalizedInput =
                (text ?? string.Empty).Trim();

            if (
                !string.Equals(
                    normalizedInput,
                    CommandInputParser.Prefix,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                ClearSuggestions();
                return;
            }

            CommandDefinition[] suggestions =
                _registry.GetDefinitions(
                    CommandInputParser.Prefix
                );

            Array.Sort(
                suggestions,
                delegate(
                    CommandDefinition left,
                    CommandDefinition right
                )
                {
                    return string.Compare(
                        left.CanonicalName,
                        right.CanonicalName,
                        StringComparison.Ordinal
                    );
                }
            );

            _suggestions = suggestions;
            _selectedSuggestionIndex = 0;

            if (suggestions.Length == 0)
            {
                ClearSuggestions();
                return;
            }

            PublishSuggestions();
        }

        private void OnInteraction(
            string interaction
        )
        {
            if (
                _disposed
                || !IsConnected
                || _suggestions.Length == 0
            )
            {
                return;
            }

            if (
                string.Equals(
                    interaction,
                    "Previous",
                    StringComparison.Ordinal
                )
            )
            {
                _selectedSuggestionIndex =
                    (
                        _selectedSuggestionIndex
                        + _suggestions.Length
                        - 1
                    )
                    % _suggestions.Length;

                PublishSuggestions();
                return;
            }

            if (
                string.Equals(
                    interaction,
                    "Next",
                    StringComparison.Ordinal
                )
            )
            {
                _selectedSuggestionIndex =
                    (
                        _selectedSuggestionIndex
                        + 1
                    )
                    % _suggestions.Length;

                PublishSuggestions();
                return;
            }

            if (
                string.Equals(
                    interaction,
                    "Complete",
                    StringComparison.Ordinal
                )
            )
            {
                CompleteSelectedSuggestion();
            }
        }

        private void PublishSuggestions()
        {
            if (
                _setCompanion == null
                || _registrationId == null
                || _suggestions.Length == 0
            )
            {
                return;
            }

            var items =
                new object[_suggestions.Length];

            for (
                int index = 0;
                index < _suggestions.Length;
                index++
            )
            {
                CommandDefinition definition =
                    _suggestions[index];

                items[index] =
                    new Dictionary<string, object>(
                        StringComparer.Ordinal
                    )
                    {
                        {
                            "PrimaryText",
                            definition.CanonicalName
                        },
                        {
                            "SecondaryText",
                            definition.ShortDescription
                        },
                        {
                            "CompletionText",
                            CommandInputParser.Prefix
                                + " "
                                + definition.CanonicalName
                        }
                    };
            }

            TryRequest(
                _setCompanion,
                new Dictionary<string, object>(
                    StringComparer.Ordinal
                )
                {
                    {
                        "RegistrationId",
                        _registrationId
                    },
                    {
                        "RouteId",
                        RouteId
                    },
                    {
                        "Items",
                        items
                    },
                    {
                        "SelectedIndex",
                        _selectedSuggestionIndex
                    }
                }
            );
        }

        private void CompleteSelectedSuggestion()
        {
            if (
                _setInput == null
                || _registrationId == null
                || _selectedSuggestionIndex < 0
                || _selectedSuggestionIndex >= _suggestions.Length
            )
            {
                return;
            }

            CommandDefinition definition =
                _suggestions[
                    _selectedSuggestionIndex
                ];

            TryRequest(
                _setInput,
                new Dictionary<string, object>(
                    StringComparer.Ordinal
                )
                {
                    {
                        "RegistrationId",
                        _registrationId
                    },
                    {
                        "RouteId",
                        RouteId
                    },
                    {
                        "Input",
                        CommandInputParser.Prefix
                            + " "
                            + definition.CanonicalName
                    }
                }
            );
        }

        private void ClearSuggestions()
        {
            _suggestions =
                new CommandDefinition[0];

            _selectedSuggestionIndex = 0;

            TryClearCompanion(
                _clearCompanion,
                _registrationId
            );
        }

        private void OnSubmittedInput(
            string text
        )
        {
            if (_disposed || !IsConnected)
                return;

            text =
                text ?? string.Empty;

            WriteLine(
                "> " + text
            );

            CommandParseResult parseResult =
                CommandInputParser.Parse(
                    text,
                    CommandInputParser.Prefix
                );

            if (
                parseResult.Status
                    == CommandParseStatus.NotCommand
            )
            {
                WriteLine(
                    "Command error: "
                        + "The submitted input did not match /cmd."
                );

                return;
            }

            if (
                parseResult.Status
                    == CommandParseStatus.Error
            )
            {
                WriteLine(
                    "Command error: "
                        + parseResult.ErrorMessage
                );

                return;
            }

            try
            {
                _submitter(
                    _localPeerId,
                    parseResult.Input
                );
            }
            catch (Exception)
            {
                WriteLine(
                    "Command failed: "
                        + "The command could not be completed."
                );
            }
        }

        private void WriteLine(
            string message
        )
        {
            Action<
                IDictionary<string, object>
            > appendTranscript =
                _appendTranscript;

            string registrationId =
                _registrationId;

            if (
                appendTranscript == null
                || registrationId == null
            )
            {
                return;
            }

            appendTranscript(
                new Dictionary<string, object>(
                    StringComparer.Ordinal
                )
                {
                    {
                        "RegistrationId",
                        registrationId
                    },
                    {
                        "Author",
                        DisplayName
                    },
                    {
                        "Message",
                        message ?? string.Empty
                    }
                }
            );
        }

        private void ReleaseConnection()
        {
            Action unregisterRouteInteraction =
                _unregisterRouteInteraction;

            Action unregisterRoute =
                _unregisterRoute;

            Action unregisterParticipant =
                _unregisterParticipant;

            Func<
                IDictionary<string, object>,
                bool
            > clearCompanion =
                _clearCompanion;

            string registrationId =
                _registrationId;

            TryClearCompanion(
                clearCompanion,
                registrationId
            );

            _registrationId = null;
            _appendTranscript = null;
            _setCompanion = null;
            _clearCompanion = null;
            _setInput = null;
            _unregisterRouteInteraction = null;
            _unregisterRoute = null;
            _unregisterParticipant = null;
            _suggestions =
                new CommandDefinition[0];

            _selectedSuggestionIndex = 0;

            TryInvoke(
                unregisterRouteInteraction
            );

            TryInvoke(
                unregisterRoute
            );

            TryInvoke(
                unregisterParticipant
            );
        }

        private static void TryClearCompanion(
            Func<
                IDictionary<string, object>,
                bool
            > clearCompanion,
            string registrationId
        )
        {
            if (
                clearCompanion == null
                || registrationId == null
            )
            {
                return;
            }

            TryRequest(
                clearCompanion,
                new Dictionary<string, object>(
                    StringComparer.Ordinal
                )
                {
                    {
                        "RegistrationId",
                        registrationId
                    },
                    {
                        "RouteId",
                        RouteId
                    }
                }
            );
        }

        private static bool TryRequest(
            Func<
                IDictionary<string, object>,
                bool
            > endpoint,
            IDictionary<string, object> request
        )
        {
            if (endpoint == null)
                return false;

            try
            {
                return endpoint(request);
            }
            catch
            {
                return false;
            }
        }

        private static string ReadRegistrationId(
            IDictionary<string, object> registration
        )
        {
            if (registration == null)
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned no participant registration."
                );
            }

            object value;

            if (
                !registration.TryGetValue(
                    "RegistrationId",
                    out value
                )
            )
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned no registration identifier."
                );
            }

            string registrationId =
                value as string;

            if (string.IsNullOrWhiteSpace(registrationId))
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned an invalid registration identifier."
                );
            }

            return registrationId;
        }

        private static Action ReadUnregisterAction(
            IDictionary<string, object> registration
        )
        {
            object value;

            if (
                !registration.TryGetValue(
                    "Unregister",
                    out value
                )
            )
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned no participant cleanup action."
                );
            }

            Action unregister =
                value as Action;

            if (unregister == null)
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned an invalid participant cleanup action."
                );
            }

            return unregister;
        }

        private static void TryInvoke(
            Action action
        )
        {
            if (action == null)
                return;

            try
            {
                action();
            }
            catch (Exception)
            {
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new InvalidOperationException(
                    "The RichHud chat adapter has been disposed."
                );
            }
        }
    }
}