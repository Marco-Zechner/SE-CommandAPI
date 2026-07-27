using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Api
{
    public sealed class CommandRegistrationService
    {
        private readonly CommandRegistry _registry;

        public CommandRegistrationService(
            CommandRegistry registry
        )
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            _registry = registry;
        }

        public Action RegisterCommand(
            IDictionary<string, object> metadata,
            Func<
                IDictionary<string, object>,
                IDictionary<string, object>
            > handler
        )
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            string ownerId =
                ReadRequiredString(
                    metadata,
                    "OwnerId"
                );

            string prefix =
                ReadOptionalString(
                    metadata,
                    "Prefix"
                );

            if (string.IsNullOrWhiteSpace(prefix))
                prefix = CommandInputParser.Prefix;

            prefix =
                CommandInput.NormalizePrefix(prefix);

            CommandExecutionLocation
                executionLocation =
                    ReadOptionalExecutionLocation(
                        metadata,
                        "ExecutionLocation",
                        CommandExecutionLocation.Server
                    );

            string canonicalName =
                ReadRequiredString(
                    metadata,
                    "CanonicalName"
                );

            string[] aliases =
                ReadOptionalStringArray(
                    metadata,
                    "Aliases"
                );

            string shortDescription =
                ReadOptionalString(
                    metadata,
                    "ShortDescription"
                );

            string helpText =
                ReadOptionalString(
                    metadata,
                    "HelpText"
                );

            string usage =
                ReadOptionalString(
                    metadata,
                    "Usage"
                );

            string category =
                ReadOptionalString(
                    metadata,
                    "Category"
                );

            int permissionRequirement =
                ReadOptionalInt32(
                    metadata,
                    "PermissionRequirement",
                    0
                );

            var definition =
                new CommandDefinition(
                    canonicalName,
                    aliases,
                    shortDescription,
                    helpText,
                    usage,
                    category,
                    executionLocation,
                    permissionRequirement,
                    ownerId,
                    delegate(
                        CommandExecutionContext context,
                        CommandInput input
                    )
                    {
                        return InvokeHandler(
                            handler,
                            context,
                            input
                        );
                    }
                );

            Action unregister;
            string errorMessage;

            if (
                !_registry.TryRegister(
                    prefix,
                    definition,
                    out unregister,
                    out errorMessage
                )
            )
            {
                throw new InvalidOperationException(
                    errorMessage
                );
            }

            return unregister;
        }

        private static CommandResult InvokeHandler(
            Func<
                IDictionary<string, object>,
                IDictionary<string, object>
            > handler,
            CommandExecutionContext context,
            CommandInput input
        )
        {
            var request =
                new Dictionary<string, object>(
                    StringComparer.Ordinal
                )
                {
                    {
                        "RequestId",
                        context.RequestId
                    },
                    {
                        "RequesterSteamId",
                        context.RequesterSteamId
                    },
                    {
                        "RequesterIdentityId",
                        context.RequesterIdentityId
                    },
                    {
                        "RequesterDisplayName",
                        context.RequesterDisplayName
                    },
                    {
                        "PermissionLevel",
                        context.PermissionLevel
                    },
                    {
                        "IsServer",
                        context.IsServer
                    },
                    {
                        "Prefix",
                        input.Prefix
                    },
                    {
                        "CommandName",
                        input.CommandName
                    },
                    {
                        "Arguments",
                        input.Arguments
                    }
                };

            IDictionary<string, object> response =
                handler(request);

            if (response == null)
            {
                throw new ArgumentException(
                    "The command handler returned no result.",
                    nameof(handler)
                );
            }

            bool isSuccess =
                ReadRequiredBoolean(
                    response,
                    "IsSuccess"
                );

            string title =
                ReadRequiredString(
                    response,
                    "Title"
                );

            string summary =
                ReadRequiredString(
                    response,
                    "Summary"
                );

            string[] detailLines =
                ReadOptionalStringArray(
                    response,
                    "DetailLines"
                );

            CommandSeverity severity =
                ReadOptionalSeverity(
                    response,
                    "Severity",
                    CommandSeverity.Information
                );

            string usageHint =
                ReadNullableString(
                    response,
                    "UsageHint"
                );

            return new CommandResult(
                isSuccess,
                title,
                summary,
                detailLines,
                severity,
                usageHint
            );
        }

        private static string ReadRequiredString(
            IDictionary<string, object> values,
            string key
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
            {
                throw new ArgumentException(
                    "Required field '"
                    + key
                    + "' is missing.",
                    nameof(values)
                );
            }

            string text = value as string;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' must be a non-empty string.",
                    nameof(values)
                );
            }

            return text.Trim();
        }

        private static string ReadOptionalString(
            IDictionary<string, object> values,
            string key
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
                return string.Empty;

            if (value == null)
                return string.Empty;

            string text = value as string;

            if (text == null)
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' must be a string.",
                    nameof(values)
                );
            }

            return text;
        }

        private static string ReadNullableString(
            IDictionary<string, object> values,
            string key
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
                return null;

            if (value == null)
                return null;

            string text = value as string;

            if (text == null)
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' must be a string or null.",
                    nameof(values)
                );
            }

            return text;
        }

        private static CommandExecutionLocation
            ReadOptionalExecutionLocation(
                IDictionary<string, object> values,
                string key,
                CommandExecutionLocation defaultValue
            )
        {
            object value;

            if (!values.TryGetValue(key, out value))
                return defaultValue;

            string text = value as string;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' must name an execution location.",
                    nameof(values)
                );
            }

            CommandExecutionLocation location;

            if (
                !Enum.TryParse(
                    text,
                    true,
                    out location
                )
                || !Enum.IsDefined(
                    typeof(CommandExecutionLocation),
                    location
                )
            )
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' contains an unsupported execution location.",
                    nameof(values)
                );
            }

            return location;
        }

        private static int ReadOptionalInt32(
            IDictionary<string, object> values,
            string key,
            int defaultValue
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
                return defaultValue;

            if (!(value is int))
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' must be an Int32.",
                    nameof(values)
                );
            }

            return (int)value;
        }

        private static bool ReadRequiredBoolean(
            IDictionary<string, object> values,
            string key
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
            {
                throw new ArgumentException(
                    "Required field '"
                    + key
                    + "' is missing.",
                    nameof(values)
                );
            }

            if (!(value is bool))
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' must be a Boolean.",
                    nameof(values)
                );
            }

            return (bool)value;
        }

        private static string[] ReadOptionalStringArray(
            IDictionary<string, object> values,
            string key
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
                return new string[0];

            if (value == null)
                return new string[0];

            string[] array = value as string[];

            if (array != null)
            {
                var copy =
                    new string[array.Length];

                for (
                    int index = 0;
                    index < array.Length;
                    index++
                )
                {
                    if (array[index] == null)
                    {
                        throw new ArgumentException(
                            "Field '"
                            + key
                            + "' cannot contain null strings.",
                            nameof(values)
                        );
                    }

                    copy[index] = array[index];
                }

                return copy;
            }

            IList<string> list =
                value as IList<string>;

            if (list == null)
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' must be a string array.",
                    nameof(values)
                );
            }

            var listCopy =
                new string[list.Count];

            for (
                int index = 0;
                index < list.Count;
                index++
            )
            {
                if (list[index] == null)
                {
                    throw new ArgumentException(
                        "Field '"
                        + key
                        + "' cannot contain null strings.",
                        nameof(values)
                    );
                }

                listCopy[index] = list[index];
            }

            return listCopy;
        }

        private static CommandSeverity ReadOptionalSeverity(
            IDictionary<string, object> values,
            string key,
            CommandSeverity defaultValue
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
                return defaultValue;

            string text = value as string;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' must name a command severity.",
                    nameof(values)
                );
            }

            CommandSeverity severity;

            if (
                !Enum.TryParse(
                    text,
                    true,
                    out severity
                )
                || !Enum.IsDefined(
                    typeof(CommandSeverity),
                    severity
                )
            )
            {
                throw new ArgumentException(
                    "Field '"
                    + key
                    + "' contains an unsupported severity.",
                    nameof(values)
                );
            }

            return severity;
        }
    }
}
