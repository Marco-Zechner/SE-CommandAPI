# CommandAPI

CommandAPI is a command framework and registration API for Space Engineers.

The current basic runtime is intentionally independent of RichHudChatAPI. Command input and result presentation use vanilla Space Engineers chat. Rich HUD integration can be designed again later without being part of the command core.

## Current runtime

CommandAPI supports:

- multiple top-level command prefixes such as `/cmd` or `/config`;
- command names and aliases scoped within each prefix;
- shared prefixes, with collisions enforced at command and alias level;
- client-local, authoritative-server, either-side, and internal execution locations;
- local execution for `Client` and normal `Either` submissions;
- authoritative networking for `Server` submissions;
- requester-only structured command results;
- vanilla chat interception on clients;
- external command registration on clients, listen servers, and dedicated servers.

Unknown prefixes remain ordinary chat. A registered prefix stops being intercepted when its final command is unregistered.

## Built-in commands

CommandAPI owns `/cmd`:

- `/cmd help`
- `/cmd ping`
- `/cmd whoami`
- `/cmd status`

Help lookup and usage output are scoped to the prefix used for the request.

## Consumer API

Mods should consume CommandAPI through the typed `Mz.CommandAPI.Consumer` package rather than interacting with ApiProtocol dictionaries directly.

Install it from the consuming mod root with:

    selibs add Mz.CommandAPI.Consumer

The consumer facade owns provider discovery, endpoint validation, rediscovery, registration cleanup, typed registration metadata, typed command requests, and typed command responses.

A typical registration specifies:

- owner ID;
- top-level prefix;
- canonical command name and optional aliases;
- execution location;
- help and usage metadata;
- permission requirement;
- typed handler.

The provider publishes the BCL-only `RegisterCommand` endpoint internally. The current public provider API version is defined by `Data/Scripts/CommandAPI/ApiVersionFile.cs`.

External mods must register on every peer where they are loaded. CommandAPI publishes its provider locally on every peer so client and server handlers are available in the correct process.

## Prefix collision policy

Prefixes are shared namespaces. Different mods may register different command names under the same prefix.

For example, one mod may own `/config reload` while another owns `/config export`. Registering the same canonical name or a colliding alias under the same prefix fails. The same command name may exist under different prefixes.

## Build and test

From the mod root:

    dotnet test CommandAPI.slnx --nologo
    dotnet build Data\CommandAPI.csproj --nologo

Consumer package format is verified with:

    powershell -NoProfile -ExecutionPolicy Bypass -File .github\tests\New-ConsumerReleaseBundle.Tests.ps1
