# AGENTS.md

Architecture recap for the **PartyGame** project. Kept intentionally small — the
project is still a scaffold. Expand this file as real systems land.

## What this is

A multiplayer party game built in **Unity 6** (`6000.4.6f1`). Networking is the
core focus: the project is wired for online multiplayer from the start.

## Tech stack

- **Engine**: Unity 6 (`6000.4.6f1`)
- **Networking**: Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.13)
  over Unity Transport
- **Online services**: Unity Gaming Services — Multiplayer (lobby/relay/matchmaker)
  and Vivox (voice chat)
- **Tweening / animation**: DOTween (`Assets/Plugins/Demigiant/DOTween`)
- **UI**: uGUI (`com.unity.ugui`)
- **IDE**: Visual Studio integration

## Layout

```
Assets/
  NGO_Minimal_Setup/      # Starter networking scene + player prefab
    NGO_Setup.unity       # Current (only) scene
    PlayerPrefab.prefab
    NetworkPrefabsList.asset
  Plugins/Demigiant/DOTween/   # Third-party (do not edit)
  Resources/              # DOTweenSettings.asset
  DefaultNetworkPrefabs.asset  # NGO network prefab registry
Packages/manifest.json    # Package dependencies
ProjectSettings/          # Unity project config
```

The dozens of `*.csproj` / `*.slnx` files at the repo root are Unity-generated
IDE projects — never hand-edit them.

## Status

Core flow is in place: main menu → host/join lobby (Unity Sessions + Relay) →
host launches a minigame scene over NGO → minigame runs and returns everyone to
the lobby. One reference minigame ships: **TagPlatformer** (a 2D tag/hot-potato
platformer).

## Minigame framework

Every minigame is a **separate scene** loaded over the network by the host. A
shared base class owns the common flow so each game only writes its own rules.

### Naming convention

The first game was prototyped as `TagPlatformer`, then its per-game files were
renamed to a numeric id (`001`) for a faster copy-paste workflow on the next
games. The two naming styles currently coexist:

- **Numbered**, per-game gameplay/UI files: `Controller_001`, `PlayerController_001`,
  `UI_001`, under `Gameplay/001/` — copy these as the starting point for game `002`.
- **Named**, framework-level references: enum `E_MiniGame.TagPlatformer = 001`,
  scene `MiniGame_TagPlatformer`, namespace `Game.TagPlatformer`.

### Reusable pieces (write once, every game gets them)

- **`MiniGameController`** (`Gameplay/MiniGameController.cs`) — abstract
  `NetworkBehaviour` base. Server-authoritative state machine
  (`WaitingForPlayers → Playing → Finished → ReturningToLobby`) synced via
  `NetworkVariable`. Waits for every client to load the scene, runs an optional
  timer (`_gameDuration`, 0 = no timer), then returns to the lobby after
  `_returnToLobbyDelay`. Exposes `State`, `TimeRemaining`,
  `ReturnCountdownRemaining`, and `GameStarted` / `GameFinished` events. Hooks for
  subclasses: `OnGameStarted()` (optional) and `OnGameFinished()` (required).
  Server can also end early with `FinishGame()`.
- **`UI_Timer`** (`UI/MiniGames/UI_Timer.cs`) — drop-in label that shows the play
  timer, then the return-to-lobby countdown. Assign the controller + a TMP label,
  no per-game code.
- **`PlayerColors`** (`DataManagement/PlayerColors.cs`) — shared palette indexed by
  id; the index is persisted (PlayerPrefs `PreferencesManager.playerColor_PlayerPrefKey`)
  and synced. Never reorder/remove entries.
- **`SessionManager.StartMiniGame(E_MiniGame)`** / **`ReturnToLobby()`** — host-only
  network scene loads. `GameDataRegistry` maps each `E_MiniGame` to its scene name.

### TagPlatformer (001) — reference implementation

- **`Controller_TagPlatformer`** (`Gameplay/001/Controller_001.cs`) — overrides
  `OnGameStarted` to (server-only) `InstantiateAndSpawn` one `_playerPrefab` per
  connected client at a spawn point, owned by that client, then tags one random
  player. `OnGameFinished` is the wrap-up hook (currently a TODO).
- **`PlayerController_001`** (`Gameplay/001/PlayerController_001.cs`) — owner-authoritative
  2D movement (run + jump + single wall-jump) driving a `Rigidbody2D`; replicated
  by NetworkTransform + NetworkRigidbody2D. Holds the server-authoritative
  `_isTagged` flag (tag boosts speed, shows a head sprite, has an immunity window
  to stop instant ping-pong). Tag transfer is requested by the owner on
  `OnCollisionEnter2D` and validated server-side via a `ServerRpc`. Facing and
  lobby color index are owner-written `NetworkVariable`s. On `GameFinished` inputs
  freeze and whoever holds the tag plays an explosion (it loses).
- **`TriggerSensor`** (`Gameplay/001/TriggerSensor.cs`) — child trigger that counts
  overlaps on a layer (ignoring own body); used as the foot (ground) and side
  (wall) sensors that gate jumping.
- **`OwnerNetworkAnimator`** (`Gameplay/001/OwnerNetworkAnimator.cs`) — `NetworkAnimator`
  with `OnIsServerAuthoritative() => false` so the owning client drives animation,
  matching the owner-authoritative movement.

### Adding a new minigame (e.g. 002)

1. Copy `Gameplay/001/` → `Gameplay/002/`, rename classes to `_002`. Subclass
   `MiniGameController` for the new game's rules.
2. Build the minigame scene (`MiniGame_<Name>`), put the controller in it, wire
   `UI_Timer` if you want the timer label.
3. Add the enum value + scene constant + dictionary entry in
   `E_MiniGame` / `GameDataRegistry`, and add the scene to Build Settings.
4. Register the player prefab in the NGO network prefabs list.

## Conventions

See `CLAUDE.md` for working guidelines. Key points: code identifiers and comments
in English, commit messages in French. Verification is Play Mode behaviour, not
unit tests.

### Naming

- **Private fields**: underscore + camelCase, e.g. `private int _myInt`. Applies
  to `[SerializeField] private` fields too.
