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
            ApiProtocolChannels.Discovery;

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

        private Func<
            IDictionary<string, object>,
            Action<string>,
            Action<string>,
            Action
        > _registerRoute;

        private Func<
            IDictionary<string, object>,
            Action<string>,
            Action
        > _registerRouteInteraction;

        private readonly Dictionary<
            string,
            ExternalRouteRegistration
        > _externalRoutes =
            new Dictionary<
                string,
                ExternalRouteRegistration
            >(
                StringComparer.Ordinal
            );

        private int _nextExternalRouteId =
            1;

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

        private string _draftPrefix =
            CommandInputParser.Prefix;

        private string _draftRouteId =
            RouteId;

        private int _draftPrefixLength;
        private int _draftCommandStart;
        private int _draftCommandLength;

        private string _draftErrorText;

        private bool _supportsStyledCompanion;

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

            if (_registry != null)
                _registry.Changed += OnRegistryChanged;
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
                            ModVersionFile.Major,
                            ModVersionFile.Minor,
                            ModVersionFile.Patch
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

            if (_registry != null)
                _registry.Changed -= OnRegistryChanged;

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

            bool supportsActivationPrefix =
                supportsCompanion
                && connection.Descriptor.Version
                    >= new SemanticVersion(
                        1,
                        2,
                        0
                    );

            bool supportsStyledCompanion =
                supportsCompanion
                && connection.Descriptor.Version
                    >= new SemanticVersion(
                        1,
                        3,
                        0
                    );

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
                var routeMetadata =
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
                    };

                if (supportsActivationPrefix)
                {
                    routeMetadata.Add(
                        "ActivationPrefix",
                        "/"
                    );
                }

                unregisterRoute =
                    registerRoute(
                        routeMetadata,
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

            _registerRoute =
                registerRoute;

            _registerRouteInteraction =
                supportsCompanion
                    ? registerRouteInteraction
                    : null;

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

            _supportsStyledCompanion =
                supportsStyledCompanion;

            SynchronizeExternalRoutes();

            WriteLine(
                "Ready. Use /cmd help."
            );
        }

        private void OnRegistryChanged()
        {
            if (_disposed || !IsConnected)
                return;

            try
            {
                SynchronizeExternalRoutes();
                LastError = null;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
            }
        }

        private void SynchronizeExternalRoutes()
        {
            if (
                _registry == null
                || _registerRoute == null
                || _registrationId == null
            )
            {
                return;
            }

            string[] prefixes =
                _registry.GetPrefixes();

            var desiredPrefixes =
                new Dictionary<string, bool>(
                    StringComparer.Ordinal
                );

            for (
                int index = 0;
                index < prefixes.Length;
                index++
            )
            {
                string prefix =
                    prefixes[index];

                if (
                    string.Equals(
                        prefix,
                        CommandInputParser.Prefix,
                        StringComparison.Ordinal
                    )
                )
                {
                    continue;
                }

                desiredPrefixes[prefix] =
                    true;
            }

            string[] registeredPrefixes =
                new string[
                    _externalRoutes.Count
                ];

            _externalRoutes.Keys.CopyTo(
                registeredPrefixes,
                0
            );

            for (
                int index = 0;
                index < registeredPrefixes.Length;
                index++
            )
            {
                string prefix =
                    registeredPrefixes[index];

                if (desiredPrefixes.ContainsKey(prefix))
                    continue;

                ExternalRouteRegistration registration =
                    _externalRoutes[prefix];

                _externalRoutes.Remove(prefix);

                TryClearCompanion(
                    _clearCompanion,
                    _registrationId,
                    registration.RouteId
                );

                registration.Release();
            }

            foreach (
                string prefix
                in desiredPrefixes.Keys
            )
            {
                if (_externalRoutes.ContainsKey(prefix))
                    continue;

                string routeId =
                    RouteId
                    + "-external-"
                    + _nextExternalRouteId;

                _nextExternalRouteId++;

                string capturedPrefix =
                    prefix;

                string capturedRouteId =
                    routeId;

                Action<string> inputChanged =
                    null;

                if (_setCompanion != null)
                {
                    inputChanged =
                        delegate(string text)
                        {
                            OnInputChanged(
                                capturedPrefix,
                                capturedRouteId,
                                text
                            );
                        };
                }

                Action unregisterRoute =
                    _registerRoute(
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
                                capturedRouteId
                            },
                            {
                                "Prefix",
                                capturedPrefix
                            },
                            {
                                "IsDefault",
                                false
                            }
                        },
                        delegate(string text)
                        {
                            OnSubmittedInput(
                                capturedPrefix,
                                text
                            );
                        },
                        inputChanged
                    );

                if (unregisterRoute == null)
                {
                    throw new InvalidOperationException(
                        "RichHudChatAPI returned no route registration "
                            + "for prefix "
                            + capturedPrefix
                            + "."
                    );
                }

                Action unregisterInteraction =
                    null;

                try
                {
                    if (_registerRouteInteraction != null)
                    {
                        unregisterInteraction =
                            _registerRouteInteraction(
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
                                        capturedRouteId
                                    }
                                },
                                delegate(string interaction)
                                {
                                    OnInteraction(
                                        capturedPrefix,
                                        capturedRouteId,
                                        interaction
                                    );
                                }
                            );

                        if (unregisterInteraction == null)
                        {
                            throw new InvalidOperationException(
                                "RichHudChatAPI returned no interaction "
                                    + "registration for prefix "
                                    + capturedPrefix
                                    + "."
                            );
                        }
                    }

                    _externalRoutes.Add(
                        capturedPrefix,
                        new ExternalRouteRegistration(
                            capturedRouteId,
                            unregisterRoute,
                            unregisterInteraction
                        )
                    );
                }
                catch
                {
                    TryInvoke(unregisterInteraction);
                    TryInvoke(unregisterRoute);
                    throw;
                }
            }
        }

        private void OnInputChanged(
            string text
        )
        {
            OnInputChanged(
                CommandInputParser.Prefix,
                RouteId,
                text
            );
        }

        private void OnInputChanged(
            string prefix,
            string routeId,
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

            string input =
                text
                ?? string.Empty;

            int prefixLength;
            int commandStart;
            int commandLength;

            if (
                !TryReadCommandDraft(
                    input,
                    prefix,
                    out prefixLength,
                    out commandStart,
                    out commandLength
                )
            )
            {
                ClearSuggestions(
                    routeId
                );

                return;
            }

            string commandFragment =
                commandLength == 0
                    ? string.Empty
                    : input.Substring(
                        commandStart,
                        commandLength
                    );

            CommandDefinition[] definitions =
                _registry.GetDefinitions(
                    prefix
                );

            var matches =
                new List<CommandDefinition>();

            for (
                int index = 0;
                index < definitions.Length;
                index++
            )
            {
                CommandDefinition definition =
                    definitions[index];

                if (
                    MatchesCommandFragment(
                        definition,
                        commandFragment
                    )
                )
                {
                    matches.Add(definition);
                }
            }

            CommandDefinition[] suggestions =
                matches.ToArray();

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

            _draftPrefix =
                prefix;

            _draftRouteId =
                routeId;

            _draftPrefixLength =
                prefixLength;

            _draftCommandStart =
                commandStart;

            _draftCommandLength =
                commandLength;

            _suggestions =
                suggestions;

            if (suggestions.Length > 0)
            {
                _selectedSuggestionIndex = 0;
                _draftErrorText = null;
            }
            else
            {
                _selectedSuggestionIndex = -1;

                _draftErrorText =
                    commandLength > 0
                        ? "Unknown command '"
                            + commandFragment
                            + "'."
                        : null;
            }

            if (
                suggestions.Length == 0
                && !_supportsStyledCompanion
            )
            {
                ClearSuggestions(
                    routeId
                );

                return;
            }

            PublishSuggestions();
        }

        private static bool TryReadCommandDraft(
            string input,
            string prefix,
            out int prefixLength,
            out int commandStart,
            out int commandLength
        )
        {
            prefixLength = 0;
            commandStart = 0;
            commandLength = 0;

            if (
                string.IsNullOrEmpty(input)
                || string.IsNullOrWhiteSpace(prefix)
            )
            {
                return false;
            }

            if (
                input.Length <= prefix.Length
                && prefix.StartsWith(
                    input,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                prefixLength = input.Length;
                return true;
            }

            if (
                input.Length < prefix.Length
                || string.Compare(
                    input,
                    0,
                    prefix,
                    0,
                    prefix.Length,
                    StringComparison.OrdinalIgnoreCase
                ) != 0
            )
            {
                return false;
            }

            prefixLength =
                prefix.Length;

            if (input.Length == prefix.Length)
                return true;

            if (!char.IsWhiteSpace(input[prefix.Length]))
                return false;

            commandStart =
                prefix.Length;

            while (
                commandStart < input.Length
                && char.IsWhiteSpace(
                    input[commandStart]
                )
            )
            {
                commandStart++;
            }

            if (commandStart >= input.Length)
                return true;

            int commandEnd =
                commandStart;

            while (
                commandEnd < input.Length
                && !char.IsWhiteSpace(
                    input[commandEnd]
                )
            )
            {
                commandEnd++;
            }

            commandLength =
                commandEnd - commandStart;

            return true;
        }

        private static bool MatchesCommandFragment(
            CommandDefinition definition,
            string fragment
        )
        {
            if (string.IsNullOrEmpty(fragment))
                return true;

            if (
                definition.CanonicalName.StartsWith(
                    fragment,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return true;
            }

            string[] aliases =
                definition.Aliases;

            for (
                int index = 0;
                index < aliases.Length;
                index++
            )
            {
                if (
                    aliases[index].StartsWith(
                        fragment,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        private void OnInteraction(
            string interaction
        )
        {
            OnInteraction(
                CommandInputParser.Prefix,
                RouteId,
                interaction
            );
        }

        private void OnInteraction(
            string prefix,
            string routeId,
            string interaction
        )
        {
            if (
                _disposed
                || !IsConnected
                || _suggestions.Length == 0
                || !string.Equals(
                    prefix,
                    _draftPrefix,
                    StringComparison.Ordinal
                )
                || !string.Equals(
                    routeId,
                    _draftRouteId,
                    StringComparison.Ordinal
                )
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
                || string.IsNullOrWhiteSpace(
                    _draftPrefix
                )
                || string.IsNullOrWhiteSpace(
                    _draftRouteId
                )
            )
            {
                return;
            }

            if (
                _suggestions.Length == 0
                && !_supportsStyledCompanion
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

                string completionText =
                    _draftPrefix
                        + " "
                        + definition.CanonicalName;

                var item =
                    new Dictionary<string, object>(
                        StringComparer.Ordinal
                    )
                    {
                        {
                            "PrimaryText",
                            _supportsStyledCompanion
                                ? completionText
                                : definition.CanonicalName
                        },
                        {
                            "SecondaryText",
                            definition.ShortDescription
                        },
                        {
                            "CompletionText",
                            completionText
                        }
                    };

                if (_supportsStyledCompanion)
                {
                    item.Add(
                        "PrimarySpans",
                        new object[]
                        {
                            CreateSpan(
                                0,
                                _draftPrefix.Length,
                                "Valid"
                            )
                        }
                    );
                }

                items[index] = item;
            }

            var request =
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
                        _draftRouteId
                    },
                    {
                        "Items",
                        items
                    },
                    {
                        "SelectedIndex",
                        _selectedSuggestionIndex
                    }
                };

            if (_supportsStyledCompanion)
            {
                request.Add(
                    "HeaderText",
                    _draftPrefix
                        + " <command>"
                );

                request.Add(
                    "HeaderSpans",
                    new object[]
                    {
                        CreateSpan(
                            0,
                            _draftPrefix.Length,
                            "Valid"
                        ),
                        CreateSpan(
                            _draftPrefix.Length,
                            10,
                            "Muted"
                        )
                    }
                );

                request.Add(
                    "Footer",
                    _suggestions.Length > 0
                        ? "Up/Down selects | Tab completes"
                        : string.Equals(
                            _draftPrefix,
                            CommandInputParser.Prefix,
                            StringComparison.Ordinal
                        )
                            ? "Type /cmd help for command help."
                            : "No matching commands under "
                                + _draftPrefix
                                + "."
                );

                request.Add(
                    "InputSpans",
                    BuildInputSpans()
                );

                if (
                    !string.IsNullOrWhiteSpace(
                        _draftErrorText
                    )
                )
                {
                    request.Add(
                        "ErrorText",
                        _draftErrorText
                    );
                }
            }

            TryRequest(
                _setCompanion,
                request
            );
        }

        private object[] BuildInputSpans()
        {
            var spans =
                new List<object>();

            if (_draftPrefixLength > 0)
            {
                spans.Add(
                    CreateSpan(
                        0,
                        _draftPrefixLength,
                        "Valid"
                    )
                );
            }

            if (_draftCommandLength > 0)
            {
                spans.Add(
                    CreateSpan(
                        _draftCommandStart,
                        _draftCommandLength,
                        _suggestions.Length > 0
                            ? "Valid"
                            : "Error"
                    )
                );
            }

            return spans.ToArray();
        }

        private static IDictionary<string, object>
            CreateSpan(
                int start,
                int length,
                string style
            )
        {
            return new Dictionary<string, object>(
                StringComparer.Ordinal
            )
            {
                {
                    "Start",
                    start
                },
                {
                    "Length",
                    length
                },
                {
                    "Style",
                    style
                }
            };
        }

        private void CompleteSelectedSuggestion()
        {
            if (
                _setInput == null
                || _registrationId == null
                || string.IsNullOrWhiteSpace(
                    _draftPrefix
                )
                || string.IsNullOrWhiteSpace(
                    _draftRouteId
                )
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
                        _draftRouteId
                    },
                    {
                        "Input",
                        _draftPrefix
                            + " "
                            + definition.CanonicalName
                    }
                }
            );
        }

        private void ClearSuggestions()
        {
            ClearSuggestions(
                _draftRouteId
            );
        }

        private void ClearSuggestions(
            string routeId
        )
        {
            _suggestions =
                new CommandDefinition[0];

            _selectedSuggestionIndex = -1;
            _draftPrefixLength = 0;
            _draftCommandStart = 0;
            _draftCommandLength = 0;
            _draftErrorText = null;

            TryClearCompanion(
                _clearCompanion,
                _registrationId,
                routeId
            );
        }

        private void OnSubmittedInput(
            string text
        )
        {
            OnSubmittedInput(
                CommandInputParser.Prefix,
                text
            );
        }

        private void OnSubmittedInput(
            string prefix,
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
                    prefix
                );

            if (
                parseResult.Status
                    == CommandParseStatus.NotCommand
            )
            {
                WriteLine(
                    "Command error: "
                        + "The submitted input did not match "
                        + prefix
                        + "."
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

            ExternalRouteRegistration[] externalRoutes =
                new ExternalRouteRegistration[
                    _externalRoutes.Count
                ];

            _externalRoutes.Values.CopyTo(
                externalRoutes,
                0
            );

            Func<
                IDictionary<string, object>,
                bool
            > clearCompanion =
                _clearCompanion;

            string registrationId =
                _registrationId;

            TryClearCompanion(
                clearCompanion,
                registrationId,
                _draftRouteId
            );

            _registrationId = null;
            _appendTranscript = null;
            _setCompanion = null;
            _clearCompanion = null;
            _setInput = null;
            _registerRoute = null;
            _registerRouteInteraction = null;
            _unregisterRouteInteraction = null;
            _unregisterRoute = null;
            _unregisterParticipant = null;

            _externalRoutes.Clear();

            _suggestions =
                new CommandDefinition[0];

            _selectedSuggestionIndex = -1;

            _draftPrefix =
                CommandInputParser.Prefix;

            _draftRouteId =
                RouteId;

            _draftPrefixLength = 0;
            _draftCommandStart = 0;
            _draftCommandLength = 0;
            _draftErrorText = null;
            _supportsStyledCompanion = false;

            for (
                int index = 0;
                index < externalRoutes.Length;
                index++
            )
            {
                externalRoutes[index].Release();
            }

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
            string registrationId,
            string routeId
        )
        {
            if (
                clearCompanion == null
                || registrationId == null
                || string.IsNullOrWhiteSpace(routeId)
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
                        routeId
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

        private sealed class ExternalRouteRegistration
        {
            public string RouteId
            {
                get;
                private set;
            }

            private Action UnregisterRoute
            {
                get;
                set;
            }

            private Action UnregisterInteraction
            {
                get;
                set;
            }

            public ExternalRouteRegistration(
                string routeId,
                Action unregisterRoute,
                Action unregisterInteraction
            )
            {
                RouteId =
                    routeId;

                UnregisterRoute =
                    unregisterRoute;

                UnregisterInteraction =
                    unregisterInteraction;
            }

            public void Release()
            {
                Action unregisterInteraction =
                    UnregisterInteraction;

                Action unregisterRoute =
                    UnregisterRoute;

                UnregisterInteraction = null;
                UnregisterRoute = null;

                TryInvoke(unregisterInteraction);
                TryInvoke(unregisterRoute);
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
