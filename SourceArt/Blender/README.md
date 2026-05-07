# Blender Source Art Pipeline

This folder is the source of truth for authored art. Unity should receive exported game assets, not raw procedural mesh replacements.

## Folder Layout

- `characters/`: rigged player and enemy `.blend` files.
- `weapons/`: sword, axe, shield, tools.
- `buildables/`: modular wall, floor, roof, pillar, doorway, palisade, tower pieces.
- `world/`: trees, rocks, logs, camp props, resource nodes.
- `exports/`: generated FBX files from Blender. These are copied or exported into `Assets/_Project/Art/Models/`.

## Scale And Orientation

- `1 Blender unit = 1 Unity meter`.
- Apply transforms before export: `Ctrl+A > Rotation & Scale`.
- Model forward should face Blender `-Y`; up is `Z`.
- Export FBX with `Forward: -Z Forward`, `Up: Y Up` for Unity.

## Naming

- Static mesh: `SM_Category_Name_Variant`, for example `SM_Build_Wall_A`.
- Skeletal mesh: `SK_Character_Name`, for example `SK_Draugr_A`.
- Armature: `ARM_Character_Name`.
- Collision helper mesh: `UCX_MeshName_01`.
- Socket empty: `SOCKET_RightHand`, `SOCKET_LeftHand`, `SOCKET_Back`.

## Export Steps

1. Open the `.blend`.
2. Put final game meshes in a collection named `EXPORT`.
3. Select the objects or collection to export.
4. Run `export_unity_fbx.py` from Blender's scripting tab.
5. Put generated FBX files under `Assets/_Project/Art/Models/`.
6. In Unity run `Steading > Art Pipeline > Apply Model Import Settings`.
7. Select imported model assets and run `Steading > Art Pipeline > Create Prefabs From Selected Models`.

## Generated Prototype Pack

`generate_steading_art_pack.py` creates the first authored prototype models as OBJ/MTL files in `SourceArt/Blender/exports/`:

- `SM_PlayerViking_Prototype`
- `SM_Draugr_Prototype`
- `SM_VikingWeapons_Prototype`
- `SM_BuildingKit_Prototype`
- `SM_WorldProps_Prototype`

When Blender is installed, run `build_steading_blend_from_exports.py` from Blender to import those exports, save `SteadingPrototypeArt.blend`, and export a combined FBX preview.

In Unity, run `Steading > Art Pipeline > Import Generated SourceArt Models` to copy those exports into `Assets/_Project/Art/Models/` and apply the model import settings.

## Character Assets

Use real rigs for:

- Idle
- Walk
- Run
- Light attack
- Heavy attack
- Shield bash
- Hit react
- Death

The Unity side should attach weapons to named hand sockets instead of procedural offsets.
