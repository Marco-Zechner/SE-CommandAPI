# CommandAPI

CommandAPI is a command framework for Space Engineers.

## Current runtime

The current development slice provides:

- vanilla chat input through `/cmd`;
- structured command results;
- case-insensitive command names and aliases;
- permission and execution-location validation;
- built-in `help`, `ping`, `whoami`, and `status` commands;
- requester identity and permission data derived from Space Engineers.

Remote-client request and response transport is not implemented yet. Server
commands currently work for single-player and the listen-server host.

RichHudChat is planned as a separate optional input and presentation provider.
CommandAPI does not contain RichHudFramework and has no Rich HUD Master runtime
dependency.

## In-game commands

- `/cmd help`
- `/cmd ping`
- `/cmd whoami`
- `/cmd status`

Malformed and unknown commands are suppressed from global chat and reported to
the requester through vanilla chat output.

## Build and test

From the mod root:

`dotnet test CommandAPI.slnx --nologo`

runs the xUnit test projects.

`dotnet build Data\CommandAPI.csproj --nologo`

builds the Space Engineers mod.
