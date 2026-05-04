# M1 Setup — Bootstrap, World, and Player in one click

After Mirror is imported, all manual scene wiring is automated by an Editor menu item.

## Run the generator

In Unity:

**Steading → M1: Generate Bootstrap, World, and Player**

This creates:
- `Assets/_Project/Scenes/Bootstrap.unity` — has `NetworkBootstrap` (our `NetworkManager` subclass) + `KcpTransport` + `NetworkManagerHUD`, plus a `Main Camera`.
- `Assets/_Project/Scenes/World_Test.unity` — ground plane + directional light + `PlayerSpawn` (with `NetworkStartPosition`) + `Main Camera`.
- `Assets/_Project/Prefabs/Player.prefab` — capsule visual + `CharacterController` + `NetworkIdentity` + `NetworkTransformReliable` (client→server sync) + `PlayerInput` + `PlayerController`, with a `CameraPivot` child for the first-person camera.

Both scenes are added to **File → Build Settings**, with `Bootstrap` as scene 0.

## Smoke test (single machine, two windows)

1. Open `Assets/_Project/Scenes/Bootstrap.unity` (the generator opens it for you).
2. Press **Play**. The on-screen NetworkManagerHUD appears in the top-left.
3. Click **Host (Server + Client)**. The scene transitions to `World_Test`. A capsule appears — that's you. WASD to move, mouse to look, Shift to sprint, Space to jump.
4. Without stopping Play, build a Windows player: **File → Build And Run** → pick a folder.
5. In the built executable, click **Client**, leave the address as `localhost`, click **Connect**. A second capsule appears in both windows.
6. Move in either window — the other capsule should follow on the other side.

If the second capsule lags, that's the default `NetworkTransformReliable` send rate (10Hz). Adjust on the `Player` prefab if needed.

## Smoke test (Linux dedicated server in Docker)

1. **File → Build Settings → Dedicated Server → Linux**, output to `Server/build/Steading-Server.x86_64`.
2. From a terminal in `Server/`:
   ```sh
   docker compose up --build
   ```
3. In Unity, press **Play** → click **Client** → enter the server's IP (`localhost` if Docker on the same machine) → **Connect**.

## Troubleshooting

- **Console errors about `Mirror` namespace not found** → Mirror import incomplete. Re-import via **Assets → Reimport All**.
- **Capsule falls through ground** → Ground plane lives at y=0 with no collider thickness; CharacterController might spawn at y=0. Move the spawn up slightly: select `PlayerSpawn` and set position to `(0, 0.5, 0)`.
- **Camera doesn't follow** → The `OnStartLocalPlayer` reparent only happens on the local owner. If you Host'd in the Editor and then connect a remote Client, the remote will reparent its own Camera.main correctly; the host's editor camera won't move (that's expected).
- **No on-screen Host/Client buttons** → You're not in the Bootstrap scene, or the `NetworkManagerHUD` was removed.
