using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            var tests =
                new List<TestCase>
                {
                    new TestCase(
                        "Parse quoted arguments and preserve casing",
                        ParseQuotedArgumentsAndPreserveCasing
                    ),
                    new TestCase(
                        "Ignore unrelated chat",
                        IgnoreUnrelatedChat
                    ),
                    new TestCase(
                        "Reject prefix without command",
                        RejectPrefixWithoutCommand
                    ),
                    new TestCase(
                        "Reject unterminated quote",
                        RejectUnterminatedQuote
                    ),
                    new TestCase(
                        "Require prefix boundary",
                        RequirePrefixBoundary
                    ),
                    new TestCase(
                        "Protect stored arguments from mutation",
                        ProtectStoredArgumentsFromMutation
                    )
                };

            int failures = 0;

            foreach (TestCase test in tests)
            {
                try
                {
                    test.Action();
                    Console.WriteLine("PASS " + test.Name);
                }
                catch (Exception exception)
                {
                    failures++;

                    Console.WriteLine(
                        "FAIL "
                        + test.Name
                        + ": "
                        + exception.Message
                    );
                }
            }

            Console.WriteLine(
                failures == 0
                    ? "OK command core tests passed: " + tests.Count
                    : "FAILED command core tests: "
                        + failures
                        + " of "
                        + tests.Count
            );

            return failures == 0 ? 0 : 1;
        }

        private static void ParseQuotedArgumentsAndPreserveCasing()
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

        private static void IgnoreUnrelatedChat()
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

        private static void RejectPrefixWithoutCommand()
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

        private static void RejectUnterminatedQuote()
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

        private static void RequirePrefixBoundary()
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

        private static void ProtectStoredArgumentsFromMutation()
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

        private sealed class TestCase
        {
            public string Name { get; }

            public Action Action { get; }

            public TestCase(
                string name,
                Action action
            )
            {
                Name = name;
                Action = action;
            }
        }
    }
}
