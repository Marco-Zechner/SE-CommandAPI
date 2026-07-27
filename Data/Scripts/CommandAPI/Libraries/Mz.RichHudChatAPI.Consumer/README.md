# Mz.RichHudChatAPI.Consumer

Typed consumer facade for the `MarcoZechner.RichHudChatAPI` Space Engineers mod API.

The facade owns discovery, exact API 1.4.0 endpoint validation, typed payload conversion, participant and route registration, rediscovery, cleanup, and disposal.

## Install with SELibs

From the consuming mod root:

    selibs add Mz.RichHudChatAPI.Consumer

SELibs installs the facade as a direct package and resolves its exact `Mz.ApiProtocol` and `Mz.SemanticVersioning` dependencies transitively. The package does not install RichHudChatAPI provider implementation code or Rich HUD Framework.

## Usage

Create one client for the consuming mod, register its participant and routes, then start discovery:

    var client = new RichHudChatApiClient(
        new SpaceEngineersModMessageBus(),
        "Example.Mod",
        "Example Mod",
        new SemanticVersion(1, 0, 0),
        false,
        "Uses RichHudChatAPI for chat input."
    );

    ChatParticipantHandle participant =
        client.RegisterParticipant(
            new ChatParticipantRegistration(
                replacesVanillaChat: false
            )
        );

    participant.RegisterRoute(
        new ChatRouteRegistration(
            "commands",
            "/example",
            "/",
            "Example commands"
        ),
        input => HandleSubmittedInput(input),
        input => UpdateSuggestions(input),
        action => HandleCompanionAction(action)
    );

    client.Start();

A client owns one participant identity, and that participant may register multiple routes. Logical registrations remain pending while RichHudChatAPI is unavailable and reactivate when a compatible provider appears.

Dispose the client during mod unload.
