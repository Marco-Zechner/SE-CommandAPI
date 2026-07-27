using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;

namespace Mz.RichHudChatApi
{
    public sealed class RichHudChatApiClient :
        IDisposable
    {
        public const string ProviderApiId =
            "MarcoZechner.RichHudChatAPI";

        public const string RegisterParticipantEndpoint =
            "RegisterParticipant";

        public const string RegisterRouteEndpoint =
            "RegisterRoute";

        public const string AppendTranscriptEndpoint =
            "AppendTranscript";

        public const string SetCompanionEndpoint =
            "SetCompanion";

        public const string ClearCompanionEndpoint =
            "ClearCompanion";

        public const string RegisterRouteInteractionEndpoint =
            "RegisterRouteInteraction";

        public const string SetInputEndpoint =
            "SetInput";

        private readonly string _consumerId;
        private readonly string _consumerDisplayName;
        private readonly SemanticVersion _consumerModVersion;

        private readonly ApiDiscoveryConsumer _consumer;

        private ChatParticipantHandle _participant;

        private Func<
            IDictionary<string, object>,
            IDictionary<string, object>
        > _registerParticipant;

        private Func<
            IDictionary<string, object>,
            Action<string>,
            Action<string>,
            Action
        > _registerRoute;

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
            Action<string>,
            Action
        > _registerRouteInteraction;

        private Func<
            IDictionary<string, object>,
            bool
        > _setInput;

        private bool _isDisposed;
        private Exception _lastError;

        public event Action Connected;

        public event Action Disconnected;

        public event Action<
            ChatParticipantRegistration,
            Exception
        > ParticipantRegistrationFailed;

        public event Action<
            ChatRouteRegistration,
            Exception
        > RouteRegistrationFailed;

        public bool IsStarted =>
            _consumer.IsStarted;

        public bool IsConnected =>
            _registerParticipant != null;

        public SemanticVersion ProviderModVersion
        {
            get;
            private set;
        }

        public SemanticVersion ProviderApiVersion
        {
            get;
            private set;
        }

        public Exception LastError =>
            _lastError ?? _consumer.LastError;

        public RichHudChatApiClient(
            IModMessageBus messageBus,
            string consumerId,
            string consumerDisplayName,
            SemanticVersion consumerModVersion,
            bool isRequired,
            string featureDescription
        )
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException(
                    "A stable consumer mod identifier is required.",
                    nameof(consumerId)
                );

            if (string.IsNullOrWhiteSpace(consumerDisplayName))
                throw new ArgumentException(
                    "A consumer display name is required.",
                    nameof(consumerDisplayName)
                );

            if (consumerModVersion == null)
                throw new ArgumentNullException(
                    nameof(consumerModVersion)
                );

            _consumerId = consumerId.Trim();
            _consumerDisplayName =
                consumerDisplayName.Trim();

            _consumerModVersion =
                consumerModVersion;

            var dependency =
                new ApiDependencyDescriptor(
                    new ApiModIdentity(
                        _consumerId,
                        _consumerDisplayName,
                        _consumerModVersion
                    ),
                    new ApiRequirement(
                        ProviderApiId,
                        new ApiVersionRange(
                            ApiVersionFile
                                .MinimumProviderApiVersion,
                            null
                        )
                    ),
                    isRequired
                        ? ApiDependencyKind.Required
                        : ApiDependencyKind.Optional,
                    featureDescription
                );

            _consumer =
                new ApiDiscoveryConsumer(
                    messageBus,
                    dependency
                );

            _consumer.Connected += OnConnected;
            _consumer.Disconnected += OnDisconnected;
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (IsStarted)
                return;

            _lastError = null;
            _consumer.Start();

            if (!_consumer.IsConnected)
                _consumer.RequestDiscovery();
        }

        public Guid RequestDiscovery()
        {
            ThrowIfDisposed();
            _lastError = null;

            return _consumer.RequestDiscovery();
        }

        public Guid Rediscover()
        {
            ThrowIfDisposed();
            _lastError = null;

            return _consumer.Rediscover();
        }

        public void Stop()
        {
            ThrowIfDisposed();

            ReleaseProviderRegistration();
            ClearConnection();
            _consumer.Stop();
        }

        public ChatParticipantHandle RegisterParticipant(
            ChatParticipantRegistration registration = null
        )
        {
            ThrowIfDisposed();

            if (_participant != null)
            {
                throw new InvalidOperationException(
                    "A RichHudChatAPI client can own one participant registration."
                );
            }

            var handle =
                new ChatParticipantHandle(
                    this,
                    registration
                        ?? new ChatParticipantRegistration()
                );

            _participant = handle;

            if (IsConnected)
            {
                try
                {
                    ActivateParticipant(handle);
                }
                catch
                {
                    _participant = null;
                    handle.MarkDisposed();
                    throw;
                }
            }

            return handle;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            ReleaseProviderRegistration();

            _consumer.Connected -= OnConnected;
            _consumer.Disconnected -= OnDisconnected;
            _consumer.Dispose();

            ChatParticipantHandle participant =
                _participant;

            _participant = null;

            if (participant != null)
                participant.MarkDisposed();

            ClearConnection();
        }

        internal void RemoveParticipant(
            ChatParticipantHandle handle
        )
        {
            if (
                handle == null
                || handle.IsDisposed
                || !object.ReferenceEquals(
                    handle,
                    _participant
                )
            )
            {
                return;
            }

            _participant = null;
            handle.MarkDisposed();
        }

        internal void ActivateRoute(
            ChatParticipantHandle participant,
            ChatRouteHandle route
        )
        {
            if (
                !IsConnected
                || participant == null
                || !participant.IsActive
                || route == null
                || route.IsDisposed
            )
            {
                return;
            }

            var metadata =
                RouteRequest(
                    participant,
                    route.Registration.RouteId
                );

            metadata.Add(
                "Description",
                route.Registration.Description
            );

            metadata.Add(
                "IsDefault",
                route.Registration.IsDefault
            );

            if (!route.Registration.IsDefault)
            {
                metadata.Add(
                    "Prefix",
                    route.Registration.Prefix
                );
            }

            if (
                route.Registration
                    .ActivationPrefix != null
            )
            {
                metadata.Add(
                    "ActivationPrefix",
                    route.Registration
                        .ActivationPrefix
                );
            }

            Action unregisterRoute =
                _registerRoute(
                    metadata,
                    route.SubmitHandler,
                    route.InputChangedHandler
                );

            if (unregisterRoute == null)
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned no route unregister action."
                );
            }

            Action unregisterInteraction =
                null;

            try
            {
                if (route.InteractionHandler != null)
                {
                    unregisterInteraction =
                        _registerRouteInteraction(
                            RouteRequest(
                                participant,
                                route.Registration.RouteId
                            ),
                            route.InteractionHandler
                        );

                    if (unregisterInteraction == null)
                    {
                        throw new InvalidOperationException(
                            "RichHudChatAPI returned no interaction unregister action."
                        );
                    }
                }
            }
            catch
            {
                TryInvoke(unregisterInteraction);
                TryInvoke(unregisterRoute);
                throw;
            }

            route.ProviderUnregisterRoute =
                unregisterRoute;

            route.ProviderUnregisterInteraction =
                unregisterInteraction;

            route.LastError = null;
        }

        internal bool AppendTranscript(
            ChatParticipantHandle participant,
            string author,
            string message
        )
        {
            if (
                participant == null
                || !participant.IsActive
                || _appendTranscript == null
            )
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "A transcript message is required.",
                    nameof(message)
                );
            }

            var request =
                ParticipantRequest(participant);

            if (author != null)
                request.Add("Author", author);

            request.Add("Message", message);
            _appendTranscript(request);

            return true;
        }

        internal bool SetCompanion(
            ChatParticipantHandle participant,
            ChatRouteHandle route,
            ChatCompanionState state
        )
        {
            if (
                participant == null
                || !participant.IsActive
                || route == null
                || !route.IsActive
                || _setCompanion == null
            )
            {
                return false;
            }

            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var request =
                RouteRequest(
                    participant,
                    route.Registration.RouteId
                );

            request.Add(
                "SelectedIndex",
                state.SelectedIndex
            );

            if (state.HeaderText != null)
                request.Add("HeaderText", state.HeaderText);

            AddSpans(
                request,
                "HeaderSpans",
                state.HeaderSpans
            );

            if (state.Footer != null)
                request.Add("Footer", state.Footer);

            if (state.ErrorText != null)
                request.Add("ErrorText", state.ErrorText);

            AddSpans(
                request,
                "InputSpans",
                state.InputSpans
            );

            ChatCompanionItem[] items =
                state.Items;

            var encodedItems =
                new object[items.Length];

            for (
                int index = 0;
                index < items.Length;
                index++
            )
            {
                encodedItems[index] =
                    EncodeItem(items[index]);
            }

            request.Add("Items", encodedItems);

            return _setCompanion(request);
        }

        internal bool ClearCompanion(
            ChatParticipantHandle participant,
            ChatRouteHandle route
        )
        {
            if (
                participant == null
                || !participant.IsActive
                || route == null
                || !route.IsActive
                || _clearCompanion == null
            )
            {
                return false;
            }

            return _clearCompanion(
                RouteRequest(
                    participant,
                    route.Registration.RouteId
                )
            );
        }

        internal bool SetInput(
            ChatParticipantHandle participant,
            ChatRouteHandle route,
            string input
        )
        {
            if (
                participant == null
                || !participant.IsActive
                || route == null
                || !route.IsActive
                || _setInput == null
            )
            {
                return false;
            }

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            var request =
                RouteRequest(
                    participant,
                    route.Registration.RouteId
                );

            request.Add("Input", input);

            return _setInput(request);
        }

        private void OnConnected(
            ApiConnectedEventArgs eventArgs
        )
        {
            try
            {
                ApiConnection connection =
                    eventArgs.Connection;

                _registerParticipant =
                    GetRequiredEndpoint<
                        Func<
                            IDictionary<string, object>,
                            IDictionary<string, object>
                        >
                    >(
                        connection,
                        RegisterParticipantEndpoint
                    );

                _registerRoute =
                    GetRequiredEndpoint<
                        Func<
                            IDictionary<string, object>,
                            Action<string>,
                            Action<string>,
                            Action
                        >
                    >(
                        connection,
                        RegisterRouteEndpoint
                    );

                _appendTranscript =
                    GetRequiredEndpoint<
                        Action<
                            IDictionary<string, object>
                        >
                    >(
                        connection,
                        AppendTranscriptEndpoint
                    );

                _setCompanion =
                    GetRequiredEndpoint<
                        Func<
                            IDictionary<string, object>,
                            bool
                        >
                    >(
                        connection,
                        SetCompanionEndpoint
                    );

                _clearCompanion =
                    GetRequiredEndpoint<
                        Func<
                            IDictionary<string, object>,
                            bool
                        >
                    >(
                        connection,
                        ClearCompanionEndpoint
                    );

                _registerRouteInteraction =
                    GetRequiredEndpoint<
                        Func<
                            IDictionary<string, object>,
                            Action<string>,
                            Action
                        >
                    >(
                        connection,
                        RegisterRouteInteractionEndpoint
                    );

                _setInput =
                    GetRequiredEndpoint<
                        Func<
                            IDictionary<string, object>,
                            bool
                        >
                    >(
                        connection,
                        SetInputEndpoint
                    );

                ProviderModVersion =
                    connection.Provider.Version;

                ProviderApiVersion =
                    connection.Descriptor.Version;

                _lastError = null;

                ActivatePendingParticipant();
                RaiseConnected();
            }
            catch (Exception exception)
            {
                _lastError = exception;
                ReleaseProviderRegistration();
                ClearConnection();
                _consumer.Disconnect();
            }
        }

        private void OnDisconnected(
            ApiDisconnectedEventArgs eventArgs
        )
        {
            ReleaseProviderRegistration();
            ClearConnection();
            RaiseDisconnected();
        }

        private void ActivatePendingParticipant()
        {
            ChatParticipantHandle participant =
                _participant;

            if (
                participant == null
                || participant.IsDisposed
                || participant.IsActive
            )
            {
                return;
            }

            try
            {
                ActivateParticipant(participant);
            }
            catch (Exception exception)
            {
                participant.LastError =
                    exception;

                _lastError =
                    exception;

                RaiseParticipantRegistrationFailed(
                    participant.Registration,
                    exception
                );
            }
        }

        private void ActivateParticipant(
            ChatParticipantHandle participant
        )
        {
            if (!IsConnected)
                return;

            IDictionary<string, object> response =
                _registerParticipant(
                    new Dictionary<string, object>(
                        StringComparer.Ordinal
                    )
                    {
                        { "OwnerId", _consumerId },
                        {
                            "DisplayName",
                            participant.Registration.DisplayName
                                ?? _consumerDisplayName
                        },
                        {
                            "Version",
                            _consumerModVersion.ToString()
                        },
                        {
                            "ReplacesVanillaChat",
                            participant.Registration
                                .ReplacesVanillaChat
                        }
                    }
                );

            participant.ProviderRegistrationId =
                ReadRegistrationId(response);

            participant.ProviderUnregister =
                ReadUnregister(response);

            participant.LastError = null;

            ChatRouteHandle[] routes =
                participant.GetRoutes();

            for (
                int index = 0;
                index < routes.Length;
                index++
            )
            {
                ChatRouteHandle route =
                    routes[index];

                if (
                    route.IsDisposed
                    || route.IsActive
                )
                {
                    continue;
                }

                try
                {
                    ActivateRoute(
                        participant,
                        route
                    );
                }
                catch (Exception exception)
                {
                    route.LastError =
                        exception;

                    _lastError =
                        exception;

                    RaiseRouteRegistrationFailed(
                        route.Registration,
                        exception
                    );
                }
            }
        }

        private void ReleaseProviderRegistration()
        {
            ChatParticipantHandle participant =
                _participant;

            if (participant != null)
                participant.ReleaseProviderRegistration();
        }

        private void ClearConnection()
        {
            _registerParticipant = null;
            _registerRoute = null;
            _appendTranscript = null;
            _setCompanion = null;
            _clearCompanion = null;
            _registerRouteInteraction = null;
            _setInput = null;
            ProviderModVersion = null;
            ProviderApiVersion = null;
        }

        private static TDelegate GetRequiredEndpoint<TDelegate>(
            ApiConnection connection,
            string endpointName
        )
            where TDelegate : class
        {
            TDelegate endpoint;

            if (
                !connection.TryGetEndpoint(
                    endpointName,
                    out endpoint
                )
            )
            {
                throw new InvalidOperationException(
                    "The RichHudChatAPI provider is missing the exact '"
                        + endpointName
                        + "' endpoint."
                );
            }

            return endpoint;
        }

        private static Dictionary<string, object>
            ParticipantRequest(
                ChatParticipantHandle participant
            )
        {
            return new Dictionary<string, object>(
                StringComparer.Ordinal
            )
            {
                {
                    "RegistrationId",
                    participant.ProviderRegistrationId
                }
            };
        }

        private static Dictionary<string, object>
            RouteRequest(
                ChatParticipantHandle participant,
                string routeId
            )
        {
            var request =
                ParticipantRequest(participant);

            request.Add(
                "RouteId",
                routeId
            );

            return request;
        }

        private static IDictionary<string, object>
            EncodeItem(
                ChatCompanionItem item
            )
        {
            var encoded =
                new Dictionary<string, object>(
                    StringComparer.Ordinal
                )
                {
                    {
                        "PrimaryText",
                        item.PrimaryText
                    }
                };

            if (item.Key != null)
                encoded.Add("Key", item.Key);

            if (item.SecondaryText != null)
            {
                encoded.Add(
                    "SecondaryText",
                    item.SecondaryText
                );
            }

            if (item.CompletionText != null)
            {
                encoded.Add(
                    "CompletionText",
                    item.CompletionText
                );
            }

            AddSpans(
                encoded,
                "PrimarySpans",
                item.PrimarySpans
            );

            return encoded;
        }

        private static void AddSpans(
            IDictionary<string, object> target,
            string key,
            ChatTextSpan[] spans
        )
        {
            if (spans == null || spans.Length == 0)
                return;

            var encoded =
                new object[spans.Length];

            for (
                int index = 0;
                index < spans.Length;
                index++
            )
            {
                ChatTextSpan span =
                    spans[index];

                encoded[index] =
                    new Dictionary<string, object>(
                        StringComparer.Ordinal
                    )
                    {
                        { "Start", span.Start },
                        { "Length", span.Length },
                        {
                            "Style",
                            span.Style.ToString()
                        }
                    };
            }

            target.Add(key, encoded);
        }

        private static string ReadRegistrationId(
            IDictionary<string, object> response
        )
        {
            object value;

            if (
                response == null
                || !response.TryGetValue(
                    "RegistrationId",
                    out value
                )
            )
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned no participant registration identifier."
                );
            }

            string registrationId =
                value as string;

            if (string.IsNullOrWhiteSpace(registrationId))
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned an invalid participant registration identifier."
                );
            }

            return registrationId.Trim();
        }

        private static Action ReadUnregister(
            IDictionary<string, object> response
        )
        {
            object value;

            if (
                response == null
                || !response.TryGetValue(
                    "Unregister",
                    out value
                )
            )
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned no participant unregister action."
                );
            }

            Action unregister =
                value as Action;

            if (unregister == null)
            {
                throw new InvalidOperationException(
                    "RichHudChatAPI returned an invalid participant unregister action."
                );
            }

            return unregister;
        }

        private void RaiseConnected()
        {
            RaiseEvent(Connected);
        }

        private void RaiseDisconnected()
        {
            RaiseEvent(Disconnected);
        }

        private void RaiseEvent(
            Action handler
        )
        {
            if (handler == null)
                return;

            foreach (
                Action subscriber
                in handler.GetInvocationList()
            )
            {
                try
                {
                    subscriber();
                }
                catch (Exception exception)
                {
                    if (_lastError == null)
                        _lastError = exception;
                }
            }
        }

        private void RaiseParticipantRegistrationFailed(
            ChatParticipantRegistration registration,
            Exception exception
        )
        {
            Action<
                ChatParticipantRegistration,
                Exception
            > handler =
                ParticipantRegistrationFailed;

            if (handler == null)
                return;

            foreach (
                Action<
                    ChatParticipantRegistration,
                    Exception
                > subscriber
                in handler.GetInvocationList()
            )
            {
                try
                {
                    subscriber(
                        registration,
                        exception
                    );
                }
                catch (Exception subscriberException)
                {
                    if (_lastError == null)
                        _lastError =
                            subscriberException;
                }
            }
        }

        private void RaiseRouteRegistrationFailed(
            ChatRouteRegistration registration,
            Exception exception
        )
        {
            Action<
                ChatRouteRegistration,
                Exception
            > handler =
                RouteRegistrationFailed;

            if (handler == null)
                return;

            foreach (
                Action<
                    ChatRouteRegistration,
                    Exception
                > subscriber
                in handler.GetInvocationList()
            )
            {
                try
                {
                    subscriber(
                        registration,
                        exception
                    );
                }
                catch (Exception subscriberException)
                {
                    if (_lastError == null)
                        _lastError =
                            subscriberException;
                }
            }
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
            catch
            {
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new InvalidOperationException(
                    "The RichHudChatAPI client has been disposed."
                );
            }
        }
    }
}