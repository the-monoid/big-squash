# Dedicated server

Linux-only headless build. Runs in Docker for parity with the production VPS.

## Build flow

1. In Unity (Windows or Mac dev machine):
   - File -> Build Settings -> select **Dedicated Server** platform.
   - Target: **Linux**.
   - Build to `Server/build/` (output executable: `Steading-Server.x86_64`).
2. From this directory:
   ```sh
   docker compose up --build
   ```

The `world_data/` volume holds save files and persists across container restarts.

## CLI flags

The server entrypoint forwards Unity standard flags plus our own:

| Flag | Default | Meaning |
|---|---|---|
| `-port <n>` | 7777 | UDP port |
| `-world <name>` | `default` | World save slot |
| `-maxplayers <n>` | 8 | Connection cap |
| `-tickrate <hz>` | 30 | Server simulation rate |

## Logs

Container logs are mirrored to `./logs/` via the bind volume. Server-side errors and raid director events log here.
