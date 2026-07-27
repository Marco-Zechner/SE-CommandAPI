using System;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.CommandApi;
using Mz.SemanticVersioning;
using VRage.Utils;

namespace MarcoZechner.CommandApi.Smoke
{
    public sealed class CommandApiSmokeConsumer :
        IDisposable
    {
        private const string Prefix =
            "/smoke";

        private const string PreferredCommandName =
            "smoke";

        private readonly string _ownerId;
        private readonly string _displayName;
        private readonly string _qualifiedCommandName;
        private readonly string _label;

        private CommandApiClient _client;

        private CommandRegistrationHandle
            _qualifiedRegistration;

        private CommandRegistrationHandle
            _preferredRegistration;

        private bool _disposed;

        public CommandApiSmokeConsumer(
            string ownerId,
            string displayName,
            string qualifiedCommandName,
            string label
        )
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                throw new ArgumentException(
                    "An owner identifier is required.",
                    nameof(ownerId)
                );

            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException(
                    "A display name is required.",
                    nameof(displayName)
                );

            if (string.IsNullOrWhiteSpace(qualifiedCommandName))
                throw new ArgumentException(
                    "A qualified command name is required.",
                    nameof(qualifiedCommandName)
                );

            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException(
                    "A label is required.",
                    nameof(label)
                );

            _ownerId = ownerId.Trim();
            _displayName = displayName.Trim();

            _qualifiedCommandName =
                qualifiedCommandName.Trim();

            _label = label.Trim();
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (_client != null)
                return;

            var client =
                new CommandApiClient(
                    new SpaceEngineersModMessageBus(),
                    _ownerId,
                    _displayName,
                    new SemanticVersion(
                        1,
                        0,
                        0
                    ),
                    true,
                    "Registers CommandAPI collision smoke-test commands."
                );

            client.Connected +=
                OnConnected;

            client.Disconnected +=
                OnDisconnected;

            client.RegistrationFailed +=
                OnRegistrationFailed;

            _client = client;

            _qualifiedRegistration =
                client.Register(
                    CreateRegistration(
                        _qualifiedCommandName,
                        CommandExecutionLocation.Server,
                        "Deterministic server command for "
                            + _displayName
                            + "."
                    ),
                    HandleCommand
                );

            _preferredRegistration =
                client.Register(
                    CreateRegistration(
                        PreferredCommandName,
                        CommandExecutionLocation.Client,
                        "Load-order winner for the shared local smoke command."
                    ),
                    HandleCommand
                );

            Log(
                "waiting for CommandAPI API "
                + ApiVersionFile.MinimumProviderApiVersion
                + " or newer."
            );

            client.Start();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            CommandApiClient client =
                _client;

            _client = null;
            _qualifiedRegistration = null;
            _preferredRegistration = null;

            if (client == null)
                return;

            client.Connected -=
                OnConnected;

            client.Disconnected -=
                OnDisconnected;

            client.RegistrationFailed -=
                OnRegistrationFailed;

            client.Dispose();
        }

        private CommandRegistration
            CreateRegistration(
                string canonicalName,
                CommandExecutionLocation executionLocation,
                string description
            )
        {
            return new CommandRegistration(
                Prefix,
                canonicalName,
                executionLocation,
                new string[0],
                description,
                description
                    + " It reports which smoke mod handled "
                    + "the request.",
                canonicalName,
                "CommandAPI smoke tests",
                0
            );
        }

        private CommandResponse HandleCommand(
            CommandRequest request
        )
        {
            return new CommandResponse(
                true,
                _label + " smoke command",
                "Handled by "
                    + _displayName
                    + " through '"
                    + request.CommandName
                    + "'.",
                new[]
                {
                    "OwnerId: " + _ownerId,
                    "Qualified command: "
                        + _qualifiedCommandName,
                    "Requester: "
                        + request.RequesterDisplayName
                },
                CommandSeverity.Success,
                null
            );
        }

        private void OnConnected()
        {
            CommandApiClient client =
                _client;

            if (client == null)
                return;

            Log(
                "connected to CommandAPI mod "
                + client.ProviderModVersion
                + ", API "
                + client.ProviderApiVersion
                + "."
            );

            CommandRegistrationHandle qualified =
                _qualifiedRegistration;

            if (
                qualified != null
                && qualified.IsActive
            )
            {
                Log(
                    "registered "
                    + Prefix
                    + " "
                    + _qualifiedCommandName
                    + "."
                );
            }

            CommandRegistrationHandle preferred =
                _preferredRegistration;

            if (
                preferred != null
                && preferred.IsActive
            )
            {
                Log(
                    "won the shared local "
                    + Prefix
                    + " "
                    + PreferredCommandName
                    + " name."
                );
            }
        }

        private void OnDisconnected()
        {
            Log("CommandAPI disconnected.");
        }

        private void OnRegistrationFailed(
            CommandRegistration registration,
            Exception exception
        )
        {
            if (
                registration != null
                && string.Equals(
                    registration.CanonicalName,
                    PreferredCommandName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                Log(
                    "shared "
                    + Prefix
                    + " "
                    + PreferredCommandName
                    + " is already owned; use "
                    + Prefix
                    + " "
                    + _qualifiedCommandName
                    + ". Provider said: "
                    + exception.Message
                );

                return;
            }

            Log(
                "registration failed for "
                + (
                    registration == null
                        ? "(unknown)"
                        : registration.CanonicalName
                )
                + ": "
                + exception.Message
            );
        }

        private void Log(string message)
        {
            MyLog.Default.WriteLineAndConsole(
                "["
                + _displayName
                + "] "
                + message
            );
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new InvalidOperationException(
                    "The smoke consumer has been disposed."
                );
            }
        }
    }
}
