# CommandAPI

CommandAPI is a command framework for Space Engineers.

## Current runtime

The current development slice provides:

- vanilla chat input through `/cmd`;
- structured command results;
- case-insensitive command names and aliases;
- permission and execution-location validation;
- built-in `help`, `ping`, `whoami`, and `status` commands;
- requester identity and permission data derived from authoritative server state;
- correlated client-to-server command requests;
- requester-only server-to-client command results;
- listen-server and dedicated-server networking support.

All recognized commands are submitted to the authoritative server. The server
derives requester identity and permissions from the validated transport sender,
executes the command, and returns the structured result only to that requester.

RichHudChat is planned as a separate optional input and presentation provider.
CommandAPI does not contain RichHudFramework and has no Rich HUD Master runtime
dependency.

## Embedded networking layer

CommandAPI source-copies `Mz.Networking.Core` and
`Mz.Networking.SpaceEngineers` from SpaceEngineersLibrary. The copied revision
and provenance are recorded under
`Data/Scripts/CommandAPI/Libraries/Mz.Networking/SOURCE.md`.

Command traffic uses Space Engineers secure-message channel `31280`. The value
is the low 16 bits of FNV-1a over
`MarcoZechner.CommandAPI.Network.v1` and is part of the CommandAPI network
protocol assignment.

## In-game commands

- `/cmd help`
- `/cmd ping`
- `/cmd whoami`
- `/cmd status`

Malformed and unknown commands are suppressed from global chat and reported to
the requester through the active local presentation adapter.

## Build and test

From the mod root:

`dotnet test CommandAPI.slnx --nologo`

runs the xUnit test projects.

`dotnet build Data\CommandAPI.csproj --nologo`

builds the Space Engineers mod.
