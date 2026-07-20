using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Chat;
using MarcoZechner.CommandApi.Core;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class VanillaChatCommandAdapterTests
    {
        [Fact]
        public void LeaveUnrelatedChatUntouched()
        {
            var input =
                new FakeChatInput();

            var output =
                new FakeChatOutput();

            int contextRequests = 0;

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    new CommandExecutor(
                        new CommandRegistry()
                    ),
                    delegate(ulong senderId)
                    {
                        contextRequests++;

                        return Context(
                            "unexpected",
                            senderId
                        );
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                17UL,
                "hello everyone",
                ref sendToOthers
            );

            True(
                sendToOthers,
                "Unrelated chat was suppressed."
            );

            Equal(
                0,
                output.Lines.Count,
                "output count"
            );

            Equal(
                0,
                contextRequests,
                "context request count"
            );

            adapter.Dispose();
        }

        [Fact]
        public void SuppressAndExecuteRecognizedCommand()
        {
            var registry =
                new CommandRegistry();

            string observedArgument = null;

            Register(
                registry,
                Definition(
                    "echo",
                    delegate(
                        CommandExecutionContext context,
                        CommandInput commandInput
                    )
                    {
                        observedArgument =
                            commandInput.Arguments[0];

                        return new CommandResult(
                            true,
                            "Echo",
                            "Command completed.",
                            new[]
                            {
                                observedArgument
                            },
                            CommandSeverity.Success,
                            null
                        );
                    }
                )
            );

            var input =
                new FakeChatInput();

            var output =
                new FakeChatOutput();

            ulong observedSender = 0UL;

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    new CommandExecutor(registry),
                    delegate(ulong senderId)
                    {
                        observedSender = senderId;

                        return Context(
                            "request-echo",
                            senderId
                        );
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                42UL,
                "/cmd echo \"MiXeD Value\"",
                ref sendToOthers
            );

            False(
                sendToOthers,
                "Recognized command was not suppressed."
            );

            Equal(
                42UL,
                observedSender,
                "sender ID"
            );

            Equal(
                "MiXeD Value",
                observedArgument,
                "command argument"
            );

            AssertOutput(
                output,
                new[]
                {
                    "Echo: Command completed.",
                    "MiXeD Value"
                }
            );

            adapter.Dispose();
        }

        [Fact]
        public void SuppressMalformedCommandAndShowParseError()
        {
            var input =
                new FakeChatInput();

            var output =
                new FakeChatOutput();

            int contextRequests = 0;

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    new CommandExecutor(
                        new CommandRegistry()
                    ),
                    delegate(ulong senderId)
                    {
                        contextRequests++;

                        return Context(
                            "unexpected",
                            senderId
                        );
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                7UL,
                "/cmd echo \"unterminated",
                ref sendToOthers
            );

            False(
                sendToOthers,
                "Malformed command was not suppressed."
            );

            Equal(
                0,
                contextRequests,
                "context request count"
            );

            AssertOutput(
                output,
                new[]
                {
                    "Command error: Quoted argument is not terminated."
                }
            );

            adapter.Dispose();
        }

        [Fact]
        public void FormatStructuredExecutionFailure()
        {
            var input =
                new FakeChatInput();

            var output =
                new FakeChatOutput();

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    new CommandExecutor(
                        new CommandRegistry()
                    ),
                    delegate(ulong senderId)
                    {
                        return Context(
                            "request-missing",
                            senderId
                        );
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                9UL,
                "/cmd missing",
                ref sendToOthers
            );

            False(
                sendToOthers,
                "Unknown command was not suppressed."
            );

            AssertOutput(
                output,
                new[]
                {
                    "Unknown command: Unknown command 'missing'.",
                    "Usage: /cmd help"
                }
            );

            adapter.Dispose();
        }

        [Fact]
        public void BoundVerboseResultOutput()
        {
            var registry =
                new CommandRegistry();

            Register(
                registry,
                Definition(
                    "verbose",
                    delegate(
                        CommandExecutionContext context,
                        CommandInput commandInput
                    )
                    {
                        return new CommandResult(
                            true,
                            "Verbose",
                            "Many details.",
                            new[]
                            {
                                "Line 1",
                                "Line 2",
                                "Line 3",
                                "Line 4",
                                "Line 5",
                                "Line 6",
                                "Line 7",
                                "Line 8",
                                "Line 9",
                                "Line 10"
                            },
                            CommandSeverity.Information,
                            null
                        );
                    }
                )
            );

            var input =
                new FakeChatInput();

            var output =
                new FakeChatOutput();

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    new CommandExecutor(registry),
                    delegate(ulong senderId)
                    {
                        return Context(
                            "request-verbose",
                            senderId
                        );
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                11UL,
                "/cmd verbose",
                ref sendToOthers
            );

            AssertOutput(
                output,
                new[]
                {
                    "Verbose: Many details.",
                    "Line 1",
                    "Line 2",
                    "Line 3",
                    "Line 4",
                    "Line 5",
                    "Line 6",
                    "Line 7",
                    "Line 8",
                    "... 2 more lines."
                }
            );

            adapter.Dispose();
        }

        [Fact]
        public void DisposeUnsubscribesChatHandler()
        {
            var input =
                new FakeChatInput();

            var output =
                new FakeChatOutput();

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    new CommandExecutor(
                        new CommandRegistry()
                    ),
                    delegate(ulong senderId)
                    {
                        return Context(
                            "request-disposed",
                            senderId
                        );
                    }
                );

            Equal(
                1,
                input.SubscriberCount,
                "subscriber count before disposal"
            );

            adapter.Dispose();
            adapter.Dispose();

            Equal(
                0,
                input.SubscriberCount,
                "subscriber count after disposal"
            );

            bool sendToOthers = true;

            input.Raise(
                12UL,
                "/cmd missing",
                ref sendToOthers
            );

            True(
                sendToOthers,
                "Disposed adapter still suppressed chat."
            );

            Equal(
                0,
                output.Lines.Count,
                "output count after disposal"
            );
        }

        private static CommandDefinition Definition(
            string name,
            CommandHandler handler
        )
        {
            return new CommandDefinition(
                name,
                new string[0],
                name + " command",
                name + " command help",
                name,
                "Tests",
                CommandExecutionLocation.Server,
                0,
                "CommandAPI.Tests",
                handler
            );
        }

        private static void Register(
            CommandRegistry registry,
            CommandDefinition definition
        )
        {
            string errorMessage;

            True(
                registry.TryRegister(
                    definition,
                    out errorMessage
                ),
                errorMessage
            );
        }

        private static CommandExecutionContext Context(
            string requestId,
            ulong senderId
        )
        {
            return new CommandExecutionContext(
                requestId,
                senderId,
                1234L,
                "Test Player",
                0,
                true
            );
        }

        private static void AssertOutput(
            FakeChatOutput output,
            string[] expectedMessages
        )
        {
            Equal(
                expectedMessages.Length,
                output.Lines.Count,
                "output count"
            );

            for (
                int index = 0;
                index < expectedMessages.Length;
                index++
            )
            {
                Equal(
                    "CommandAPI",
                    output.Lines[index].Author,
                    "author[" + index + "]"
                );

                Equal(
                    expectedMessages[index],
                    output.Lines[index].Message,
                    "message[" + index + "]"
                );
            }
        }

        private static void True(
            bool condition,
            string message
        )
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void False(
            bool condition,
            string message
        )
        {
            if (condition)
                throw new InvalidOperationException(message);
        }

        private static void Equal<T>(
            T expected,
            T actual,
            string label
        )
        {
            if (!EqualityComparer<T>.Default.Equals(
                expected,
                actual
            ))
            {
                throw new InvalidOperationException(
                    label
                    + ": expected "
                    + expected
                    + ", actual "
                    + actual
                );
            }
        }

        private sealed class FakeChatInput : IVanillaChatInput
        {
            private VanillaChatMessageEnteredHandler _handler;

            public int SubscriberCount
            {
                get
                {
                    return _handler == null
                        ? 0
                        : _handler.GetInvocationList().Length;
                }
            }

            public event VanillaChatMessageEnteredHandler MessageEntered
            {
                add { _handler += value; }
                remove { _handler -= value; }
            }

            public void Raise(
                ulong senderId,
                string message,
                ref bool sendToOthers
            )
            {
                VanillaChatMessageEnteredHandler handler =
                    _handler;

                if (handler != null)
                {
                    handler(
                        senderId,
                        message,
                        ref sendToOthers
                    );
                }
            }
        }

        private sealed class FakeChatOutput : IVanillaChatOutput
        {
            public List<OutputLine> Lines { get; }

            public FakeChatOutput()
            {
                Lines =
                    new List<OutputLine>();
            }

            public void WriteLine(
                string author,
                string message
            )
            {
                Lines.Add(
                    new OutputLine(
                        author,
                        message
                    )
                );
            }
        }

        private sealed class OutputLine
        {
            public string Author { get; }

            public string Message { get; }

            public OutputLine(
                string author,
                string message
            )
            {
                Author = author;
                Message = message;
            }
        }
    }
}
