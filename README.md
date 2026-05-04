# Steading (working title)

A 4–8 player co-op Viking survival game with two-way siege warfare.

- **Defense:** Named AI war-bands assault your settlement with real siege tactics — rams target gates under archer cover, breachers climb walls.
- **Offense (M5+):** Build longships, sail to procedural AI clan villages, raid them, and trigger reputation-driven retaliation.

Plan: see `C:\Users\AJW\.claude\plans\plan-out-a-stategy-virtual-nebula.md`.

## Stack

- **Unity 6 LTS** + **URP**
- **Mirror Networking** (MIT)
- **MessagePack-CSharp** for save format
- **Steamworks.NET** (M6)
- C# / .NET, Git + Git LFS

## Getting started

### Prerequisites
- Unity Hub
- Unity 6 LTS (install via Unity Hub)
- Git with Git LFS (`git lfs install` once per machine)
- Docker Desktop (for the dedicated server image)

### First-time setup
1. Install Unity 6 LTS via Unity Hub.
2. In Unity Hub, choose **Open** and point at this folder. Unity will populate `ProjectSettings/`, `Packages/manifest.json`, etc.
3. In the Package Manager, install:
   - Mirror (via Asset Store or `https://github.com/MirrorNetworking/Mirror.git`)
   - MessagePack-CSharp (via Package Manager git URL)
   - Input System
   - Universal RP
4. Open `Assets/_Project/Scenes/Bootstrap.unity` and press Play.

### Running the dedicated server
```sh
cd Server
docker compose up --build
```
The server listens on UDP 7777 by default.

### Connecting from a client
Build the client (File → Build Settings → Windows) and launch with the host IP. Direct-IP join screen accepts `host:port`.

## Layout

```
Assets/_Project/Scripts/
  Core/        # GameBootstrap, scene loader
  Net/         # Mirror NetworkManager subclass
  Player/      # PlayerController, PlayerInput
  Combat/      # Health, Stamina, Weapons
  AI/          # EnemyController, Squad, Roles, Archetypes
  Building/    # BuildController, Structure, IntegritySolver
  Raids/       # RaidDirector, ScoutEvent, SiegeStage
  Vehicles/    # Longship (M5)
  World/       # World streaming, ClanReputation (M6)
  WorldGen/    # AIVillageGenerator (M5)
  Persistence/ # WorldSaveService (M6)
  Loot/        # Loot tables
  UI/          # HUD, inventory, build menu
```

## Milestones

| # | Weeks | Goal |
|---|---|---|
| M1 | 3 | Two players moving on a Linux dedicated server |
| M2 | 4 | Combat + first enemy (Draugr) |
| M3 | 5 | ~40 placeable parts with structural integrity |
| M4 | 6 | Smart siege AI war-bands |
| M5 | 5 | Longships + procedural AI villages |
| M6 | 5 | Reputation, persistence, polish, Steam |

## Authority model

Server-authoritative for combat, structure damage, AI, world state, inventory. Clients send inputs and predict their own movement. World state saves to disk every 60 seconds and on graceful shutdown.
