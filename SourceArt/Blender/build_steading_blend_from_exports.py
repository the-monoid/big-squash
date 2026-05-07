from pathlib import Path

import bpy


ROOT = Path(bpy.path.abspath("//"))
EXPORTS = ROOT / "exports"
BLEND_OUT = ROOT / "SteadingPrototypeArt.blend"
FBX_OUT = ROOT / "exports" / "SteadingPrototypeArt.fbx"


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def import_obj(path: Path, x_offset: float):
    before = set(bpy.context.scene.objects)
    if hasattr(bpy.ops.wm, "obj_import"):
        bpy.ops.wm.obj_import(filepath=str(path))
    else:
        bpy.ops.import_scene.obj(filepath=str(path))
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    for obj in imported:
        obj.location.x += x_offset
    return imported


def set_origins_and_shading(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        if obj.type == "MESH":
            bpy.ops.object.shade_smooth()
    bpy.ops.object.select_all(action="DESELECT")


def main():
    clear_scene()
    obj_files = sorted(EXPORTS.rglob("*.obj"))
    if not obj_files:
        raise RuntimeError("No OBJ exports found. Run generate_steading_art_pack.py first.")

    all_imported = []
    for index, path in enumerate(obj_files):
        all_imported.extend(import_obj(path, index * 5.0))

    set_origins_and_shading(all_imported)

    light_data = bpy.data.lights.new("Key_Light", type="AREA")
    light_data.energy = 500
    light_data.size = 5
    light = bpy.data.objects.new("Key_Light", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (0, -5, 6)

    camera_data = bpy.data.cameras.new("Preview_Camera")
    camera = bpy.data.objects.new("Preview_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (6, -9, 5)
    camera.rotation_euler = (1.1, 0.0, 0.62)
    bpy.context.scene.camera = camera

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_OUT),
        use_selection=False,
        apply_unit_scale=True,
        object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
    )
    print(f"Saved {BLEND_OUT}")
    print(f"Exported {FBX_OUT}")


if __name__ == "__main__":
    main()
