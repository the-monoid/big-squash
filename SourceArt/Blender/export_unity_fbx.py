import bpy
from pathlib import Path


EXPORT_COLLECTION = "EXPORT"
OUTPUT_DIR = Path(bpy.path.abspath("//exports"))


def ensure_output_dir():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)


def export_object(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    safe_name = obj.name.replace(" ", "_")
    output_path = OUTPUT_DIR / f"{safe_name}.fbx"

    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        apply_unit_scale=True,
        bake_space_transform=False,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
    )
    print(f"Exported {output_path}")


def export_collection():
    ensure_output_dir()
    collection = bpy.data.collections.get(EXPORT_COLLECTION)
    if collection is None:
        raise RuntimeError(f"Missing collection named {EXPORT_COLLECTION}")

    roots = [obj for obj in collection.objects if obj.parent is None and obj.type in {"MESH", "ARMATURE", "EMPTY"}]
    if not roots:
        raise RuntimeError(f"No root objects found in {EXPORT_COLLECTION}")

    for obj in roots:
        export_object(obj)


if __name__ == "__main__":
    export_collection()
