using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Mz.Networking;

namespace MarcoZechner.CommandApi.Networking
{
    public sealed class CommandNetworkCoordinator :
        IDisposable
    {
        public const string RequestMessageType =
            "CommandAPI.CommandRequest.v2";

        public const string ResultMessageType =
            "CommandAPI.CommandResult.v2";

        private readonly NetworkEndpoint _endpoint;
        private readonly CommandExecutor _executor;
        private readonly bool _isServer;
        private readonly ulong _localPeerId;

        private readonly Func<
            ulong,
            string,
            CommandExecutionContext
        > _contextProvider;

        private readonly Action<
            CommandResultMessage
        > _resultHandler;

        private readonly HashSet<string>
            _pendingRequestIds;

        private NetworkMessageSubscription
            _requestSubscription;

        private NetworkMessageSubscription
            _resultSubscription;

        private bool _disposed;

        public CommandNetworkCoordinator(
            NetworkEndpoint endpoint,
            CommandExecutor executor,
            bool isServer,
            ulong localPeerId,
            Func<
                ulong,
                string,
                CommandExecutionContext
            > contextProvider,
            Action<CommandResultMessage> resultHandler
        )
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            if (contextProvider == null)
            {
                throw new ArgumentNullException(
                    nameof(contextProvider)
                );
            }

            if (resultHandler == null)
            {
                throw new ArgumentNullException(
                    nameof(resultHandler)
                );
            }

            _endpoint = endpoint;
            _executor = executor;
            _isServer = isServer;
            _localPeerId = localPeerId;
            _contextProvider = contextProvider;
            _resultHandler = resultHandler;

            _pendingRequestIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );

            _requestSubscription =
                _endpoint.RegisterHandler(
                    RequestMessageType,
                    OnRequestReceived
                );

            try
            {
                _resultSubscription =
                    _endpoint.RegisterHandler(
                        ResultMessageType,
                        OnResultReceived
                    );
            }
            catch
            {
                _requestSubscription.Dispose();
                _requestSubscription = null;
                throw;
            }
        }

        public void SendRequest(
            string requestId,
            CommandInput input
        )
        {
            EnsureNotDisposed();

            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException(
                    "A request ID is required.",
                    nameof(requestId)
                );
            }

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (!_pendingRequestIds.Add(requestId))
            {
                throw new InvalidOperationException(
                    "A command request with ID '"
                    + requestId
                    + "' is already pending."
                );
            }

            try
            {
                var request =
                    new CommandRequestMessage(
                        requestId,
                        input.Prefix,
                        input.CommandName,
                        input.Arguments
                    );

                _endpoint.SendToServer(
                    RequestMessageType,
                    CommandMessageCodec
                        .SerializeRequest(request)
                );
            }
            catch
            {
                _pendingRequestIds.Remove(requestId);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_resultSubscription != null)
            {
                _resultSubscription.Dispose();
                _resultSubscription = null;
            }

            if (_requestSubscription != null)
            {
                _requestSubscription.Dispose();
                _requestSubscription = null;
            }

            _pendingRequestIds.Clear();
        }

        private void OnRequestReceived(
            NetworkReceiveContext receiveContext
        )
        {
            if (_disposed)
                return;

            if (!_isServer)
            {
                throw new InvalidOperationException(
                    "Command requests can only be processed "
                    + "by the authoritative server."
                );
            }

            CommandRequestMessage request =
                CommandMessageCodec.DeserializeRequest(
                    receiveContext.Envelope.Payload
                );

            ulong requesterPeerId =
                receiveContext
                    .Envelope
                    .OriginalSenderId;

            CommandExecutionContext executionContext =
                _contextProvider(
                    requesterPeerId,
                    request.RequestId
                );

            if (executionContext == null)
            {
                throw new InvalidOperationException(
                    "The command execution context "
                    + "could not be created."
                );
            }

            CommandResult result =
                _executor.Execute(
                    executionContext,
                    new CommandInput(
                        request.Prefix,
                        request.CommandName,
                        request.Arguments
                    )
                );

            var resultMessage =
                new CommandResultMessage(
                    request.RequestId,
                    result.IsSuccess,
                    result.Title,
                    result.Summary,
                    result.DetailLines,
                    result.Severity,
                    result.UsageHint
                );

            if (requesterPeerId == _localPeerId)
            {
                CompleteResult(resultMessage);
                return;
            }

            _endpoint.SendToPlayer(
                ResultMessageType,
                CommandMessageCodec
                    .SerializeResult(resultMessage),
                requesterPeerId
            );
        }

        private void OnResultReceived(
            NetworkReceiveContext receiveContext
        )
        {
            if (_disposed)
                return;

            if (!receiveContext.TransportSenderIsServer)
            {
                throw new InvalidOperationException(
                    "Command results are accepted only "
                    + "from the authoritative server."
                );
            }

            CommandResultMessage result =
                CommandMessageCodec.DeserializeResult(
                    receiveContext.Envelope.Payload
                );

            CompleteResult(result);
        }

        private void CompleteResult(
            CommandResultMessage result
        )
        {
            if (
                !_pendingRequestIds.Remove(
                    result.RequestId
                )
            )
            {
                return;
            }

            _resultHandler(result);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new InvalidOperationException(
                    "The command network coordinator has been disposed."
                );
            }
        }
    }
}
