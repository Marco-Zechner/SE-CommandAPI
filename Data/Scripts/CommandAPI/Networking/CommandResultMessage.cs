using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Networking
{
    public sealed class CommandResultMessage
    {
        private readonly string[] _detailLines;

        public string RequestId { get; }

        public bool IsSuccess { get; }

        public string Title { get; }

        public string Summary { get; }

        public string[] DetailLines
        {
            get
            {
                return Copy(_detailLines);
            }
        }

        public CommandSeverity Severity { get; }

        public string UsageHint { get; }

        public CommandResultMessage(
            string requestId,
            bool isSuccess,
            string title,
            string summary,
            IList<string> detailLines,
            CommandSeverity severity,
            string usageHint
        )
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException(
                    "A request ID is required.",
                    nameof(requestId)
                );
            }

            if (title == null)
                throw new ArgumentNullException(nameof(title));

            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            if (detailLines == null)
            {
                throw new ArgumentNullException(
                    nameof(detailLines)
                );
            }

            if (
                !Enum.IsDefined(
                    typeof(CommandSeverity),
                    severity
                )
            )
            {
                throw new ArgumentException(
                    "The command severity is invalid.",
                    nameof(severity)
                );
            }

            RequestId = requestId;
            IsSuccess = isSuccess;
            Title = title;
            Summary = summary;
            Severity = severity;
            UsageHint = usageHint;
            _detailLines = new string[detailLines.Count];

            for (
                int index = 0;
                index < detailLines.Count;
                index++
            )
            {
                _detailLines[index] =
                    detailLines[index] ?? string.Empty;
            }
        }

        private static string[] Copy(string[] source)
        {
            var copy = new string[source.Length];

            for (
                int index = 0;
                index < source.Length;
                index++
            )
            {
                copy[index] = source[index];
            }

            return copy;
        }
    }
}
