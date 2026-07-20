using System;
using MarcoZechner.CommandApi.Chat;
using MarcoZechner.CommandApi.Core;
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

        private const string CommandApiVersion =
            "0.1.0";

        private const string ProtocolVersion =
            "1.0.0";

        private SpaceEngineersVanillaChatInput
            _chatInput;

        private VanillaChatCommandAdapter
            _chatAdapter;

        private bool _initialized;

        public override void BeforeStart()
        {
            base.BeforeStart();

            if (_initialized)
                return;

            if (
                MyAPIGateway.Utilities == null
                || MyAPIGateway.Utilities.IsDedicated
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

                MyAPIGateway.Utilities.ShowMessage(
                    ModDisplayName,
                    "Initialization failed. See SpaceEngineers.log."
                );
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

            var input =
                new SpaceEngineersVanillaChatInput();

            try
            {
                var output =
                    new SpaceEngineersVanillaChatOutput();

                VanillaChatCommandAdapter adapter =
                    null;

                adapter =
                    new VanillaChatCommandAdapter(
                        input,
                        output,
                        delegate(
                            ulong senderId,
                            CommandInput commandInput
                        )
                        {
                            CommandExecutionContext context =
                                SpaceEngineersExecutionContextProvider
                                    .Create(senderId);

                            CommandResult result =
                                executor.Execute(
                                    context,
                                    commandInput
                                );

                            adapter.PresentResult(result);
                        }
                    );

                _chatAdapter = adapter;
                _chatInput = input;
                _initialized = true;

                output.WriteLine(
                    ModDisplayName,
                    "Ready. Use /cmd help."
                );
            }
            catch
            {
                input.Dispose();
                throw;
            }
        }

        private static CommandStatusSnapshot
            BuildStatusSnapshot()
        {
            return new CommandStatusSnapshot(
                CommandApiVersion,
                ProtocolVersion,
                "Not initialized",
                false,
                "VanillaChat",
                0
            );
        }

        private void DisposeRuntime()
        {
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

            _initialized = false;
        }
    }
}
