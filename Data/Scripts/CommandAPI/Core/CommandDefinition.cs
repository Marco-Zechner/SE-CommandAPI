using System;
using System.Collections.Generic;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandDefinition
    {
        private readonly string[] _aliases;

        public string CanonicalName { get; }

        public string[] Aliases
        {
            get
            {
                var copy =
                    new string[_aliases.Length];

                for (
                    int index = 0;
                    index < _aliases.Length;
                    index++
                )
                {
                    copy[index] = _aliases[index];
                }

                return copy;
            }
        }

        public string ShortDescription { get; }

        public string HelpText { get; }

        public string Usage { get; }

        public string Category { get; }

        public CommandExecutionLocation ExecutionLocation { get; }

        public int PermissionRequirement { get; }

        public string OwnerId { get; }

        public CommandHandler Handler { get; }

        public CommandDefinition(
            string canonicalName,
            IList<string> aliases,
            string shortDescription,
            string helpText,
            string usage,
            string category,
            CommandExecutionLocation executionLocation,
            int permissionRequirement,
            string ownerId,
            CommandHandler handler
        )
        {
            CanonicalName =
                NormalizeName(canonicalName);

            if (aliases == null)
                throw new ArgumentNullException(nameof(aliases));

            if (ownerId == null)
                throw new ArgumentNullException(nameof(ownerId));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var normalizedAliases =
                new List<string>();

            for (
                int index = 0;
                index < aliases.Count;
                index++
            )
            {
                string alias =
                    NormalizeName(aliases[index]);

                if (alias == CanonicalName)
                {
                    throw new ArgumentException(
                        "A command alias cannot equal its canonical name.",
                        nameof(aliases)
                    );
                }

                if (normalizedAliases.Contains(alias))
                {
                    throw new ArgumentException(
                        "Command aliases must be unique.",
                        nameof(aliases)
                    );
                }

                normalizedAliases.Add(alias);
            }

            _aliases =
                normalizedAliases.ToArray();

            ShortDescription =
                shortDescription ?? string.Empty;

            HelpText =
                helpText ?? string.Empty;

            Usage =
                usage ?? string.Empty;

            Category =
                category ?? string.Empty;

            ExecutionLocation = executionLocation;
            PermissionRequirement = permissionRequirement;
            OwnerId = ownerId;
            Handler = handler;
        }

        internal static string NormalizeName(
            string name
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Command name is required.",
                    nameof(name)
                );
            }

            return name.Trim().ToLowerInvariant();
        }
    }
}
