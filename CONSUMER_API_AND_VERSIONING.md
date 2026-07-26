# CommandAPI consumer package and versioning

This document records the intended CommandAPI versioning, compatibility, and
consumer-package design before implementation.

## Goals

CommandAPI should provide:

- an installed provider mod with its own semantic version and changelog;
- a separately versioned public CommandAPI contract;
- a distributable typed consumer facade outside the provider mod's `Data`
  directory;
- a SELibs package that installs the facade and only the dependencies required
  by consumers;
- runtime compatibility decisions made by the current provider rather than by
  a stale hardcoded future-version ceiling in an old consumer;
- changelogs represented as C# data so they can be read and displayed in Space
  Engineers.

Markdown changelog files are not the primary source of version history.

## Repository boundary

The provider mod remains beneath:

    Data/Scripts/CommandAPI/

The consumer facade lives beneath:

    Consumer/Mz.CommandAPI.Consumer/

`Consumer` is outside `Data`, so Space Engineers does not compile the consumer
facade into the CommandAPI provider mod.

The intended high-level layout is:

    Data/
      Scripts/
        CommandAPI/
          ModVersionFile.cs
          ApiVersionFile.cs
          Api/
          Chat/
          Core/
          Networking/
          Libraries/

    Consumer/
      Mz.CommandAPI.Consumer/
        ApiVersionFile.cs
        CommandApiClient.cs
        CommandRegistration.cs
        CommandRequest.cs
        CommandResponse.cs
        CommandExecutionLocation.cs
        CommandSeverity.cs
        README.md

Provider-only implementation and dependencies must not leak into the consumer
package.

## Mod version

`Data/Scripts/CommandAPI/ModVersionFile.cs` defines the installed CommandAPI mod
release.

It contains:

- `Major`, `Minor`, and `Patch`;
- `VersionString`;
- a `Mz.SemanticVersioning.Changelog` ordered newest to oldest.

The mod version is used for:

- CommandAPI diagnostics and `/cmd status`;
- the provider mod identity published through ApiProtocol;
- CommandAPI's consumer identity when CommandAPI consumes another mod API;
- mod release tags and release notes.

The provider identity must not contain a separate hardcoded version.

The intended release tag form is:

    release/CommandAPI/<major.minor.patch>

## Provider API version

`Data/Scripts/CommandAPI/ApiVersionFile.cs` defines the public CommandAPI
contract exposed by the provider.

It contains:

- the current provider API version;
- an API changelog represented by `Mz.SemanticVersioning.Changelog`;
- provider-owned compatibility policy for consumer facade/API-client versions.

The current API version is published as `ApiDescriptor.Version`.

The API version changes when the public contract changes, including:

- endpoint names;
- endpoint delegate signatures;
- required or optional payload fields;
- result fields;
- lifecycle guarantees;
- compatibility behavior.

The API version is independent from the mod version. A mod bug fix may change
the mod version while leaving the API version unchanged.

## Consumer facade API version

`Consumer/Mz.CommandAPI.Consumer/ApiVersionFile.cs` defines the distributed
consumer facade/API-client version.

The filename is `ApiVersionFile.cs`, not `LibraryVersionFile.cs`, because this
is the CommandAPI consumer API facade even though SELibs distributes it.

It contains:

- `Major`, `Minor`, and `Patch`;
- `VersionString`;
- the consumer facade changelog represented by
  `Mz.SemanticVersioning.Changelog`.

The facade version is also the SELibs package release version.

The initial package ID is:

    Mz.CommandAPI.Consumer

The intended release tag form is:

    release/Mz.CommandAPI.Consumer/<major.minor.patch>

The package version may diverge from the provider API version. For example, a
facade implementation fix may release consumer package `1.1.1` while still
communicating with provider API `1.1.0`.

## Compatibility ownership

The provider should make the final compatibility decision.

An old consumer facade must not permanently reject a newer compatible provider
only because the newer version did not exist when that consumer was released.

The intended handshake is:

1. The consumer identifies its mod using that consuming mod's mod version.
2. The consumer also reports the version of
   `Mz.CommandAPI.Consumer/ApiVersionFile.cs`.
3. The provider reports its current public API version.
4. The provider evaluates the consumer facade version against the compatibility
   policy in the provider's `ApiVersionFile.cs`.
5. The provider accepts or rejects the connection and returns an explicit
   compatibility result.
6. The facade validates that required endpoints and delegate types are present
   before becoming ready.

The consuming mod version and consumer facade version are different values:

- consuming mod version identifies the downstream mod;
- consumer facade version identifies the CommandAPI client contract used by
  that mod.

The current ApiProtocol request only carries the consuming mod identity and its
API requirement. Supporting provider-owned facade compatibility may therefore
require an explicit CommandAPI handshake endpoint or an ApiProtocol extension.
This must be implemented deliberately rather than overloading the consuming
mod version.

Endpoint validation remains necessary even after version compatibility succeeds.

## Provider dependencies

The provider mod currently depends on:

    Mz.ApiProtocol 0.2.2
    Mz.Networking 0.1.2

`Mz.SemanticVersioning 0.1.1` is installed transitively.

`Mz.Networking` is an implementation detail of the CommandAPI provider. It is
not a consumer dependency.

Provider dependencies remain declared in the root `selibs.json` and installed
under:

    Data/Scripts/CommandAPI/Libraries/

## Consumer package dependencies

The consumer facade package should declare only:

    Mz.ApiProtocol 0.2.2

`Mz.SemanticVersioning` is then installed transitively through
`Mz.ApiProtocol`.

The consumer package owns exactly one SELibs folder:

    Libraries/Mz.CommandAPI.Consumer/

A consuming mod installs the complete facade graph with:

    selibs add Mz.CommandAPI.Consumer

The resulting dependency graph is:

    Mz.CommandAPI.Consumer    direct
    Mz.ApiProtocol            transitive
    Mz.SemanticVersioning     transitive

`Mz.Networking` must not be installed into consumer mods unless their own code
also requests it.

## Typed facade responsibilities

The production facade is not the current smoke-test consumer.

The facade should hide raw ApiProtocol and dictionary transport details behind
typed CommandAPI concepts.

The facade owns:

- provider discovery;
- compatibility negotiation;
- endpoint lookup and delegate validation;
- connection, disconnection, and readiness state;
- rediscovery;
- typed registration metadata;
- typed command requests and responses;
- translation to and from the stable BCL-only endpoint payloads;
- registration cleanup;
- disposal and unload cleanup;
- diagnostic error state.

Consumers should not need to work directly with:

- `ApiDiscoveryConsumer`;
- API IDs;
- endpoint names;
- endpoint delegate dictionaries;
- `IDictionary<string, object>` request payloads;
- `IDictionary<string, object>` response payloads.

## Network protocol version

CommandAPI currently has an internal command-network wire version.

A separate `NetworkProtocolVersionFile.cs` is deferred.

Different versions of the same CommandAPI mod should not normally coexist in
one game, and the current wire decoder already rejects an unsupported wire
version.

A dedicated network protocol version file should be added only when the
protocol needs its own visible compatibility policy or changelog. Network
protocol changes are still recorded in the mod changelog.

## Current inconsistencies to remove

The current implementation contains several duplicated version values:

- `CommandApiSession` reports mod version `0.2.0`;
- `CommandApiProvider` publishes provider identity version `0.1.0`;
- `CommandApiProvider` publishes API descriptor version `1.1.0`;
- `CommandApiSession` reports protocol version `2.0.0`;
- `CommandMessageCodec` uses wire byte `2`.

The provider identity version is already stale.

Implementation must replace duplicate mod and API literals with the appropriate
version files and update tests to enforce the single sources of truth.

## Implementation slices

### Slice 1: provider version files

- Add `ModVersionFile.cs`.
- Add provider `ApiVersionFile.cs`.
- Add code-based changelogs.
- Replace duplicated mod and API version literals.
- Update diagnostics and tests.

### Slice 2: typed consumer facade

- Add `Consumer/Mz.CommandAPI.Consumer/`.
- Add consumer `ApiVersionFile.cs`.
- Add typed request, result, registration, and client classes.
- Add focused facade tests.
- Replace smoke-consumer protocol duplication where practical.

### Slice 3: compatibility handshake

- Transmit both consuming mod version and consumer facade version.
- Make the provider evaluate compatibility.
- Return explicit incompatibility information.
- Preserve endpoint contract validation.
- Test provider-first, consumer-first, compatible, obsolete, and future facade
  cases.

### Slice 4: SELibs publication

- Add CommandAPI consumer-package bundle tooling.
- Publish checksum-verified component and package manifest assets.
- Declare exact `Mz.ApiProtocol` dependency.
- Add the package route to the central SELibs registry.
- Verify installation into a clean consumer mod.
- Publish `release/Mz.CommandAPI.Consumer/<version>`.

### Slice 5: mod releases

- Add CommandAPI mod release tooling.
- Validate the release tag against `ModVersionFile.cs`.
- Generate release notes from the code changelog.
- Publish `release/CommandAPI/<version>`.
