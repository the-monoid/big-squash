from __future__ import annotations

import math
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Tuple


ROOT = Path(__file__).resolve().parents[2]
UNITY_MODELS = ROOT / "Assets" / "_Project" / "Art" / "Models"
SOURCE_EXPORTS = ROOT / "SourceArt" / "Blender" / "exports"


Vec3 = Tuple[float, float, float]


@dataclass
class MeshObject:
    name: str
    material: str
    vertices: List[Vec3] = field(default_factory=list)
    faces: List[Tuple[int, ...]] = field(default_factory=list)


class ObjScene:
    def __init__(self, name: str):
        self.name = name
        self.objects: List[MeshObject] = []
        self.materials: Dict[str, Tuple[float, float, float, float]] = {}

    def material(self, name: str, color: Tuple[float, float, float], roughness: float = 0.65) -> str:
        self.materials[name] = (color[0], color[1], color[2], roughness)
        return name

    def add(self, mesh: MeshObject) -> None:
        self.objects.append(mesh)

    def write(self, path: Path) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        mtl_path = path.with_suffix(".mtl")
        with mtl_path.open("w", encoding="utf-8") as mtl:
            mtl.write("# Generated Steading prototype materials\n")
            for name, (r, g, b, roughness) in self.materials.items():
                mtl.write(f"newmtl {name}\n")
                mtl.write(f"Kd {r:.4f} {g:.4f} {b:.4f}\n")
                mtl.write(f"Ka {r * 0.28:.4f} {g * 0.28:.4f} {b * 0.28:.4f}\n")
                mtl.write("Ks 0.1200 0.1200 0.1200\n")
                mtl.write(f"Ns {max(4.0, (1.0 - roughness) * 120.0):.2f}\n")
                mtl.write("d 1.0\nillum 2\n\n")

        with path.open("w", encoding="utf-8") as obj:
            obj.write(f"# Generated Steading model: {self.name}\n")
            obj.write(f"mtllib {mtl_path.name}\n")
            vertex_offset = 1
            for mesh in self.objects:
                obj.write(f"\no {mesh.name}\n")
                obj.write(f"usemtl {mesh.material}\n")
                for v in mesh.vertices:
                    obj.write(f"v {v[0]:.5f} {v[1]:.5f} {v[2]:.5f}\n")
                for face in mesh.faces:
                    indices = " ".join(str(vertex_offset + i) for i in face)
                    obj.write(f"f {indices}\n")
                vertex_offset += len(mesh.vertices)


def transform(v: Vec3, position: Vec3, rotation_y: float = 0.0, scale: Vec3 = (1, 1, 1)) -> Vec3:
    x, y, z = v[0] * scale[0], v[1] * scale[1], v[2] * scale[2]
    c, s = math.cos(rotation_y), math.sin(rotation_y)
    return (x * c - z * s + position[0], y + position[1], x * s + z * c + position[2])


def box(name: str, mat: str, center: Vec3, size: Vec3, rot_y: float = 0.0) -> MeshObject:
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    local = [
        (-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
        (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz),
    ]
    verts = [transform(v, center, rot_y) for v in local]
    faces = [
        (0, 1, 2, 3), (5, 4, 7, 6), (4, 0, 3, 7),
        (1, 5, 6, 2), (3, 2, 6, 7), (4, 5, 1, 0),
    ]
    return MeshObject(name, mat, verts, faces)


def cylinder(name: str, mat: str, center: Vec3, radius: float, height: float, segments: int = 24, scale_x: float = 1.0, scale_z: float = 1.0, rot_y: float = 0.0) -> MeshObject:
    verts: List[Vec3] = []
    for y in (-height / 2, height / 2):
        for i in range(segments):
            a = i / segments * math.tau
            verts.append(transform((math.cos(a) * radius * scale_x, y, math.sin(a) * radius * scale_z), center, rot_y))
    verts.append(transform((0, -height / 2, 0), center, rot_y))
    verts.append(transform((0, height / 2, 0), center, rot_y))
    bottom_center = segments * 2
    top_center = bottom_center + 1
    faces: List[Tuple[int, ...]] = []
    for i in range(segments):
        ni = (i + 1) % segments
        faces.append((i, ni, segments + ni, segments + i))
        faces.append((bottom_center, i, ni))
        faces.append((top_center, segments + ni, segments + i))
    return MeshObject(name, mat, verts, faces)


def cone(name: str, mat: str, center: Vec3, radius: float, height: float, segments: int = 18, scale_x: float = 1.0, scale_z: float = 1.0, rot_y: float = 0.0) -> MeshObject:
    verts: List[Vec3] = []
    for i in range(segments):
        a = i / segments * math.tau
        verts.append(transform((math.cos(a) * radius * scale_x, -height / 2, math.sin(a) * radius * scale_z), center, rot_y))
    verts.append(transform((0, height / 2, 0), center, rot_y))
    verts.append(transform((0, -height / 2, 0), center, rot_y))
    tip = segments
    base = segments + 1
    faces: List[Tuple[int, ...]] = []
    for i in range(segments):
        ni = (i + 1) % segments
        faces.append((i, ni, tip))
        faces.append((base, ni, i))
    return MeshObject(name, mat, verts, faces)


def ellipsoid(name: str, mat: str, center: Vec3, radii: Vec3, lat: int = 14, lon: int = 28, rot_y: float = 0.0) -> MeshObject:
    verts: List[Vec3] = []
    for iy in range(lat + 1):
        phi = iy / lat * math.pi
        for ix in range(lon):
            theta = ix / lon * math.tau
            local = (
                math.cos(theta) * math.sin(phi) * radii[0],
                math.cos(phi) * radii[1],
                math.sin(theta) * math.sin(phi) * radii[2],
            )
            verts.append(transform(local, center, rot_y))
    faces: List[Tuple[int, ...]] = []
    for iy in range(lat):
        row = iy * lon
        next_row = (iy + 1) * lon
        for ix in range(lon):
            ni = (ix + 1) % lon
            faces.append((row + ix, row + ni, next_row + ni, next_row + ix))
    return MeshObject(name, mat, verts, faces)


def blade(name: str, mat: str, center: Vec3, length: float, width: float, depth: float, rot_y: float = 0.0) -> MeshObject:
    local = [
        (-width, 0, 0), (0, 0, depth), (width, 0, 0), (0, 0, -depth),
        (-width * 0.45, length * 0.84, 0), (0, length * 0.84, depth * 0.65),
        (width * 0.45, length * 0.84, 0), (0, length * 0.84, -depth * 0.65),
        (0, length, 0),
    ]
    verts = [transform(v, center, rot_y) for v in local]
    faces = [
        (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7),
        (4, 5, 8), (5, 6, 8), (6, 7, 8), (7, 4, 8), (0, 3, 2, 1),
    ]
    return MeshObject(name, mat, verts, faces)


def add_viking(scene: ObjScene) -> None:
    skin = scene.material("M_Skin_Nordic", (0.70, 0.50, 0.36), 0.52)
    hair = scene.material("M_Hair_Brown", (0.18, 0.10, 0.045), 0.68)
    cloth = scene.material("M_Tunic_Green", (0.10, 0.28, 0.22), 0.72)
    cloak = scene.material("M_Cloak_Blue", (0.10, 0.16, 0.28), 0.78)
    leather = scene.material("M_Leather", (0.30, 0.16, 0.07), 0.60)
    metal = scene.material("M_Iron", (0.50, 0.52, 0.50), 0.34)
    fur = scene.material("M_Fur", (0.44, 0.40, 0.34), 0.86)

    scene.add(ellipsoid("Pelvis", cloth, (0, 0.93, 0), (0.30, 0.16, 0.22)))
    scene.add(ellipsoid("Torso", cloth, (0, 1.31, 0), (0.38, 0.42, 0.25)))
    scene.add(ellipsoid("Chest_Armor", metal, (0, 1.50, -0.015), (0.40, 0.26, 0.26)))
    scene.add(ellipsoid("Fur_Collar", fur, (0, 1.71, 0), (0.48, 0.10, 0.30)))
    scene.add(box("Cloak_Back", cloak, (0, 1.24, -0.31), (0.78, 0.95, 0.055)))
    scene.add(ellipsoid("Head", skin, (0, 1.95, 0.035), (0.18, 0.25, 0.17)))
    scene.add(ellipsoid("Beard", hair, (0, 1.78, 0.14), (0.15, 0.18, 0.07)))
    scene.add(ellipsoid("Helmet", metal, (0, 2.10, 0.0), (0.24, 0.12, 0.22)))
    scene.add(box("Nose_Guard", metal, (0, 2.00, 0.19), (0.035, 0.26, 0.025)))
    scene.add(box("Belt", leather, (0, 1.10, 0.015), (0.68, 0.08, 0.08)))
    scene.add(cylinder("Helmet_Brow_Band", metal, (0, 2.035, 0.0), 0.235, 0.028, 32, scale_x=1.0, scale_z=0.86))

    for row in range(4):
        count = 7 - row
        y = 1.57 - row * 0.062
        width = 0.46 - row * 0.045
        for i in range(count):
            x = -width * 0.5 + width * (i / max(1, count - 1))
            scene.add(ellipsoid(f"Mail_Scale_{row}_{i}", metal, (x, y, 0.255), (0.026, 0.036, 0.009), 6, 12))

    for i in range(13):
        x = -0.39 + i * (0.78 / 12)
        scene.add(ellipsoid(f"Fur_Tuft_{i}", fur, (x, 1.70 - abs(x) * 0.08, 0.02 + abs(x) * 0.08), (0.050 + abs(x) * 0.035, 0.040, 0.095), 6, 12))

    for i in range(9):
        x = -0.13 + i * (0.26 / 8)
        scene.add(cylinder(f"Beard_Strand_{i}", hair, (x, 1.70, 0.155), 0.014 + abs(x) * 0.030, 0.26 + abs(x) * 0.30, 10))

    for side, sx in (("Left", -1), ("Right", 1)):
        scene.add(cylinder(f"{side}_UpperArm", cloth, (sx * 0.43, 1.42, 0), 0.075, 0.43, rot_y=0.0))
        scene.objects[-1].vertices = [transform((v[0] - sx * 0.43, v[1] - 1.42, v[2]), (sx * 0.50, 1.27, 0), 0, (0.72, 1.0, 0.72)) for v in scene.objects[-1].vertices]
        scene.add(cylinder(f"{side}_Forearm", skin, (sx * 0.58, 1.05, 0.02), 0.060, 0.38, scale_x=0.9))
        scene.add(ellipsoid(f"{side}_Hand", skin, (sx * 0.58, 0.82, 0.05), (0.06, 0.07, 0.045)))
        scene.add(cylinder(f"{side}_Thigh", cloth, (sx * 0.15, 0.62, 0), 0.085, 0.48))
        scene.add(cylinder(f"{side}_Shin", leather, (sx * 0.15, 0.20, 0), 0.070, 0.42))
        scene.add(ellipsoid(f"{side}_Boot", leather, (sx * 0.15, 0.00, 0.12), (0.09, 0.05, 0.18)))

    scene.add(cylinder("Shield_Board", scene.material("M_Shield_Wood", (0.48, 0.29, 0.14), 0.65), (-0.70, 1.05, 0.22), 0.33, 0.045, 32, scale_x=1.0, scale_z=1.0, rot_y=math.pi / 2))
    scene.add(cylinder("Shield_Painted_Face", cloak, (-0.70, 1.05, 0.255), 0.265, 0.020, 32, rot_y=math.pi / 2))
    scene.add(ellipsoid("Shield_Boss", metal, (-0.70, 1.05, 0.25), (0.11, 0.11, 0.045)))
    scene.add(blade("Held_Sword_Blade", metal, (0.62, 0.83, 0.05), 0.85, 0.055, 0.025, rot_y=0.05))
    scene.add(cylinder("Held_Sword_Grip", leather, (0.62, 0.72, 0.05), 0.025, 0.26))


def add_draugr(scene: ObjScene) -> None:
    skin = scene.material("M_Draugr_Skin", (0.20, 0.31, 0.28), 0.72)
    rag = scene.material("M_Draugr_Rags", (0.15, 0.13, 0.11), 0.86)
    bone = scene.material("M_Bone", (0.62, 0.58, 0.48), 0.75)
    glow = scene.material("M_Eye_Glow", (0.35, 0.95, 0.70), 0.25)
    rust = scene.material("M_Rust_Iron", (0.33, 0.25, 0.17), 0.62)

    scene.add(ellipsoid("Gaunt_Pelvis", rag, (0, 0.84, 0), (0.27, 0.13, 0.20)))
    scene.add(ellipsoid("Ribcage", skin, (0, 1.26, 0), (0.34, 0.38, 0.22)))
    scene.add(box("Ragged_Tunic", rag, (0, 1.16, 0.02), (0.70, 0.62, 0.12)))
    scene.add(cylinder("Spine_Bone", bone, (0, 1.35, -0.24), 0.025, 0.56))
    scene.add(ellipsoid("Skull_Head", skin, (0, 1.82, 0.02), (0.18, 0.24, 0.16)))
    scene.add(ellipsoid("Jaw", bone, (0, 1.66, 0.07), (0.13, 0.08, 0.10)))
    scene.add(ellipsoid("Left_Eye", glow, (-0.055, 1.85, 0.16), (0.020, 0.012, 0.008)))
    scene.add(ellipsoid("Right_Eye", glow, (0.055, 1.85, 0.16), (0.020, 0.012, 0.008)))
    scene.add(box("Rusty_Chest_Plate", rust, (0, 1.39, 0.22), (0.36, 0.28, 0.035)))
    for i in range(5):
        y = 1.42 - i * 0.060
        scene.add(cylinder(f"Left_Rib_{i}", bone, (-0.16, y, 0.235), 0.012, 0.33 - i * 0.018, 10, scale_x=1.0, scale_z=0.65, rot_y=math.pi / 2))
        scene.add(cylinder(f"Right_Rib_{i}", bone, (0.16, y, 0.235), 0.012, 0.33 - i * 0.018, 10, scale_x=1.0, scale_z=0.65, rot_y=math.pi / 2))
    scene.add(box("Rotten_Sash", rag, (-0.02, 1.31, 0.255), (0.64, 0.055, 0.035), rot_y=0.45))

    for side, sx in (("Left", -1), ("Right", 1)):
        scene.add(cylinder(f"{side}_UpperArm", skin, (sx * 0.42, 1.30, 0), 0.065, 0.42))
        scene.add(cylinder(f"{side}_Forearm", skin, (sx * 0.58, 0.95, 0.02), 0.052, 0.36))
        scene.add(cone(f"{side}_Claw", bone, (sx * 0.58, 0.72, 0.08), 0.055, 0.18, 10))
        if side == "Right":
            scene.add(cylinder("Rusty_Knife_Grip", rag, (sx * 0.58, 0.64, 0.07), 0.020, 0.18, 12))
            scene.add(blade("Rusty_Knife_Blade", rust, (sx * 0.58, 0.49, 0.08), 0.34, 0.035, 0.018))
        scene.add(cylinder(f"{side}_Thigh", rag, (sx * 0.14, 0.55, 0), 0.075, 0.44))
        scene.add(cylinder(f"{side}_Shin", skin, (sx * 0.14, 0.16, 0), 0.060, 0.36))
        scene.add(ellipsoid(f"{side}_Foot", skin, (sx * 0.14, 0.00, 0.10), (0.075, 0.045, 0.15)))


def add_weapon_pack(scene: ObjScene) -> None:
    iron = scene.material("M_Polished_Iron", (0.68, 0.70, 0.68), 0.32)
    dark = scene.material("M_Dark_Iron", (0.17, 0.18, 0.18), 0.48)
    gold = scene.material("M_Brass", (0.68, 0.50, 0.24), 0.38)
    leather = scene.material("M_Grip_Leather", (0.16, 0.08, 0.035), 0.65)
    wood = scene.material("M_Ash_Wood", (0.43, 0.26, 0.12), 0.72)
    paint = scene.material("M_Shield_Paint", (0.38, 0.08, 0.06), 0.74)

    scene.add(blade("Sword_Blade", iron, (-1.2, 0.05, 0), 1.15, 0.07, 0.025))
    scene.add(cylinder("Sword_Grip", leather, (-1.2, -0.18, 0), 0.030, 0.32))
    scene.add(box("Sword_Guard", gold, (-1.2, 0.02, 0), (0.45, 0.045, 0.065)))
    scene.add(ellipsoid("Sword_Pommel", gold, (-1.2, -0.37, 0), (0.075, 0.055, 0.075)))

    scene.add(cylinder("Axe_Handle", wood, (0.15, 0.02, 0), 0.035, 1.10))
    scene.add(box("Axe_Eye", dark, (0.15, 0.48, 0), (0.16, 0.18, 0.08)))
    scene.add(box("Axe_Blade", iron, (0.36, 0.50, 0), (0.36, 0.30, 0.055)))
    scene.add(box("Axe_Beard", iron, (0.32, 0.34, 0), (0.24, 0.18, 0.055)))

    scene.add(cylinder("RoundShield_Wood", wood, (1.35, 0.30, 0), 0.36, 0.055, 36, rot_y=math.pi / 2))
    scene.add(cylinder("RoundShield_Paint", paint, (1.35, 0.30, 0.035), 0.28, 0.018, 36, rot_y=math.pi / 2))
    scene.add(ellipsoid("RoundShield_Boss", dark, (1.35, 0.30, 0.08), (0.11, 0.11, 0.055)))


def add_buildables(scene: ObjScene) -> None:
    wood_a = scene.material("M_Weathered_Wood_A", (0.42, 0.25, 0.12), 0.78)
    wood_b = scene.material("M_Weathered_Wood_B", (0.30, 0.17, 0.08), 0.82)
    iron = scene.material("M_Blackened_Iron", (0.12, 0.13, 0.12), 0.52)
    stone = scene.material("M_Rough_Stone", (0.42, 0.42, 0.38), 0.84)
    banner = scene.material("M_Red_Banner", (0.36, 0.05, 0.04), 0.86)

    for i in range(7):
        scene.add(box(f"Wall_Plank_{i}", wood_a if i % 2 == 0 else wood_b, (-3.0 + i * 0.16, 1.2, 0), (0.13, 2.25, 0.16)))
    scene.add(box("Wall_Top_Beam", wood_b, (-2.52, 2.32, -0.02), (1.32, 0.16, 0.20)))
    scene.add(box("Wall_Cross_Brace_A", wood_b, (-2.65, 1.20, -0.13), (0.13, 2.35, 0.12), rot_y=0.0))
    scene.add(box("Wall_Cross_Brace_B", wood_b, (-2.38, 1.20, -0.15), (0.13, 2.35, 0.12), rot_y=0.0))

    for i in range(8):
        scene.add(box(f"Floor_Board_{i}", wood_a if i % 2 == 0 else wood_b, (-0.72 + i * 0.20, 0.05, -1.25), (0.18, 0.10, 1.80)))
    scene.add(box("Floor_Edge_A", wood_b, (0.0, 0.12, -2.18), (1.82, 0.18, 0.12)))
    scene.add(box("Floor_Edge_B", wood_b, (0.0, 0.12, -0.32), (1.82, 0.18, 0.12)))

    for i in range(5):
        scene.add(box(f"Palisade_Log_{i}", wood_b, (1.85 + i * 0.22, 1.35, 0), (0.16, 2.70, 0.18)))
        scene.add(cone(f"Palisade_Point_{i}", wood_b, (1.85 + i * 0.22, 2.82, 0), 0.095, 0.32, 8))
    scene.add(box("Palisade_Iron_Band", iron, (2.29, 1.25, -0.11), (1.18, 0.11, 0.08)))

    scene.add(box("Tower_Stone_Base", stone, (3.75, 0.25, -1.35), (1.45, 0.50, 1.45)))
    for x in (-0.58, 0.58):
        for z in (-0.58, 0.58):
            scene.add(cylinder("Tower_Post", wood_b, (3.75 + x, 1.35, -1.35 + z), 0.07, 2.20))
    scene.add(box("Tower_Platform", wood_a, (3.75, 2.35, -1.35), (1.85, 0.16, 1.85)))
    scene.add(box("Tower_Roof", wood_b, (3.75, 3.05, -1.35), (1.55, 0.22, 1.55)))
    scene.add(box("Tower_Banner", banner, (3.75, 2.60, -2.30), (0.48, 0.62, 0.04)))


def add_world_props(scene: ObjScene) -> None:
    bark = scene.material("M_Pine_Bark", (0.24, 0.15, 0.08), 0.86)
    pine = scene.material("M_Pine_Needles", (0.12, 0.27, 0.12), 0.82)
    stone = scene.material("M_Field_Stone", (0.42, 0.42, 0.38), 0.88)
    moss = scene.material("M_Moss", (0.18, 0.30, 0.13), 0.90)

    scene.add(cylinder("Pine_Trunk", bark, (-1.0, 1.4, 0), 0.16, 2.8, 14))
    scene.add(cone("Pine_Lower_Canopy", pine, (-1.0, 1.65, 0), 0.95, 1.25, 18))
    scene.add(cone("Pine_Mid_Canopy", pine, (-1.0, 2.25, 0), 0.70, 1.05, 18))
    scene.add(cone("Pine_Top_Canopy", pine, (-1.0, 2.78, 0), 0.42, 0.78, 18))

    scene.add(ellipsoid("Rock_Body", stone, (1.0, 0.22, 0), (0.72, 0.34, 0.55), 7, 14))
    scene.add(ellipsoid("Rock_Moss", moss, (0.86, 0.42, 0.08), (0.28, 0.06, 0.22), 5, 10))

    scene.add(cylinder("Fallen_Log", bark, (0.0, 0.24, -1.35), 0.20, 1.90, 14, rot_y=math.pi / 2))


def write_scene(name: str, relative: str, builder) -> None:
    scene = ObjScene(name)
    builder(scene)
    unity_path = UNITY_MODELS / relative
    export_path = SOURCE_EXPORTS / relative
    scene.write(export_path)
    print(f"Wrote {export_path.relative_to(ROOT)}")
    try:
        scene.write(unity_path)
        print(f"Wrote {unity_path.relative_to(ROOT)}")
    except PermissionError:
        print(f"Skipped Unity asset write while editor has Assets locked: {unity_path.relative_to(ROOT)}")


def main() -> None:
    write_scene("SM_PlayerViking_Prototype", "Characters/Player/SM_PlayerViking_Prototype.obj", add_viking)
    write_scene("SM_Draugr_Prototype", "Characters/Enemies/SM_Draugr_Prototype.obj", add_draugr)
    write_scene("SM_VikingWeapons_Prototype", "Weapons/SM_VikingWeapons_Prototype.obj", add_weapon_pack)
    write_scene("SM_BuildingKit_Prototype", "Buildables/SM_BuildingKit_Prototype.obj", add_buildables)
    write_scene("SM_WorldProps_Prototype", "World/SM_WorldProps_Prototype.obj", add_world_props)


if __name__ == "__main__":
    main()
