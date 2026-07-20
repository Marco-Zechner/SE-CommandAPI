using System;
using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Chat
{
    public sealed class VanillaChatCommandAdapter :
        IDisposable
    {
        private const string Author = "CommandAPI";
        private const int MaximumDetailLines = 8;

        private readonly IVanillaChatInput _input;
        private readonly IVanillaChatOutput _output;

        private readonly VanillaChatCommandSubmitter
            _submitter;

        private bool _disposed;

        public VanillaChatCommandAdapter(
            IVanillaChatInput input,
            IVanillaChatOutput output,
            VanillaChatCommandSubmitter submitter
        )
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            if (submitter == null)
            {
                throw new ArgumentNullException(
                    nameof(submitter)
                );
            }

            _input = input;
            _output = output;
            _submitter = submitter;

            _input.MessageEntered += OnMessageEntered;
        }

        public void PresentResult(
            CommandResult result
        )
        {
            if (_disposed)
                return;

            if (result == null)
            {
                WriteAdapterFailure();
                return;
            }

            _output.WriteLine(
                Author,
                result.Title
                    + ": "
                    + result.Summary
            );

            string[] detailLines =
                result.DetailLines;

            int detailCount =
                Math.Min(
                    detailLines.Length,
                    MaximumDetailLines
                );

            for (
                int index = 0;
                index < detailCount;
                index++
            )
            {
                _output.WriteLine(
                    Author,
                    detailLines[index] ?? string.Empty
                );
            }

            int remainingDetailCount =
                detailLines.Length - detailCount;

            if (remainingDetailCount > 0)
            {
                _output.WriteLine(
                    Author,
                    "... "
                        + remainingDetailCount
                        + " more lines."
                );
            }

            if (
                !string.IsNullOrWhiteSpace(
                    result.UsageHint
                )
            )
            {
                _output.WriteLine(
                    Author,
                    "Usage: " + result.UsageHint
                );
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _input.MessageEntered -= OnMessageEntered;
        }

        private void OnMessageEntered(
            ulong senderId,
            string message,
            ref bool sendToOthers
        )
        {
            if (_disposed)
                return;

            CommandParseResult parseResult =
                CommandInputParser.Parse(message);

            if (
                parseResult.Status
                    == CommandParseStatus.NotCommand
            )
            {
                return;
            }

            sendToOthers = false;

            if (
                parseResult.Status
                    == CommandParseStatus.Error
            )
            {
                _output.WriteLine(
                    Author,
                    "Command error: "
                        + parseResult.ErrorMessage
                );

                return;
            }

            try
            {
                _submitter(
                    senderId,
                    parseResult.Input
                );
            }
            catch (Exception)
            {
                WriteAdapterFailure();
            }
        }

        private void WriteAdapterFailure()
        {
            _output.WriteLine(
                Author,
                "Command failed: "
                    + "The command could not be completed."
            );
        }
    }
}
