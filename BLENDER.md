# Blender pipeline

The Steading project ships with a Blender add-on, a naming convention, and a
Unity AssetPostprocessor that work together so you can model in Blender and have
the result drop straight into the game with the painterly shader applied — no
manual import settings, no material swapping, no rig configuration.

## One-time setup

### Blender

1. Get **Blender 4.0+** (4.2 LTS recommended).
2. Edit → Preferences → Add-ons → **Install...** → pick
   `SourceArt/Blender/steading_addon.py`.
3. Tick the box next to "Steading" in the add-on list to enable it.
4. Save Preferences.

You should now see a **Steading** tab in the 3D View's N-panel (press `N` to
toggle the panel).

### Working .blend files

Always save your `.blend` somewhere inside the repo's `SourceArt/` tree (e.g.
`SourceArt/Blender/Player/VikingHero.blend`). The add-on uses the `.blend` file
location to find the repo root and the Unity Models directory — if it can't
walk up to find both `SourceArt/` and `Assets/` siblings, the export button
errors out.

## Authoring loop

### 1. Create a new asset

In the 3D View's **Steading** panel:

| Button | What it does |
|---|---|
| **Create Humanoid Template** | Builds a Viking-proportioned mesh + 19-bone armature, parented with auto weights. Edit/sculpt the mesh, refine the rig. |
| **Validate Rig For Steading** | Sanity-checks bone names and scale on the active armature. Run this before exporting. |
| **Export Selected To Steading** | Opens a dialog asking for category + asset name, then writes an FBX into the right Unity folder. |

For static assets (weapons, buildings, world props) you don't need the rig —
just model the mesh and use the export button with the right category.

### 2. Naming convention

The Unity AssetPostprocessor keys off file-name prefixes:

| Prefix | Avatar | Animations | Used for |
|---|---|---|---|
| `Player_*` | Humanoid | imported | Player characters with full rig |
| `Enemy_*` | Generic | imported | Draugr, war-bands, bosses |
| `Weapon_*` | None | — | Swords, axes, bows, shields |
| `Buildable_*` | None | — | Walls, doors, furniture |
| `World_*` | None | — | Trees, rocks, scenery |

The export dialog adds the prefix automatically based on the category you
pick — don't add it yourself.

### 3. Export

Click **Export Selected To Steading**, choose **Category** and type an **Asset
Name** (e.g. "VikingHero"). The FBX lands at:

```
Assets/_Project/Art/Models/<category>/Player_VikingHero.fbx
```

Unity detects the file, applies the right ModelImporter settings via the
postprocessor, and creates an external materials folder beside it. Each
material is auto-retargeted to `Steading/PainterlyLit` so the model picks up
the banded lighting + rim glow that match the rest of the world.

### 4. Drop into the scene

Drag the imported model into a scene (or right-click → Create → Prefab). For
characters, you'll typically want to:

1. Drag into the scene, save as a prefab in `Assets/_Project/Prefabs/Art/`.
2. Add `NetworkIdentity`, `NetworkTransformReliable` (or `Hybrid`), `Health`,
   and the relevant controller (`PlayerController` or `EnemyController`).
3. Replace the runtime-procedural mesh in `Player.prefab` / `Draugr.prefab`
   with this prefab.

For static buildings/weapons:

1. Drag into the scene.
2. The `BuildableVisualEnhancer` on `Structure.cs` will detect imported meshes
   and skip its procedural geometry generation.

## Quick-start: generate a real Viking character

For artists who want a starting point instead of building from scratch:

1. Save a new `.blend` inside `SourceArt/Blender/Player/` (e.g.
   `VikingHero.blend`).
2. Open the **Scripting** workspace in Blender.
3. **Open Text Block** → pick `SourceArt/Blender/generate_player_blend.py`.
4. Click **Run Script**.

You'll get:
- `VikingMesh` — high-poly humanoid (~6 k vertices, smooth-shaded, heroic
  male proportions ~1.85m tall)
- `VikingRig` — 19-bone armature with the Steading naming convention
- Five basic materials (skin, tunic, hair, leather, iron)
- `Player_VikingHero.fbx` exported into the Unity project's
  `Assets/_Project/Art/Models/Characters/Player/`
- `.blend` saved beside the script's path so you have something to iterate on

Unity sees the FBX, the postprocessor configures it as a Humanoid avatar,
extracts materials, and retargets them to `Steading/PainterlyLit`. Drag
the imported model into a scene and you have a real character to replace
the procedural capsule.

Re-run the script after sculpting/editing in Blender to overwrite the FBX.

## What the add-on does NOT do

- **Texture painting / UV unwrapping** — those happen in Blender as normal. The
  postprocessor will pick up baked textures if they're in the same folder as
  the FBX.
- **Animation authoring** — bake animations to Actions in Blender, then export
  with `bake_anim` enabled (the add-on does this automatically for the Player
  and Enemy categories).
- **Custom shaders per material** — every imported material gets retargeted to
  `Steading/PainterlyLit`. To opt out, manually re-assign a different shader
  in Unity after import.

## Folder layout

```
SourceArt/
  Blender/
    steading_addon.py            <- the Blender add-on
    generate_steading_art_pack.py <- legacy procedural OBJ generator (kept for prototypes)
    export_unity_fbx.py           <- standalone bpy export script (advanced)
    Player/
      VikingHero.blend            <- example
    Enemies/
    Weapons/
    Buildables/
    World/

Assets/_Project/Art/Models/
  Characters/
    Player/      Player_*.fbx
    Enemies/     Enemy_*.fbx
  Weapons/        Weapon_*.fbx
  Buildables/     Buildable_*.fbx
  World/          World_*.fbx
```

## Troubleshooting

- **"Couldn't locate repo root."** — Save your `.blend` file inside
  `SourceArt/Blender/` (any subfolder). The add-on walks up looking for the
  `Assets/` and `SourceArt/` siblings.
- **Imported model is huge / tiny** — Apply scale before exporting
  (`Object → Apply → Scale` or `Ctrl+A` then **Scale**) and re-run the rig
  validator.
- **Material is pink in Unity** — `Steading/PainterlyLit` shader didn't load.
  Run **Steading → Art: Repair URP Render Pipeline** in Unity and reimport
  the asset.
- **Animations don't play** — Make sure your action is named (not "Action.001"
  scratch buffers). For Player_* assets, the avatar definition needs the
  Humanoid mapping verified in Unity (Inspector → Rig → Configure...).
