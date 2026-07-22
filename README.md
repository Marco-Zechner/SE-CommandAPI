# CommandAPI

CommandAPI is a command framework for Space Engineers.

## Current runtime

The current vertical slice supports:

- multiple top-level command prefixes such as `/cmd`, `/ime`, or `/smoke`;
- command names and aliases scoped within each prefix;
- shared prefixes, with collisions enforced at command and alias level;
- client-local, authoritative-server, either-side, and internal commands;
- local execution for `Client` and normal `Either` submissions;
- authoritative networking for `Server` submissions;
- requester-only structured command results;
- optional RichHudChatAPI input and transcript presentation for /cmd;
- vanilla chat interception as the client fallback;
- external command registration on clients, listen servers, and dedicated servers.

Unknown prefixes are left untouched as ordinary chat. A registered prefix is
removed from interception when its final command is unregistered.

## RichHudChatAPI integration

RichHudChatAPI is optional and negotiated by API descriptor version:

- `1.0` provides `/cmd` route submission and transcript presentation.
- `1.1` adds command suggestions, selection, and completion interactions.
- `1.2` lets `/` activate CommandAPI draft context while submissions remain
  restricted to the `/cmd` route.
- `1.3` adds styled headers, command rows, input spans, control hints, filtering,
  and unknown-command feedback.

CommandAPI owns command lookup, filtering, selection, completion, validation
semantics, and execution. RichHudChatAPI only renders generic presentation data
and forwards input interactions. Older compatible providers retain their
earlier behavior without receiving unsupported metadata.

## Prefix collision policy

Prefixes are shared namespaces rather than exclusively owned resources.

Two mods may both register commands under `/ime`:

- Mod A may register `/ime theme`.
- Mod B may register `/ime reload`.

Both registrations succeed. If both attempt `/ime theme`, or if a name collides
with an alias already registered under `/ime`, the second registration fails.

The same command name may exist under different prefixes, such as `/cmd status`
and `/ime status`.

## Built-in commands

CommandAPI owns `/cmd`:

- `/cmd help`
- `/cmd ping`
- `/cmd whoami`
- `/cmd status`

Help lookup and usage output are scoped to the prefix used for the request.

## External registration API

The `RegisterCommand` metadata dictionary supports:

- `OwnerId` â€” required string;
- `Prefix` â€” optional string, defaults to `/cmd`;
- `ExecutionLocation` â€” optional string: `Client`, `Server`, `Either`, or
  `Internal`; defaults to `Server`;
- `CanonicalName` â€” required string;
- `Aliases` â€” optional string array;
- `ShortDescription`, `HelpText`, `Usage`, and `Category` â€” optional strings;
- `PermissionRequirement` â€” optional `Int32`.

The handler request includes `Prefix`, `CommandName`, `Arguments`, requester
identity and permission fields, `RequestId`, and `IsServer`.

The API descriptor version is `1.1.0`. The registration endpoint signature is
unchanged, and omitted new fields retain the original `/cmd` server-command
behavior.

External mods must register on every peer where they are loaded. CommandAPI
publishes its provider locally on every peer so client command handlers and
server command handlers are both available in the correct process.

## Smoke consumers

The two included smoke mods both use the shared `/smoke` prefix.

Each registers a deterministic server command:

- `/smoke smoke.alpha`
- `/smoke smoke.beta`

Both also attempt the client-local `/smoke smoke` command. The first loaded
consumer owns that command name on the current peer; the other keeps its
qualified server command.

## Build and test

From the mod root:

`dotnet test CommandAPI.slnx --nologo`

`dotnet build Data\CommandAPI.csproj --nologo`

The smoke projects can be built independently from `SmokeMods`.
