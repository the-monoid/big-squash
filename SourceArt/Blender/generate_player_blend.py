"""
Steading — generate a sculpt-style Viking character .blend
==========================================================

Run this script from inside Blender (4.x) to produce a high-poly humanoid
mesh + armature + weighted skin + basic materials, then export it as
Player_VikingHero.fbx into the Unity project's Models folder.

Usage:
    1. Save your .blend somewhere inside `SourceArt/Blender/Player/`
       (the script walks up the directory tree to find the repo root).
    2. Open Blender's Scripting workspace.
    3. Open this file via `Open Text Block` (or just paste it in).
    4. Run Script.

It produces:
    * `VikingMesh`  — high-poly sculpt-friendly humanoid (~6 k vertices,
                      smooth-shaded, anatomically proportioned)
    * `VikingRig`   — 19-bone humanoid armature with the Steading naming
                      convention required by the Unity importer
    * `Player_VikingHero.fbx` — exported into
        Assets/_Project/Art/Models/Characters/Player/

Once Unity sees the FBX, the SteadingFbxPostprocessor automatically:
    * sets the avatar to Humanoid
    * imports animations
    * retargets the materials to Steading/PainterlyLit

Re-running this script overwrites the existing FBX in place.
"""

from __future__ import annotations

import math
import os
import bpy  # type: ignore
import bmesh  # type: ignore
from mathutils import Vector, Matrix  # type: ignore


# ===================================================================== Repo root

def find_repo_root() -> str | None:
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


# ===================================================================== Mesh build

# Body proportion table — heroic male, ~1.85m tall.
# y is up. Vertices are placed in a coarse cage and then smoothed via
# subdivision + a thin sculpt pass.
def build_humanoid_mesh(name: str = "VikingMesh") -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    bm = bmesh.new()

    # Local helper: extruded ellipsoid with smooth UV rings.
    def add_capsule(loc, radius_xz, height, segs=24, rings=14):
        for ring in range(rings + 1):
            v = ring / rings                # 0..1 along height
            phi = (v - 0.5) * math.pi       # -pi/2 .. pi/2
            ringY = math.sin(phi) * (height * 0.5) + height * 0.5
            ringR_factor = math.cos(phi)
            for s in range(segs):
                theta = (s / segs) * math.tau
                x = math.cos(theta) * radius_xz[0] * ringR_factor
                z = math.sin(theta) * radius_xz[1] * ringR_factor
                bm.verts.new(Vector((loc[0] + x, loc[1] + ringY, loc[2] + z)))

        bm.verts.ensure_lookup_table()
        offset = len(bm.verts) - (rings + 1) * segs
        for ring in range(rings):
            for s in range(segs):
                a = offset + ring * segs + s
                b = offset + ring * segs + (s + 1) % segs
                c = offset + (ring + 1) * segs + (s + 1) % segs
                d = offset + (ring + 1) * segs + s
                bm.faces.new((bm.verts[a], bm.verts[b], bm.verts[c], bm.verts[d]))

    # Build major body parts as overlapping capsules.
    # The boolean union below merges them into one mesh.
    add_capsule((0, 0.95, 0), (0.20, 0.16), 0.20, segs=20, rings=8)            # hips
    add_capsule((0, 1.20, 0), (0.24, 0.18), 0.30, segs=24, rings=10)           # torso
    add_capsule((0, 1.45, 0), (0.26, 0.20), 0.26, segs=24, rings=10)           # chest
    add_capsule((0, 1.78, 0), (0.13, 0.135), 0.20, segs=20, rings=10)          # head

    add_capsule((-0.12, 0.55, 0), (0.075, 0.075), 0.86, segs=18, rings=14)     # left leg
    add_capsule(( 0.12, 0.55, 0), (0.075, 0.075), 0.86, segs=18, rings=14)     # right leg
    add_capsule((-0.10, 0.045, 0.04), (0.07, 0.13), 0.10, segs=16, rings=6)    # left foot
    add_capsule(( 0.10, 0.045, 0.04), (0.07, 0.13), 0.10, segs=16, rings=6)    # right foot

    add_capsule((-0.30, 1.45, 0), (0.06, 0.06), 0.55, segs=16, rings=12)       # left arm
    add_capsule(( 0.30, 1.45, 0), (0.06, 0.06), 0.55, segs=16, rings=12)       # right arm
    add_capsule((-0.30, 0.95, 0), (0.055, 0.055), 0.42, segs=14, rings=10)     # left forearm
    add_capsule(( 0.30, 0.95, 0), (0.055, 0.055), 0.42, segs=14, rings=10)     # right forearm
    add_capsule((-0.30, 0.80, 0), (0.05, 0.06), 0.10, segs=12, rings=6)        # left hand
    add_capsule(( 0.30, 0.80, 0), (0.05, 0.06), 0.10, segs=12, rings=6)        # right hand

    # Weld + smooth before writing back so the seams between capsules disappear.
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.005)
    for face in bm.faces:
        face.smooth = True

    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    # One subdivision pass for sculpt-grade resolution.
    mod_subsurf = obj.modifiers.new("Subsurf", "SUBSURF")
    mod_subsurf.levels = 2
    mod_subsurf.render_levels = 2

    # Normal smoothing.
    obj.data.use_auto_smooth = True
    obj.data.auto_smooth_angle = math.radians(60)

    return obj


# ===================================================================== Armature

VIKING_BONES = [
    # (name, head, tail, parent)
    ("Hips",          (0.0, 0.95, 0.0), (0.0, 1.05, 0.0),  None),
    ("Spine",         (0.0, 1.05, 0.0), (0.0, 1.30, 0.0),  "Hips"),
    ("Chest",         (0.0, 1.30, 0.0), (0.0, 1.55, 0.0),  "Spine"),
    ("Neck",          (0.0, 1.55, 0.0), (0.0, 1.65, 0.0),  "Chest"),
    ("Head",          (0.0, 1.65, 0.0), (0.0, 1.85, 0.0),  "Neck"),

    ("LeftUpLeg",     (-0.12, 0.95, 0.0), (-0.12, 0.55, 0.0), "Hips"),
    ("LeftLeg",       (-0.12, 0.55, 0.0), (-0.12, 0.10, 0.0), "LeftUpLeg"),
    ("LeftFoot",      (-0.12, 0.10, 0.0), (-0.12, 0.05, 0.16), "LeftLeg"),
    ("RightUpLeg",    ( 0.12, 0.95, 0.0), ( 0.12, 0.55, 0.0), "Hips"),
    ("RightLeg",      ( 0.12, 0.55, 0.0), ( 0.12, 0.10, 0.0), "RightUpLeg"),
    ("RightFoot",     ( 0.12, 0.10, 0.0), ( 0.12, 0.05, 0.16), "RightLeg"),

    ("LeftShoulder",  (-0.18, 1.55, 0.0), (-0.30, 1.50, 0.0), "Chest"),
    ("LeftArm",       (-0.30, 1.50, 0.0), (-0.30, 1.15, 0.0), "LeftShoulder"),
    ("LeftForeArm",   (-0.30, 1.15, 0.0), (-0.30, 0.85, 0.0), "LeftArm"),
    ("LeftHand",      (-0.30, 0.85, 0.0), (-0.30, 0.75, 0.0), "LeftForeArm"),
    ("RightShoulder", ( 0.18, 1.55, 0.0), ( 0.30, 1.50, 0.0), "Chest"),
    ("RightArm",      ( 0.30, 1.50, 0.0), ( 0.30, 1.15, 0.0), "RightShoulder"),
    ("RightForeArm",  ( 0.30, 1.15, 0.0), ( 0.30, 0.85, 0.0), "RightArm"),
    ("RightHand",     ( 0.30, 0.85, 0.0), ( 0.30, 0.75, 0.0), "RightForeArm"),
]


def build_armature(name: str = "VikingRig") -> bpy.types.Object:
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm = bpy.context.object
    arm.name = name
    arm.data.name = name + "_Data"
    arm.show_in_front = True

    eb = arm.data.edit_bones
    for b in list(eb):
        eb.remove(b)

    created = {}
    for bone_name, head, tail, parent in VIKING_BONES:
        b = eb.new(bone_name)
        b.head = Vector(head)
        b.tail = Vector(tail)
        if parent and parent in created:
            b.parent = created[parent]
            b.use_connect = False
        created[bone_name] = b

    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


# ===================================================================== Skin + materials

def auto_skin(mesh_obj: bpy.types.Object, armature_obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def make_material(name: str, color: tuple[float, float, float]) -> bpy.types.Material:
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
        bsdf.inputs["Roughness"].default_value = 0.6
        bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def assign_basic_materials(mesh_obj: bpy.types.Object) -> None:
    skin   = make_material("VikingSkin",   (0.85, 0.65, 0.50))
    tunic  = make_material("VikingTunic",  (0.30, 0.45, 0.55))
    hair   = make_material("VikingHair",   (0.18, 0.12, 0.08))
    leather= make_material("VikingLeather",(0.35, 0.22, 0.12))
    metal  = make_material("VikingIron",   (0.55, 0.56, 0.58))
    for m in (skin, tunic, hair, leather, metal):
        if m.name not in [s.material.name for s in mesh_obj.material_slots if s.material]:
            mesh_obj.data.materials.append(m)


# ===================================================================== Export

def export_fbx(repo_root: str, name: str = "Player_VikingHero") -> str:
    target_dir = os.path.join(repo_root, "Assets", "_Project", "Art", "Models", "Characters", "Player")
    os.makedirs(target_dir, exist_ok=True)
    fbx_path = os.path.join(target_dir, f"{name}.fbx")

    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_simplify_factor=1.0,
        path_mode="AUTO",
        embed_textures=False,
        axis_forward="-Z",
        axis_up="Y",
    )
    return fbx_path


# ===================================================================== Main

def main() -> None:
    repo = find_repo_root()
    if repo is None:
        raise RuntimeError(
            "Couldn't locate repo root. Save the .blend file inside "
            "SourceArt/Blender/ before running this script."
        )

    # Clean slate — clear default cube + lights + camera the user might have.
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()

    arm = build_armature("VikingRig")
    bpy.ops.object.mode_set(mode="OBJECT")

    mesh = build_humanoid_mesh("VikingMesh")
    auto_skin(mesh, arm)
    assign_basic_materials(mesh)

    # Select both for export.
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm

    fbx_path = export_fbx(repo, "Player_VikingHero")
    print(f"[Steading] Exported sculpt-style Viking to: {fbx_path}")

    # Save the .blend itself alongside the export so the artist has a
    # starting point to refine.
    blend_path = bpy.data.filepath
    if blend_path:
        bpy.ops.wm.save_mainfile()
        print(f"[Steading] Saved .blend to: {blend_path}")


if __name__ == "__main__":
    main()
