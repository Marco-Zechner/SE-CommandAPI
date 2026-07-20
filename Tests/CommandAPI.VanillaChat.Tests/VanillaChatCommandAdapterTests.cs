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
            var input = new FakeChatInput();
            var output = new FakeChatOutput();
            int submissionCount = 0;

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    delegate(
                        ulong senderId,
                        CommandInput commandInput
                    )
                    {
                        submissionCount++;
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                17UL,
                "hello everyone",
                ref sendToOthers
            );

            Assert.True(sendToOthers);
            Assert.Equal(0, submissionCount);
            Assert.Empty(output.Lines);

            adapter.Dispose();
        }

        [Fact]
        public void SuppressAndSubmitRecognizedCommand()
        {
            var input = new FakeChatInput();
            var output = new FakeChatOutput();

            ulong observedSender = 0UL;
            CommandInput observedInput = null;

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    delegate(
                        ulong senderId,
                        CommandInput commandInput
                    )
                    {
                        observedSender = senderId;
                        observedInput = commandInput;
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                42UL,
                "/cmd echo \"MiXeD Value\"",
                ref sendToOthers
            );

            Assert.False(sendToOthers);
            Assert.Equal(42UL, observedSender);
            Assert.NotNull(observedInput);
            Assert.Equal("echo", observedInput.CommandName);
            Assert.Equal(
                new[] { "MiXeD Value" },
                observedInput.Arguments
            );
            Assert.Empty(output.Lines);

            adapter.Dispose();
        }

        [Fact]
        public void SuppressMalformedCommandAndShowParseError()
        {
            var input = new FakeChatInput();
            var output = new FakeChatOutput();
            int submissionCount = 0;

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    delegate(
                        ulong senderId,
                        CommandInput commandInput
                    )
                    {
                        submissionCount++;
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                7UL,
                "/cmd echo \"unterminated",
                ref sendToOthers
            );

            Assert.False(sendToOthers);
            Assert.Equal(0, submissionCount);

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
        public void ReportSubmissionFailure()
        {
            var input = new FakeChatInput();
            var output = new FakeChatOutput();

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    delegate(
                        ulong senderId,
                        CommandInput commandInput
                    )
                    {
                        throw new InvalidOperationException(
                            "Transport unavailable."
                        );
                    }
                );

            bool sendToOthers = true;

            input.Raise(
                9UL,
                "/cmd ping",
                ref sendToOthers
            );

            Assert.False(sendToOthers);

            AssertOutput(
                output,
                new[]
                {
                    "Command failed: The command could not be completed."
                }
            );

            adapter.Dispose();
        }

        [Fact]
        public void PresentStructuredResult()
        {
            var input = new FakeChatInput();
            var output = new FakeChatOutput();

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    IgnoreSubmission
                );

            adapter.PresentResult(
                new CommandResult(
                    false,
                    "Unknown command",
                    "Unknown command 'missing'.",
                    new string[0],
                    CommandSeverity.Error,
                    "/cmd help"
                )
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
            var input = new FakeChatInput();
            var output = new FakeChatOutput();

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    IgnoreSubmission
                );

            adapter.PresentResult(
                new CommandResult(
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
                )
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
        public void DisposeUnsubscribesAndStopsPresentation()
        {
            var input = new FakeChatInput();
            var output = new FakeChatOutput();
            int submissionCount = 0;

            var adapter =
                new VanillaChatCommandAdapter(
                    input,
                    output,
                    delegate(
                        ulong senderId,
                        CommandInput commandInput
                    )
                    {
                        submissionCount++;
                    }
                );

            Assert.Equal(1, input.SubscriberCount);

            adapter.Dispose();
            adapter.Dispose();

            Assert.Equal(0, input.SubscriberCount);

            bool sendToOthers = true;

            input.Raise(
                12UL,
                "/cmd ping",
                ref sendToOthers
            );

            adapter.PresentResult(
                new CommandResult(
                    true,
                    "Pong",
                    "Completed.",
                    new string[0],
                    CommandSeverity.Success,
                    null
                )
            );

            Assert.True(sendToOthers);
            Assert.Equal(0, submissionCount);
            Assert.Empty(output.Lines);
        }

        private static void IgnoreSubmission(
            ulong senderId,
            CommandInput commandInput
        )
        {
        }

        private static void AssertOutput(
            FakeChatOutput output,
            string[] expectedMessages
        )
        {
            Assert.Equal(
                expectedMessages.Length,
                output.Lines.Count
            );

            for (
                int index = 0;
                index < expectedMessages.Length;
                index++
            )
            {
                Assert.Equal(
                    "CommandAPI",
                    output.Lines[index].Author
                );

                Assert.Equal(
                    expectedMessages[index],
                    output.Lines[index].Message
                );
            }
        }

        private sealed class FakeChatInput :
            IVanillaChatInput
        {
            private VanillaChatMessageEnteredHandler
                _handler;

            public int SubscriberCount
            {
                get
                {
                    return _handler == null
                        ? 0
                        : _handler
                            .GetInvocationList()
                            .Length;
                }
            }

            public event VanillaChatMessageEnteredHandler
                MessageEntered
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

                if (handler == null)
                    return;

                handler(
                    senderId,
                    message,
                    ref sendToOthers
                );
            }
        }

        private sealed class FakeChatOutput :
            IVanillaChatOutput
        {
            public List<OutputLine> Lines { get; }

            public FakeChatOutput()
            {
                Lines = new List<OutputLine>();
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
