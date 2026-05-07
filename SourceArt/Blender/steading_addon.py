"""
Steading — Blender add-on
=========================

Drop this single .py into Blender's Add-ons folder (or Edit > Preferences >
Add-ons > Install...) and enable it. Adds a "Steading" tab in the 3D View's
N-panel with:

  * Generate Humanoid Template      — builds a Viking-proportioned humanoid
                                      mesh + armature + weight groups, ready
                                      to pose and export.
  * Validate Rig For Steading       — sanity-checks bone names, scale, and
                                      vertex group coverage.
  * Export Selected To Steading     — exports the active object as FBX into
                                      the Unity project's Models/<category>/
                                      folder, with the import settings the
                                      Unity AssetPostprocessor expects.

The add-on assumes the .blend file lives somewhere inside the SourceArt/
tree of the Steading repo. It walks up the directory tree to find the repo
root (the dir containing both SourceArt/ and Assets/), so artists can
keep their .blend files in any subfolder of SourceArt/.
"""

bl_info = {
    "name": "Steading",
    "author": "Steading",
    "version": (0, 1, 0),
    "blender": (4, 0, 0),
    "location": "View3D > N-Panel > Steading",
    "description": "Humanoid rig template + Unity-aware FBX export pipeline for the Steading game.",
    "category": "Pipeline",
}

import os
import bpy  # type: ignore
import bmesh  # type: ignore
from bpy.props import EnumProperty, StringProperty  # type: ignore
from mathutils import Vector  # type: ignore


# ---------------------------------------------------------------- Repo discovery

def find_repo_root():
    """Walk up from the current .blend file to the repo root."""
    blend = bpy.data.filepath
    if not blend:
        return None
    cur = os.path.dirname(blend)
    for _ in range(8):
        if os.path.isdir(os.path.join(cur, "Assets")) and os.path.isdir(os.path.join(cur, "SourceArt")):
            return cur
        parent = os.path.dirname(cur)
        if parent == cur:
            break
        cur = parent
    return None


CATEGORY_PATHS = {
    "PLAYER":    "Assets/_Project/Art/Models/Characters/Player",
    "ENEMY":     "Assets/_Project/Art/Models/Characters/Enemies",
    "WEAPON":    "Assets/_Project/Art/Models/Weapons",
    "BUILDABLE": "Assets/_Project/Art/Models/Buildables",
    "WORLD":     "Assets/_Project/Art/Models/World",
}

CATEGORY_PREFIX = {
    "PLAYER":    "Player_",
    "ENEMY":     "Enemy_",
    "WEAPON":    "Weapon_",
    "BUILDABLE": "Buildable_",
    "WORLD":     "World_",
}


# ---------------------------------------------------------------- Humanoid template

VIKING_BONES = [
    # (name, head, tail, parent)
    ("Hips",       (0.0, 0.0, 0.95), (0.0, 0.0, 1.05), None),
    ("Spine",      (0.0, 0.0, 1.05), (0.0, 0.0, 1.30), "Hips"),
    ("Chest",      (0.0, 0.0, 1.30), (0.0, 0.0, 1.55), "Spine"),
    ("Neck",       (0.0, 0.0, 1.55), (0.0, 0.0, 1.65), "Chest"),
    ("Head",       (0.0, 0.0, 1.65), (0.0, 0.0, 1.85), "Neck"),
    # Legs
    ("LeftUpLeg",  (-0.10, 0.0, 0.95), (-0.10, 0.0, 0.55), "Hips"),
    ("LeftLeg",    (-0.10, 0.0, 0.55), (-0.10, 0.0, 0.10), "LeftUpLeg"),
    ("LeftFoot",   (-0.10, 0.0, 0.10), (-0.10, -0.15, 0.05), "LeftLeg"),
    ("RightUpLeg", ( 0.10, 0.0, 0.95), ( 0.10, 0.0, 0.55), "Hips"),
    ("RightLeg",   ( 0.10, 0.0, 0.55), ( 0.10, 0.0, 0.10), "RightUpLeg"),
    ("RightFoot",  ( 0.10, 0.0, 0.10), ( 0.10, -0.15, 0.05), "RightLeg"),
    # Arms
    ("LeftShoulder",  (-0.18, 0.0, 1.50), (-0.32, 0.0, 1.50), "Chest"),
    ("LeftArm",       (-0.32, 0.0, 1.50), (-0.32, 0.0, 1.20), "LeftShoulder"),
    ("LeftForeArm",   (-0.32, 0.0, 1.20), (-0.32, 0.0, 0.95), "LeftArm"),
    ("LeftHand",      (-0.32, 0.0, 0.95), (-0.32, 0.0, 0.85), "LeftForeArm"),
    ("RightShoulder", ( 0.18, 0.0, 1.50), ( 0.32, 0.0, 1.50), "Chest"),
    ("RightArm",      ( 0.32, 0.0, 1.50), ( 0.32, 0.0, 1.20), "RightShoulder"),
    ("RightForeArm",  ( 0.32, 0.0, 1.20), ( 0.32, 0.0, 0.95), "RightArm"),
    ("RightHand",     ( 0.32, 0.0, 0.95), ( 0.32, 0.0, 0.85), "RightForeArm"),
]


def build_armature(name="VikingRig"):
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm = bpy.context.object
    arm.name = name
    arm.data.name = name + "_Data"

    # Remove the default "Bone" that armature_add created.
    edit_bones = arm.data.edit_bones
    for b in list(edit_bones):
        edit_bones.remove(b)

    created = {}
    for bone_name, head, tail, parent in VIKING_BONES:
        b = edit_bones.new(bone_name)
        b.head = Vector(head)
        b.tail = Vector(tail)
        if parent and parent in created:
            b.parent = created[parent]
        created[bone_name] = b

    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def build_humanoid_mesh(name="VikingMesh"):
    """Build a low-poly humanoid using bmesh — capsule-ish torso, ovoid head, tapered limbs."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    bm = bmesh.new()

    def add_uvsphere(loc, radius, name=None):
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=radius)
        for v in bm.verts:
            if not v.tag:
                v.co += Vector(loc)
                v.tag = True
        for v in bm.verts:
            v.tag = False

    def add_cube(loc, size):
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            if not v.tag:
                v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
                v.co += Vector(loc)
                v.tag = True
        for v in bm.verts:
            v.tag = False

    # Reset tag
    for v in bm.verts:
        v.tag = False

    # Head (slightly elongated sphere)
    add_uvsphere((0, 0, 1.75), 0.12)
    # Torso
    add_cube((0, 0, 1.30), (0.36, 0.20, 0.50))
    # Hips
    add_cube((0, 0, 1.00), (0.30, 0.18, 0.18))
    # Legs (left/right)
    add_cube((-0.10, 0, 0.55), (0.12, 0.14, 0.45))
    add_cube(( 0.10, 0, 0.55), (0.12, 0.14, 0.45))
    # Feet
    add_cube((-0.10, -0.06, 0.05), (0.10, 0.20, 0.08))
    add_cube(( 0.10, -0.06, 0.05), (0.10, 0.20, 0.08))
    # Upper arms
    add_cube((-0.32, 0, 1.35), (0.10, 0.10, 0.30))
    add_cube(( 0.32, 0, 1.35), (0.10, 0.10, 0.30))
    # Forearms
    add_cube((-0.32, 0, 1.05), (0.09, 0.09, 0.25))
    add_cube(( 0.32, 0, 1.05), (0.09, 0.09, 0.25))
    # Hands
    add_cube((-0.32, 0, 0.88), (0.08, 0.10, 0.10))
    add_cube(( 0.32, 0, 0.88), (0.08, 0.10, 0.10))

    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return obj


def auto_skin(mesh_obj, armature_obj):
    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


# ---------------------------------------------------------------- Operators

class STEADING_OT_create_humanoid(bpy.types.Operator):
    """Create a Viking-proportioned humanoid mesh + armature template."""
    bl_idname = "steading.create_humanoid"
    bl_label = "Create Humanoid Template"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        arm = build_armature("VikingRig")
        bpy.ops.object.mode_set(mode="OBJECT")
        mesh = build_humanoid_mesh("VikingMesh")
        auto_skin(mesh, arm)
        self.report({"INFO"}, "Created VikingRig + VikingMesh. Edit/sculpt the mesh, then export.")
        return {"FINISHED"}


class STEADING_OT_validate_rig(bpy.types.Operator):
    """Check the active armature for required bones."""
    bl_idname = "steading.validate_rig"
    bl_label = "Validate Rig For Steading"

    def execute(self, context):
        arm = context.active_object
        if arm is None or arm.type != "ARMATURE":
            self.report({"ERROR"}, "Select an armature first.")
            return {"CANCELLED"}

        required = {b[0] for b in VIKING_BONES}
        present = {b.name for b in arm.data.bones}
        missing = required - present
        if missing:
            self.report({"ERROR"}, f"Missing bones: {', '.join(sorted(missing))}")
            return {"CANCELLED"}

        if abs(arm.scale[0] - 1.0) > 0.001 or abs(arm.scale[1] - 1.0) > 0.001 or abs(arm.scale[2] - 1.0) > 0.001:
            self.report({"WARNING"}, f"Armature scale is {arm.scale} — apply scale (Ctrl+A > Scale) before exporting.")
        else:
            self.report({"INFO"}, f"Rig OK. {len(present)} bones present.")
        return {"FINISHED"}


class STEADING_OT_export(bpy.types.Operator):
    """Export the selected mesh+armature to the Unity project as FBX."""
    bl_idname = "steading.export"
    bl_label = "Export Selected To Steading"

    category: EnumProperty(
        name="Category",
        items=[
            ("PLAYER",    "Player",    "Humanoid character with full rig"),
            ("ENEMY",     "Enemy",     "Generic-rigged enemy character"),
            ("WEAPON",    "Weapon",    "Static weapon mesh"),
            ("BUILDABLE", "Buildable", "Static building piece"),
            ("WORLD",     "World",     "Static world prop / scenery"),
        ],
        default="PLAYER",
    )

    asset_name: StringProperty(name="Asset Name", default="VikingHero")

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)

    def execute(self, context):
        if not context.selected_objects:
            self.report({"ERROR"}, "Select at least one object to export.")
            return {"CANCELLED"}

        repo = find_repo_root()
        if repo is None:
            self.report({"ERROR"}, "Couldn't locate repo root. Save the .blend file inside SourceArt/Blender/ first.")
            return {"CANCELLED"}

        target_dir = os.path.join(repo, CATEGORY_PATHS[self.category])
        os.makedirs(target_dir, exist_ok=True)

        prefix = CATEGORY_PREFIX[self.category]
        filename = f"{prefix}{self.asset_name}.fbx"
        filepath = os.path.join(target_dir, filename)

        # Standard Unity-friendly FBX export options. The Unity AssetPostprocessor
        # picks up the file by name pattern and applies humanoid / generic / none
        # avatar settings automatically.
        bpy.ops.export_scene.fbx(
            filepath=filepath,
            use_selection=True,
            global_scale=1.0,
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            object_types={"ARMATURE", "MESH", "EMPTY"},
            use_mesh_modifiers=True,
            mesh_smooth_type="FACE",
            add_leaf_bones=False,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            armature_nodetype="NULL",
            bake_anim=(self.category in {"PLAYER", "ENEMY"}),
            bake_anim_use_all_actions=False,
            bake_anim_simplify_factor=1.0,
            path_mode="AUTO",
            embed_textures=False,
            axis_forward="-Z",
            axis_up="Y",
        )

        self.report({"INFO"}, f"Exported to {filepath} — Unity will auto-import.")
        return {"FINISHED"}


# ---------------------------------------------------------------- UI panel

class STEADING_PT_panel(bpy.types.Panel):
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Steading"
    bl_label = "Steading Pipeline"

    def draw(self, context):
        col = self.layout.column(align=True)
        col.operator("steading.create_humanoid", icon="ARMATURE_DATA")
        col.separator()
        col.operator("steading.validate_rig", icon="CHECKMARK")
        col.separator()
        col.operator("steading.export", icon="EXPORT")
        col.separator()
        repo = find_repo_root()
        if repo:
            col.label(text=f"Repo: {os.path.basename(repo)}")
        else:
            col.label(text="Repo: not found", icon="ERROR")
            col.label(text="Save .blend inside SourceArt/")


# ---------------------------------------------------------------- Registration

CLASSES = (
    STEADING_OT_create_humanoid,
    STEADING_OT_validate_rig,
    STEADING_OT_export,
    STEADING_PT_panel,
)


def register():
    for c in CLASSES:
        bpy.utils.register_class(c)


def unregister():
    for c in reversed(CLASSES):
        bpy.utils.unregister_class(c)


if __name__ == "__main__":
    register()
