# Unity setup

Step-by-step for getting this project running on a fresh machine.

## 1. Install Unity Hub + Unity 6.0 LTS

1. Download **Unity Hub** from <https://unity.com/download>.
2. In Unity Hub → **Installs** → **Install Editor**.
3. Pick **Unity 6.0 LTS — `6000.0.74f1`** (newer LTS patches are fine; non-LTS feature releases like `6000.4.x` are not).
4. When prompted for modules, include:
   - **Linux Dedicated Server Build Support** (required for the Docker server).
   - **Windows IL2CPP** (optional, used for shipping client builds).
   - Documentation (optional).

The IDE side (Visual Studio or Rider) is your call — the project includes asmdefs, so any C# editor works.

## 2. Clone the repo

```sh
git clone https://github.com/the-monoid/big-squash.git
cd big-squash
git lfs install   # one-time per machine
git lfs pull      # pulls binary assets that LFS pointers refer to
```

If you're on Windows and `git` isn't installed, get it from <https://git-scm.com/download/win>.

## 3. Open the project

In Unity Hub:
1. Click **Open** (NOT "New project" — see [gotchas](#gotchas)).
2. Pick the cloned `big-squash` folder.
3. Unity will open and resolve packages. First open takes 1–3 minutes:
   - URP 17.0.4
   - Input System 1.11.2
   - AI Navigation 2.0.6
   - Newtonsoft Json 3.2.1 (transitive Mirror dep)
   - IDE plugins, Test Framework, Timeline

The Console may show pink-magenta error materials briefly during the first import — they should resolve once URP finishes setting up.

## 4. Verify Mirror is in place

Mirror Networking 96.0.1 is **vendored** under `Assets/Mirror/` (committed via Git LFS). It does not need to be reinstalled.

If `Assets/Mirror/` is missing or empty:
```sh
git lfs pull
```
should fix it.

If you ever need to upgrade Mirror, do not use a UPM git URL — that approach doesn't work for this project (Mirror's repo lacks a clean `package.json`). Instead:
1. Delete `Assets/Mirror/`.
2. Import the latest `.unitypackage` from <https://github.com/MirrorNetworking/Mirror/releases> via **Assets → Import Package → Custom Package**.
3. Commit the new tree.

## 5. Run the milestone generators (in order)

Three Editor menu items create the playable scenes, prefabs, and wiring. Run them in order from the **Steading** top-bar menu:

| Menu item | What it does |
|---|---|
| **Steading → M1: Generate Bootstrap, World, and Player** | Creates `Bootstrap.unity`, `World_Test.unity`, `Player.prefab`. Wires the NetworkManager. Adds both scenes to Build Settings. |
| **Steading → M2: Generate Combat (Health, Draugr, NavMesh)** | Adds Health/PlayerAttack/PlayerRespawn to Player. Creates `Draugr.prefab`. Bakes the NavMesh on the ground. Drops a 3-Draugr `EnemySpawner` in `World_Test`. |
| **Steading → M3: Generate Building System (4 buildables, NavMesh-aware)** | Creates `Wall.prefab`, `Floor.prefab`, `Pillar.prefab`, `Doorway.prefab`. Adds `BuildController` + `BuildHud` to Player. Registers all four in `NetworkBootstrap.spawnPrefabs`. |

After each menu finishes, a confirmation dialog opens, and `Bootstrap.unity` is reopened so you can press **Play**.

> The menus refuse to run while Play mode is active and prompt to save dirty scenes before regenerating. They are idempotent — re-running them rebuilds the same outputs.

## 6. Smoke test

1. Open `Assets/_Project/Scenes/Bootstrap.unity` if it isn't already.
2. Press **Play**.
3. In the Game view, the on-screen NetworkManagerHUD shows in the top-left. Click **Host (Server + Client)**.
4. World_Test loads. You spawn as a capsule. Three Draugr appear in a ring.

### Controls

| Input | Action |
|---|---|
| WASD | Move |
| Mouse | Look (third-person camera) |
| Shift | Sprint |
| Space | Jump |
| Left-click | Attack (when not in build mode) |
| **B** | Toggle build mode |
| Tab (in build) | Cycle Wall → Floor → Pillar → Doorway |
| R (in build) | Rotate placement +90° |
| Left-click (in build) | Place |
| Right-click (in build) | Delete an existing structure |

You should be able to:
- Kill a Draugr (3 left-clicks at 25 dmg each — they have 60 HP).
- Die yourself and respawn 2 seconds later at PlayerSpawn.
- Build a 4-wall hut with a doorway and watch Draugr path around walls and through the doorway.

## 7. Build a dedicated Linux server (optional, for multi-machine play)

1. **File → Build Settings**.
2. **Platform**: Dedicated Server.
3. **Target**: Linux.
4. **Build** → output to `Server/build/`.
5. From a terminal in `Server/`:
   ```sh
   docker compose up --build
   ```
6. Connect from a built client to the server's IP on UDP 7777.

See `Server/README.md` for CLI flags.

## Gotchas

- **Don't click "New project" in Unity Hub.** That creates a duplicate Unity project under a `My project/` subfolder and won't pick up the existing `Assets/` and `ProjectSettings/`. Always click **Open** and point at the repo root.
- **LTS only.** This project targets `6000.0.x`. Newer feature releases (`6000.4.x` etc.) will offer to upgrade the project on first open — say no.
- **MessagePack is deferred to M6.** The OpenUPM build of `com.neuecc.messagepack` references `Microsoft.NET.StringTools` which is not bundled, causing CS0234 + Burst compile failures. Don't add it back without testing — alternatives are the `.unitypackage` from MessagePack's GitHub releases, `com.unity.nuget.newtonsoft-json` (already pulled in transitively), or plain JSON.
- **Steading menu missing?** The editor scripts didn't compile. Open **Window → General → Console** for the first red error.
- **HUD invisible during Play?** You're probably looking at the Scene view, not the Game view. Switch tabs above the main viewport.

## Where things live

```
Assets/
  _Project/
    Scenes/      Bootstrap.unity, World_Test.unity
    Prefabs/     Player.prefab, Draugr.prefab, Wall/Floor/Pillar/Doorway.prefab
    Art/         Materials (CapsulePill, BuildableWood, BuildableStone, ghosts)
    Scripts/
      Core/      GameBootstrap
      Net/       NetworkBootstrap (Mirror NetworkManager subclass)
      Player/    PlayerController, PlayerInput
      Combat/    Health, Stamina (TBD), DamageInfo, PlayerAttack, PlayerRespawn
      AI/        EnemyController, EnemySpawner, Archetypes/Draugr
      Building/  BuildController, BuildableEntry, Structure, BuildHud
      Editor/    M1Setup, M2Setup, M3Setup (menu generators)
  Mirror/        Vendored Mirror Networking 96.0.1
Server/          Linux dedicated server Docker config
ProjectSettings/ Unity-managed project config
Packages/        Package manifest + lock
```
