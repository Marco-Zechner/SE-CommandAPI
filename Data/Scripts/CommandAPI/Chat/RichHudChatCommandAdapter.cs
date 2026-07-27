using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.RichHudChatApi;
using Mz.SemanticVersioning;

namespace MarcoZechner.CommandApi.Chat
{
    public sealed class RichHudChatCommandAdapter :
        IDisposable
    {
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

        private RichHudChatApiClient _client;
        private ChatParticipantHandle _participant;
        private ChatRouteHandle _route;

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

        private bool _disposed;

        public bool IsConnected
        {
            get
            {
                return _client != null
                    && _client.IsConnected
                    && _participant != null
                    && _participant.IsActive
                    && _route != null
                    && _route.IsActive;
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

            if (_client != null)
                return;

            var client =
                new RichHudChatApiClient(
                    _messageBus,
                    OwnerId,
                    DisplayName,
                    new SemanticVersion(
                        ModVersionFile.Major,
                        ModVersionFile.Minor,
                        ModVersionFile.Patch
                    ),
                    false,
                    "Uses RichHudChatAPI for command input and result presentation."
                );

            client.Connected += OnConnected;
            client.Disconnected += OnDisconnected;
            client.ParticipantRegistrationFailed +=
                OnParticipantRegistrationFailed;

            client.RouteRegistrationFailed +=
                OnRouteRegistrationFailed;

            ChatParticipantHandle participant =
                null;

            ChatRouteHandle route =
                null;

            try
            {
                participant =
                    client.RegisterParticipant(
                        new ChatParticipantRegistration(
                            DisplayName
                        )
                    );

                Action<string> inputChanged =
                    _registry == null
                        ? null
                        : (Action<string>)OnInputChanged;

                Action<string> interaction =
                    _registry == null
                        ? null
                        : (Action<string>)OnInteraction;

                route =
                    participant.RegisterRoute(
                        new ChatRouteRegistration(
                            RouteId,
                            CommandInputParser.Prefix,
                            "/",
                            "CommandAPI commands."
                        ),
                        OnSubmittedInput,
                        inputChanged,
                        interaction
                    );

                _client = client;
                _participant = participant;
                _route = route;

                client.Start();
            }
            catch
            {
                _route = null;
                _participant = null;
                _client = null;

                client.Connected -= OnConnected;
                client.Disconnected -= OnDisconnected;
                client.ParticipantRegistrationFailed -=
                    OnParticipantRegistrationFailed;

                client.RouteRegistrationFailed -=
                    OnRouteRegistrationFailed;

                client.Dispose();
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

            RichHudChatApiClient client =
                _client;

            _client = null;
            _participant = null;
            _route = null;
            _externalRoutes.Clear();

            ResetDraftState();

            if (client != null)
            {
                client.Connected -= OnConnected;
                client.Disconnected -= OnDisconnected;
                client.ParticipantRegistrationFailed -=
                    OnParticipantRegistrationFailed;

                client.RouteRegistrationFailed -=
                    OnRouteRegistrationFailed;

                client.Dispose();
            }
        }

        private void OnConnected()
        {
            LastError = ReadFacadeError();

            if (!IsConnected)
                return;

            try
            {
                SynchronizeExternalRoutes();
                WriteLine(
                    "Ready. Use /cmd help."
                );

                LastError = null;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
            }
        }

        private void OnDisconnected()
        {
            ResetDraftState();
            LastError = ReadFacadeError();
        }

        private void OnParticipantRegistrationFailed(
            ChatParticipantRegistration registration,
            Exception exception
        )
        {
            LastError =
                exception == null
                    ? "RichHudChatAPI participant registration failed."
                    : exception.Message;
        }

        private void OnRouteRegistrationFailed(
            ChatRouteRegistration registration,
            Exception exception
        )
        {
            LastError =
                exception == null
                    ? "RichHudChatAPI route registration failed."
                    : exception.Message;
        }

        private string ReadFacadeError()
        {
            Exception exception =
                _route == null
                    ? null
                    : _route.LastError;

            if (
                exception == null
                && _participant != null
            )
            {
                exception =
                    _participant.LastError;
            }

            if (
                exception == null
                && _client != null
            )
            {
                exception =
                    _client.LastError;
            }

            return exception == null
                ? null
                : exception.Message;
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
                || _participant == null
                || _participant.IsDisposed
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
                    registration.Route
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
                    delegate(string text)
                    {
                        OnInputChanged(
                            capturedPrefix,
                            capturedRouteId,
                            text
                        );
                    };

                Action<string> interaction =
                    delegate(string value)
                    {
                        OnInteraction(
                            capturedPrefix,
                            capturedRouteId,
                            value
                        );
                    };

                ChatRouteHandle route =
                    _participant.RegisterRoute(
                        new ChatRouteRegistration(
                            capturedRouteId,
                            capturedPrefix,
                            null,
                            capturedPrefix
                                + " commands."
                        ),
                        delegate(string text)
                        {
                            OnSubmittedInput(
                                capturedPrefix,
                                text
                            );
                        },
                        inputChanged,
                        interaction
                    );

                _externalRoutes.Add(
                    capturedPrefix,
                    new ExternalRouteRegistration(
                        route
                    )
                );
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
                || FindRoute(routeId) == null
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
                string.IsNullOrWhiteSpace(
                    _draftPrefix
                )
                || string.IsNullOrWhiteSpace(
                    _draftRouteId
                )
            )
            {
                return;
            }

            ChatRouteHandle route =
                FindRoute(
                    _draftRouteId
                );

            if (route == null)
                return;

            var items =
                new ChatCompanionItem[
                    _suggestions.Length
                ];

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

                items[index] =
                    new ChatCompanionItem(
                        completionText,
                        definition.ShortDescription,
                        completionText,
                        null,
                        new[]
                        {
                            new ChatTextSpan(
                                0,
                                _draftPrefix.Length,
                                ChatTextStyle.Valid
                            )
                        }
                    );
            }

            string footer =
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
                            + ".";

            TrySetCompanion(
                route,
                new ChatCompanionState(
                    items,
                    _selectedSuggestionIndex,
                    _draftPrefix
                        + " <command>",
                    new[]
                    {
                        new ChatTextSpan(
                            0,
                            _draftPrefix.Length,
                            ChatTextStyle.Valid
                        ),
                        new ChatTextSpan(
                            _draftPrefix.Length,
                            10,
                            ChatTextStyle.Muted
                        )
                    },
                    footer,
                    _draftErrorText,
                    BuildInputSpans()
                )
            );
        }

        private ChatTextSpan[] BuildInputSpans()
        {
            var spans =
                new List<ChatTextSpan>();

            if (_draftPrefixLength > 0)
            {
                spans.Add(
                    new ChatTextSpan(
                        0,
                        _draftPrefixLength,
                        ChatTextStyle.Valid
                    )
                );
            }

            if (_draftCommandLength > 0)
            {
                spans.Add(
                    new ChatTextSpan(
                        _draftCommandStart,
                        _draftCommandLength,
                        _suggestions.Length > 0
                            ? ChatTextStyle.Valid
                            : ChatTextStyle.Error
                    )
                );
            }

            return spans.ToArray();
        }

        private void CompleteSelectedSuggestion()
        {
            if (
                string.IsNullOrWhiteSpace(
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

            ChatRouteHandle route =
                FindRoute(
                    _draftRouteId
                );

            if (route == null)
                return;

            CommandDefinition definition =
                _suggestions[
                    _selectedSuggestionIndex
                ];

            TrySetInput(
                route,
                _draftPrefix
                    + " "
                    + definition.CanonicalName
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
                FindRoute(routeId)
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
            ChatParticipantHandle participant =
                _participant;

            if (
                participant == null
                || !participant.IsActive
                || string.IsNullOrWhiteSpace(message)
            )
            {
                return;
            }

            try
            {
                participant.AppendTranscript(
                    message,
                    DisplayName
                );
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
            }
        }

        private ChatRouteHandle FindRoute(
            string routeId
        )
        {
            if (string.IsNullOrWhiteSpace(routeId))
                return null;

            ChatRouteHandle route =
                _route;

            if (
                route != null
                && !route.IsDisposed
                && string.Equals(
                    route.Registration.RouteId,
                    routeId,
                    StringComparison.Ordinal
                )
            )
            {
                return route;
            }

            foreach (
                ExternalRouteRegistration registration
                in _externalRoutes.Values
            )
            {
                route =
                    registration.Route;

                if (
                    route != null
                    && !route.IsDisposed
                    && string.Equals(
                        route.Registration.RouteId,
                        routeId,
                        StringComparison.Ordinal
                    )
                )
                {
                    return route;
                }
            }

            return null;
        }

        private void TrySetCompanion(
            ChatRouteHandle route,
            ChatCompanionState state
        )
        {
            if (route == null || route.IsDisposed)
                return;

            try
            {
                route.SetCompanion(state);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
            }
        }

        private void TryClearCompanion(
            ChatRouteHandle route
        )
        {
            if (route == null || route.IsDisposed)
                return;

            try
            {
                route.ClearCompanion();
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
            }
        }

        private void TrySetInput(
            ChatRouteHandle route,
            string input
        )
        {
            if (route == null || route.IsDisposed)
                return;

            try
            {
                route.SetInput(input);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
            }
        }

        private void ResetDraftState()
        {
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
        }

        private sealed class ExternalRouteRegistration
        {
            public ChatRouteHandle Route
            {
                get;
                private set;
            }

            public ExternalRouteRegistration(
                ChatRouteHandle route
            )
            {
                if (route == null)
                    throw new ArgumentNullException(nameof(route));

                Route = route;
            }

            public void Release()
            {
                ChatRouteHandle route =
                    Route;

                Route = null;

                if (route == null)
                    return;

                try
                {
                    route.Dispose();
                }
                catch (Exception)
                {
                }
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
