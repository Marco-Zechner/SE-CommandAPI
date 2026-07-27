# Mz.CommandAPI.Consumer

Typed consumer facade for the `MarcoZechner.CommandAPI` Space Engineers mod
API.

The facade owns discovery, exact endpoint validation, typed payload conversion,
rediscovery, registration cleanup, and disposal.

## Install with SELibs

From the consuming mod root:

```shell
    selibs add Mz.CommandAPI.Consumer
```

SELibs installs the facade as a direct package and resolves its exact
`Mz.ApiProtocol` and `Mz.SemanticVersioning` dependencies transitively. The
consumer package does not install CommandAPI provider implementation code or
`Mz.Networking`.

## Usage

Typical setup:

```csharp
    var client = new CommandApiClient(
        new SpaceEngineersModMessageBus(),
        "Example.Mod",
        "Example Mod",
        new SemanticVersion(1, 0, 0),
        true,
        "Registers Example Mod commands."
    );

    client.Register(
        new CommandRegistration(
            "/example",
            "ping",
            CommandExecutionLocation.Server,
            shortDescription: "Tests the Example Mod command."
        ),
        request =>
            new CommandResponse(
                true,
                "Pong",
                "Handled by Example Mod.",
                severity: CommandSeverity.Success
            )
    );

    client.Start();
```

Dispose the client during mod unload. Logical registrations remain pending while
CommandAPI is unavailable and are attached when a compatible provider appears.
