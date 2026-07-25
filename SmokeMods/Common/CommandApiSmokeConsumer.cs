using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;
using VRage.Utils;

namespace MarcoZechner.CommandApi.Smoke
{
    public sealed class CommandApiSmokeConsumer :
        IDisposable
    {
        private const long DiscoveryChannelId =
            ApiProtocolChannels.Discovery;

        private const string CommandApiId =
            "MarcoZechner.CommandAPI";

        private const string RegisterCommandEndpoint =
            "RegisterCommand";

        private const string Prefix =
            "/smoke";

        private const string PreferredCommandName =
            "smoke";

        private readonly string _ownerId;
        private readonly string _displayName;
        private readonly string _qualifiedCommandName;
        private readonly string _label;

        private ApiDiscoveryConsumer _consumer;
        private Action _qualifiedUnregister;
        private Action _preferredUnregister;
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

            if (_consumer != null)
                return;

            var dependency =
                new ApiDependencyDescriptor(
                    new ApiModIdentity(
                        _ownerId,
                        _displayName,
                        new SemanticVersion(
                            1,
                            0,
                            0
                        )
                    ),
                    new ApiRequirement(
                        CommandApiId,
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
                    ApiDependencyKind.Required,
                    "Registers CommandAPI collision smoke-test commands."
                );

            _consumer =
                new ApiDiscoveryConsumer(
                    new SpaceEngineersModMessageBus(),
                    dependency
                );

            _consumer.Connected += OnConnected;
            _consumer.Disconnected += OnDisconnected;

            _consumer.Start();
            _consumer.RequestDiscovery();

            Log(
                "waiting for "
                + CommandApiId
                + " 1.x."
            );
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ReleaseRegistrations();

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
            Func<
                IDictionary<string, object>,
                Func<
                    IDictionary<string, object>,
                    IDictionary<string, object>
                >,
                Action
            > registerCommand;

            if (
                !eventArgs.Connection.TryGetEndpoint(
                    RegisterCommandEndpoint,
                    out registerCommand
                )
            )
            {
                Log(
                    "provider is missing the exact "
                    + RegisterCommandEndpoint
                    + " endpoint."
                );

                return;
            }

            try
            {
                _qualifiedUnregister =
                    registerCommand(
                        CreateMetadata(
                            _qualifiedCommandName,
                            "Server",
                            "Deterministic server command for "
                                + _displayName
                                + "."
                        ),
                        HandleCommand
                    );

                Log(
                    "registered " + Prefix + " "
                    + _qualifiedCommandName
                    + "."
                );
            }
            catch (Exception exception)
            {
                Log(
                    "qualified registration failed: "
                    + exception.Message
                );

                return;
            }

            try
            {
                _preferredUnregister =
                    registerCommand(
                        CreateMetadata(
                            PreferredCommandName,
                            "Client",
                            "Load-order winner for the shared local smoke command."
                        ),
                        HandleCommand
                    );

                Log(
                    "won the shared local " + Prefix + " "
                    + PreferredCommandName
                    + " name."
                );
            }
            catch (InvalidOperationException exception)
            {
                Log(
                    "shared " + Prefix + " "
                    + PreferredCommandName
                    + " is already owned; use " + Prefix + " "
                    + _qualifiedCommandName
                    + ". Provider said: "
                    + exception.Message
                );
            }
        }

        private void OnDisconnected(
            ApiDisconnectedEventArgs eventArgs
        )
        {
            ReleaseRegistrations();

            Log(
                "CommandAPI disconnected: "
                + eventArgs.Reason
                + "."
            );
        }

        private IDictionary<string, object>
            CreateMetadata(
                string canonicalName,
                string executionLocation,
                string description
            )
        {
            return new Dictionary<string, object>(
                StringComparer.Ordinal
            )
            {
                {
                    "OwnerId",
                    _ownerId
                },
                {
                    "Prefix",
                    Prefix
                },
                {
                    "ExecutionLocation",
                    executionLocation
                },
                {
                    "CanonicalName",
                    canonicalName
                },
                {
                    "ShortDescription",
                    description
                },
                {
                    "HelpText",
                    description
                        + " It reports which smoke mod handled "
                        + "the request."
                },
                {
                    "Usage",
                    canonicalName
                },
                {
                    "Category",
                    "CommandAPI smoke tests"
                },
                {
                    "PermissionRequirement",
                    0
                }
            };
        }

        private IDictionary<string, object>
            HandleCommand(
                IDictionary<string, object> request
            )
        {
            string commandName =
                ReadString(
                    request,
                    "CommandName",
                    "(unknown)"
                );

            string requester =
                ReadString(
                    request,
                    "RequesterDisplayName",
                    "(unknown)"
                );

            return new Dictionary<string, object>(
                StringComparer.Ordinal
            )
            {
                {
                    "IsSuccess",
                    true
                },
                {
                    "Title",
                    _label + " smoke command"
                },
                {
                    "Summary",
                    "Handled by "
                        + _displayName
                        + " through '"
                        + commandName
                        + "'."
                },
                {
                    "DetailLines",
                    new[]
                    {
                        "OwnerId: " + _ownerId,
                        "Qualified command: "
                            + _qualifiedCommandName,
                        "Requester: " + requester
                    }
                },
                {
                    "Severity",
                    "Success"
                },
                {
                    "UsageHint",
                    null
                }
            };
        }

        private void ReleaseRegistrations()
        {
            Action preferred =
                _preferredUnregister;

            _preferredUnregister = null;

            if (preferred != null)
                preferred();

            Action qualified =
                _qualifiedUnregister;

            _qualifiedUnregister = null;

            if (qualified != null)
                qualified();
        }

        private static string ReadString(
            IDictionary<string, object> values,
            string key,
            string fallback
        )
        {
            object value;

            if (
                values != null
                && values.TryGetValue(
                    key,
                    out value
                )
            )
            {
                string text = value as string;

                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return fallback;
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
