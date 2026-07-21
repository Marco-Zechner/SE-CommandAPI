using System;
using System.Collections.Generic;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandRegistry
    {
        private readonly Dictionary<
            string,
            Dictionary<string, CommandDefinition>
        > _definitionsByPrefix =
            new Dictionary<
                string,
                Dictionary<string, CommandDefinition>
            >(
                StringComparer.Ordinal
            );

        private readonly List<
            CommandDefinition
        > _definitions =
            new List<CommandDefinition>();

        public int Count
        {
            get { return _definitions.Count; }
        }

        public CommandDefinition[] GetDefinitions()
        {
            var copy =
                new CommandDefinition[_definitions.Count];

            for (
                int index = 0;
                index < _definitions.Count;
                index++
            )
            {
                copy[index] = _definitions[index];
            }

            return copy;
        }

        public bool TryRegister(
            CommandDefinition definition,
            out string errorMessage
        )
        {
            return TryRegister(
                CommandInputParser.Prefix,
                definition,
                out errorMessage
            );
        }

        public bool TryRegister(
            string prefix,
            CommandDefinition definition,
            out string errorMessage
        )
        {
            Action unregister;

            return TryRegister(
                prefix,
                definition,
                out unregister,
                out errorMessage
            );
        }

        public bool TryRegister(
            CommandDefinition definition,
            out Action unregister,
            out string errorMessage
        )
        {
            return TryRegister(
                CommandInputParser.Prefix,
                definition,
                out unregister,
                out errorMessage
            );
        }

        public bool TryRegister(
            string prefix,
            CommandDefinition definition,
            out Action unregister,
            out string errorMessage
        )
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            string normalizedPrefix =
                CommandInput.NormalizePrefix(prefix);

            Dictionary<string, CommandDefinition>
                definitionsByName;

            bool prefixExists =
                _definitionsByPrefix.TryGetValue(
                    normalizedPrefix,
                    out definitionsByName
                );

            if (!prefixExists)
            {
                definitionsByName =
                    new Dictionary<
                        string,
                        CommandDefinition
                    >(
                        StringComparer.Ordinal
                    );
            }

            var names =
                new List<string>();

            names.Add(definition.CanonicalName);

            string[] aliases =
                definition.Aliases;

            for (
                int index = 0;
                index < aliases.Length;
                index++
            )
            {
                names.Add(aliases[index]);
            }

            for (
                int index = 0;
                index < names.Count;
                index++
            )
            {
                string name =
                    names[index];

                if (definitionsByName.ContainsKey(name))
                {
                    unregister = null;

                    errorMessage =
                        "Command name '"
                        + name
                        + "' is already registered.";

                    return false;
                }
            }

            if (!prefixExists)
            {
                _definitionsByPrefix.Add(
                    normalizedPrefix,
                    definitionsByName
                );
            }

            for (
                int index = 0;
                index < names.Count;
                index++
            )
            {
                definitionsByName.Add(
                    names[index],
                    definition
                );
            }

            _definitions.Add(definition);

            bool isUnregistered = false;

            unregister =
                delegate
                {
                    if (isUnregistered)
                        return;

                    isUnregistered = true;

                    Unregister(
                        normalizedPrefix,
                        definition,
                        names
                    );
                };

            errorMessage = null;
            return true;
        }

        private void Unregister(
            string prefix,
            CommandDefinition definition,
            IList<string> names
        )
        {
            Dictionary<string, CommandDefinition>
                definitionsByName;

            if (
                !_definitionsByPrefix.TryGetValue(
                    prefix,
                    out definitionsByName
                )
            )
            {
                return;
            }

            for (
                int index = 0;
                index < names.Count;
                index++
            )
            {
                string name =
                    names[index];

                CommandDefinition registered;

                if (
                    definitionsByName.TryGetValue(
                        name,
                        out registered
                    )
                    && ReferenceEquals(
                        registered,
                        definition
                    )
                )
                {
                    definitionsByName.Remove(name);
                }
            }

            _definitions.Remove(definition);

            if (definitionsByName.Count == 0)
                _definitionsByPrefix.Remove(prefix);
        }

        public bool TryResolve(
            string name,
            out CommandDefinition definition
        )
        {
            return TryResolve(
                CommandInputParser.Prefix,
                name,
                out definition
            );
        }

        public bool TryResolve(
            string prefix,
            string name,
            out CommandDefinition definition
        )
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            string normalizedPrefix =
                CommandInput.NormalizePrefix(prefix);

            Dictionary<string, CommandDefinition>
                definitionsByName;

            if (
                !_definitionsByPrefix.TryGetValue(
                    normalizedPrefix,
                    out definitionsByName
                )
            )
            {
                return false;
            }

            string normalizedName =
                CommandDefinition.NormalizeName(name);

            return definitionsByName.TryGetValue(
                normalizedName,
                out definition
            );
        }
    }
}
