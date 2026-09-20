using System;
using MarcoZechner.CommandApi.Api;
using MarcoZechner.CommandApi.Chat;
using MarcoZechner.CommandApi.Core;
using MarcoZechner.CommandApi.Networking;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.Networking.SpaceEngineers;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;

namespace MarcoZechner.CommandApi
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class CommandApiSession :
        MySessionComponentBase
    {
        private const string ModDisplayName =
            "CommandAPI";

        private const string ProtocolVersion =
            "2.0.0";

        // Low 16 bits of FNV-1a for
        // "MarcoZechner.CommandAPI.Network.v1".
        private const ushort NetworkChannelId =
            31280;

        private SpaceEngineersNetworkSession
            _networkSession;

        private CommandNetworkCoordinator
            _networkCoordinator;

        private CommandSubmissionDispatcher
            _submissionDispatcher;

        private CommandApiProvider
            _apiProvider;

        private SpaceEngineersVanillaChatInput
            _chatInput;

        private VanillaChatCommandAdapter
            _chatAdapter;

        private string _networkState =
            "Not initialized";

        private string _presentationAdapter =
            "None";

        private bool _initialized;

        public override void BeforeStart()
        {
            base.BeforeStart();

            if (_initialized)
                return;

            if (
                MyAPIGateway.Utilities == null
                || MyAPIGateway.Multiplayer == null
            )
            {
                return;
            }

            try
            {
                Initialize();
            }
            catch (Exception exception)
            {
                DisposeRuntime();

                MyLog.Default.WriteLineAndConsole(
                    ModDisplayName
                        + " initialization failed: "
                        + exception
                );

                if (
                    MyAPIGateway.Utilities != null
                    && !MyAPIGateway.Utilities.IsDedicated
                )
                {
                    MyAPIGateway.Utilities.ShowMessage(
                        ModDisplayName,
                        "Initialization failed. See SpaceEngineers.log."
                    );
                }
            }
        }

        protected override void UnloadData()
        {
            DisposeRuntime();
            base.UnloadData();
        }

        private void Initialize()
        {
            var registry =
                new CommandRegistry();

            string errorMessage;

            if (
                !CommandBuiltIns.TryRegister(
                    registry,
                    BuildStatusSnapshot,
                    out errorMessage
                )
            )
            {
                throw new InvalidOperationException(
                    errorMessage
                );
            }

            var executor =
                new CommandExecutor(registry);

            _networkSession =
                new SpaceEngineersNetworkSession(
                    NetworkChannelId,
                    OnNetworkReceiveFailure
                );

            bool isServer =
                _networkSession.Transport.IsServer;

            ulong localPeerId =
                _networkSession.Transport.LocalPeerId;

            _networkState =
                (
                    isServer
                        ? "Server"
                        : "Client"
                )
                + " transport active on channel "
                + NetworkChannelId;

            _networkCoordinator =
                new CommandNetworkCoordinator(
                    _networkSession.Endpoint,
                    executor,
                    isServer,
                    localPeerId,
                    CreateExecutionContext,
                    PresentNetworkResult
                );

            _apiProvider =
                new CommandApiProvider(
                    new SpaceEngineersModMessageBus(),
                    registry
                );

            _apiProvider.Start();

            bool isDedicated =
                MyAPIGateway.Utilities.IsDedicated;

            if (isDedicated)
            {
                _presentationAdapter =
                    "Headless server";

                _initialized = true;

                MyLog.Default.WriteLineAndConsole(
                    ModDisplayName
                        + " ready as authoritative server on channel "
                        + NetworkChannelId
                        + "."
                );

                return;
            }

            _submissionDispatcher =
                new CommandSubmissionDispatcher(
                    registry,
                    executor,
                    CreateLocalExecutionContext,
                    SubmitServerCommand,
                    PresentLocalResult
                );

            var input =
                new SpaceEngineersVanillaChatInput();

            _chatInput = input;

            var output =
                new SpaceEngineersVanillaChatOutput();

            _chatAdapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    registry.GetPrefixes,
                    SubmitCommand
                );

            _presentationAdapter = "Vanilla chat";
            _initialized = true;
            output.WriteLine(ModDisplayName, "Ready. Use /cmd help.");
        }

        private void SubmitCommand(
            ulong senderId,
            CommandInput input
        )
        {
            CommandSubmissionDispatcher dispatcher =
                _submissionDispatcher;

            if (dispatcher == null)
            {
                throw new InvalidOperationException(
                    "Command submission is unavailable."
                );
            }

            dispatcher.Submit(
                senderId,
                Guid.NewGuid().ToString("N"),
                input
            );
        }

        private void SubmitServerCommand(
            string requestId,
            CommandInput input
        )
        {
            CommandNetworkCoordinator coordinator =
                _networkCoordinator;

            if (coordinator == null)
            {
                throw new InvalidOperationException(
                    "Command networking is unavailable."
                );
            }

            coordinator.SendRequest(
                requestId,
                input
            );
        }

        private static CommandExecutionContext
            CreateLocalExecutionContext(
                ulong senderId,
                string requestId
            )
        {
            return SpaceEngineersExecutionContextProvider
                .CreateLocal(
                    senderId,
                    requestId
                );
        }

        private static CommandExecutionContext
            CreateExecutionContext(
                ulong senderId,
                string requestId
            )
        {
            return SpaceEngineersExecutionContextProvider
                .Create(
                    senderId,
                    requestId
                );
        }

        private void PresentLocalResult(
            CommandResult result
        )
        {
            if (result == null)
                return;

            VanillaChatCommandAdapter vanillaAdapter =
                _chatAdapter;

            if (vanillaAdapter == null)
                return;

            vanillaAdapter.PresentResult(result);
        }

        private void PresentNetworkResult(
            CommandResultMessage message
        )
        {
            if (message == null)
                return;

            PresentLocalResult(
                new CommandResult(
                    message.IsSuccess,
                    message.Title,
                    message.Summary,
                    message.DetailLines,
                    message.Severity,
                    message.UsageHint
                )
            );
        }

        private void OnNetworkReceiveFailure(
            SpaceEngineersNetworkReceiveFailure failure
        )
        {
            MyLog.Default.WriteLineAndConsole(
                ModDisplayName
                    + " rejected a network packet on channel "
                    + failure.ChannelId
                    + " from peer "
                    + failure.SenderPeerId
                    + " ("
                    + failure.SerializedMessage.Length
                    + " bytes): "
                    + failure.Exception
            );
        }

        private CommandStatusSnapshot BuildStatusSnapshot()
        {
            return new CommandStatusSnapshot(ModVersionFile.VersionString, ApiVersionFile.VersionString, ProtocolVersion, _networkState, _presentationAdapter, 0);
        }

        private void DisposeRuntime()
        {
            if (_apiProvider != null)
            {
                _apiProvider.Dispose();
                _apiProvider = null;
            }

            if (_chatAdapter != null)
            {
                _chatAdapter.Dispose();
                _chatAdapter = null;
            }

            if (_chatInput != null)
            {
                _chatInput.Dispose();
                _chatInput = null;
            }

            _submissionDispatcher = null;

            if (_networkCoordinator != null)
            {
                _networkCoordinator.Dispose();
                _networkCoordinator = null;
            }

            if (_networkSession != null)
            {
                _networkSession.Dispose();
                _networkSession = null;
            }

            _networkState = "Not initialized";
            _presentationAdapter = "None";
            _initialized = false;
        }
    }
}
