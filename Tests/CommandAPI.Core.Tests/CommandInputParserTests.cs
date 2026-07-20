using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandInputParserTests
    {
        [Fact]
        public void ParseQuotedArgumentsAndPreserveCasing()
        {
            CommandParseResult result =
                CommandInputParser.Parse(
                    "/CMD echo \"Hello World\" MiXeD"
                );

            Equal(
                CommandParseStatus.Success,
                result.Status,
                "status"
            );

            Equal(
                "echo",
                result.Input.CommandName,
                "command name"
            );

            SequenceEqual(
                new[] { "Hello World", "MiXeD" },
                result.Input.Arguments,
                "arguments"
            );
        }

        [Fact]
        public void IgnoreUnrelatedChat()
        {
            CommandParseResult result =
                CommandInputParser.Parse(
                    "hello everyone"
                );

            Equal(
                CommandParseStatus.NotCommand,
                result.Status,
                "status"
            );
        }

        [Fact]
        public void RejectPrefixWithoutCommand()
        {
            CommandParseResult result =
                CommandInputParser.Parse(
                    "/cmd   "
                );

            Equal(
                CommandParseStatus.Error,
                result.Status,
                "status"
            );

            Equal(
                "A command name is required.",
                result.ErrorMessage,
                "error"
            );
        }

        [Fact]
        public void RejectUnterminatedQuote()
        {
            CommandParseResult result =
                CommandInputParser.Parse(
                    "/cmd echo \"unfinished"
                );

            Equal(
                CommandParseStatus.Error,
                result.Status,
                "status"
            );

            Equal(
                "Quoted argument is not terminated.",
                result.ErrorMessage,
                "error"
            );
        }

        [Fact]
        public void RequirePrefixBoundary()
        {
            CommandParseResult result =
                CommandInputParser.Parse(
                    "/cmdx ping"
                );

            Equal(
                CommandParseStatus.NotCommand,
                result.Status,
                "status"
            );
        }

        [Fact]
        public void ProtectStoredArgumentsFromMutation()
        {
            var input =
                new CommandInput(
                    "echo",
                    new[] { "Original" }
                );

            string[] firstRead =
                input.Arguments;

            firstRead[0] = "Changed";

            Equal(
                "Original",
                input.Arguments[0],
                "stored argument"
            );
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

        private static void SequenceEqual(
            string[] expected,
            IReadOnlyList<string> actual,
            string label
        )
        {
            Equal(
                expected.Length,
                actual.Count,
                label + " count"
            );

            for (int index = 0; index < expected.Length; index++)
            {
                Equal(
                    expected[index],
                    actual[index],
                    label + "[" + index + "]"
                );
            }
        }
    }
}
