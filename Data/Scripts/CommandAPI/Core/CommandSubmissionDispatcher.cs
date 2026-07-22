using System;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandSubmissionDispatcher
    {
        private readonly CommandRegistry _registry;
        private readonly CommandExecutor _executor;

        private readonly Func<
            ulong,
            string,
            CommandExecutionContext
        > _localContextProvider;

        private readonly Action<
            string,
            CommandInput
        > _serverSubmitter;

        private readonly Action<CommandResult>
            _localResultHandler;

        public CommandSubmissionDispatcher(
            CommandRegistry registry,
            CommandExecutor executor,
            Func<
                ulong,
                string,
                CommandExecutionContext
            > localContextProvider,
            Action<
                string,
                CommandInput
            > serverSubmitter,
            Action<CommandResult> localResultHandler
        )
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            if (localContextProvider == null)
            {
                throw new ArgumentNullException(
                    nameof(localContextProvider)
                );
            }

            if (serverSubmitter == null)
            {
                throw new ArgumentNullException(
                    nameof(serverSubmitter)
                );
            }

            if (localResultHandler == null)
            {
                throw new ArgumentNullException(
                    nameof(localResultHandler)
                );
            }

            _registry = registry;
            _executor = executor;
            _localContextProvider = localContextProvider;
            _serverSubmitter = serverSubmitter;
            _localResultHandler = localResultHandler;
        }

        public void Submit(
            ulong senderId,
            string requestId,
            CommandInput input
        )
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException(
                    "A request ID is required.",
                    nameof(requestId)
                );
            }

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            CommandDefinition definition;

            bool isRegistered =
                _registry.TryResolve(
                    input.Prefix,
                    input.CommandName,
                    out definition
                );

            if (
                isRegistered
                && definition.ExecutionLocation
                    == CommandExecutionLocation.Server
            )
            {
                _serverSubmitter(
                    requestId,
                    input
                );

                return;
            }

            CommandExecutionContext context =
                _localContextProvider(
                    senderId,
                    requestId
                );

            if (context == null)
            {
                throw new InvalidOperationException(
                    "The local command execution context "
                    + "could not be created."
                );
            }

            if (context.IsServer)
            {
                throw new InvalidOperationException(
                    "Local command execution requires "
                    + "a client execution context."
                );
            }

            CommandResult result =
                _executor.Execute(
                    context,
                    input
                );

            _localResultHandler(result);
        }
    }
}
