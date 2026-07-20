using System;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandResult
    {
        private readonly string[] _detailLines;

        public bool IsSuccess { get; }

        public string Title { get; }

        public string Summary { get; }

        public string[] DetailLines
        {
            get
            {
                var copy =
                    new string[_detailLines.Length];

                for (
                    int index = 0;
                    index < _detailLines.Length;
                    index++
                )
                {
                    copy[index] = _detailLines[index];
                }

                return copy;
            }
        }

        public CommandSeverity Severity { get; }

        public string UsageHint { get; }

        public CommandResult(
            bool isSuccess,
            string title,
            string summary,
            string[] detailLines,
            CommandSeverity severity,
            string usageHint
        )
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));

            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            if (detailLines == null)
                throw new ArgumentNullException(
                    nameof(detailLines)
                );

            IsSuccess = isSuccess;
            Title = title;
            Summary = summary;
            Severity = severity;
            UsageHint = usageHint;

            _detailLines =
                new string[detailLines.Length];

            for (
                int index = 0;
                index < detailLines.Length;
                index++
            )
            {
                _detailLines[index] = detailLines[index];
            }
        }
    }
}
