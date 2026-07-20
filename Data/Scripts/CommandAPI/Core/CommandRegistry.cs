using System;
using System.Collections.Generic;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandRegistry
    {
        private readonly Dictionary<
            string,
            CommandDefinition
        > _definitionsByName =
            new Dictionary<string, CommandDefinition>();

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
            Action unregister;

            return TryRegister(
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
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

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

                if (_definitionsByName.ContainsKey(name))
                {
                    unregister = null;

                    errorMessage =
                        "Command name '"
                        + name
                        + "' is already registered.";

                    return false;
                }
            }

            for (
                int index = 0;
                index < names.Count;
                index++
            )
            {
                _definitionsByName.Add(
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
                        definition,
                        names
                    );
                };

            errorMessage = null;
            return true;
        }

        private void Unregister(
            CommandDefinition definition,
            IList<string> names
        )
        {
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
                    _definitionsByName.TryGetValue(
                        name,
                        out registered
                    )
                    && ReferenceEquals(
                        registered,
                        definition
                    )
                )
                {
                    _definitionsByName.Remove(name);
                }
            }

            _definitions.Remove(definition);
        }

        public bool TryResolve(
            string name,
            out CommandDefinition definition
        )
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            string normalizedName =
                CommandDefinition.NormalizeName(name);

            return _definitionsByName.TryGetValue(
                normalizedName,
                out definition
            );
        }
    }
}
