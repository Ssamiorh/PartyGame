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

Essentially empty: packages installed, DOTween imported, and a minimal NGO setup
scene/prefab. No gameplay code yet. The first custom scripts will define the
actual game architecture (game modes, networking flow, UI screens).

## Conventions

See `CLAUDE.md` for working guidelines. Key points: code identifiers and comments
in English, commit messages in French. Verification is Play Mode behaviour, not
unit tests.

### Naming

- **Private fields**: underscore + camelCase, e.g. `private int _myInt`. Applies
  to `[SerializeField] private` fields too.
