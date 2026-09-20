using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;

namespace MarcoZechner.CommandApi.Api
{
    public sealed class CommandApiProvider :
        IDisposable
    {
        public const long DiscoveryChannelId =
            ApiProtocolChannels.Discovery;

        public const string ApiId =
            "MarcoZechner.CommandAPI";

        public const string RegisterCommandEndpoint =
            "RegisterCommand";

        private readonly ApiDiscoveryProvider _provider;
        private readonly CommandRegistrationService _registrationService;

        public bool IsStarted
        {
            get
            {
                return _provider.IsStarted;
            }
        }

        public int ExternalProviderCount
        {
            get { return _registrationService.ExternalProviderCount; }
        }

        public CommandApiProvider(IModMessageBus messageBus, CommandRegistry registry)
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            _registrationService = new CommandRegistrationService(registry);

            Func<
                IDictionary<string, object>,
                Func<
                    IDictionary<string, object>,
                    IDictionary<string, object>
                >,
                Action
            > registerCommand =
                _registrationService.RegisterCommand;

            var endpoints =
                new Dictionary<string, Delegate>(
                    StringComparer.Ordinal
                )
                {
                    {
                        RegisterCommandEndpoint,
                        registerCommand
                    }
                };

            _provider =
                new ApiDiscoveryProvider(
                    messageBus,
                    new ApiModIdentity(
                        ApiId,
                        "CommandAPI",
                        new SemanticVersion(
                            ModVersionFile.Major,
                            ModVersionFile.Minor,
                            ModVersionFile.Patch
                        )
                    ),
                    new ApiDescriptor(
                        ApiId,
                        new SemanticVersion(
                            ApiVersionFile.Major,
                            ApiVersionFile.Minor,
                            ApiVersionFile.Patch
                        )
                    ),
                    endpoints
                );
        }

        public void Start()
        {
            _provider.Start();
        }

        public void Announce()
        {
            _provider.Announce();
        }

        public void Stop()
        {
            _provider.Stop();
        }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
