import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = ROOT / "Assets" / "Models" / "SelfPropelledArtillery"
BLEND_PATH = ASSET_DIR / "SelfPropelledArtillery.blend"
FBX_PATH = ASSET_DIR / "SelfPropelledArtillery.fbx"


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_mat(name, color, roughness=0.55, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
    else:
        material.diffuse_color = color

    return material


def parent_keep_world(child, parent):
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


def cube(name, loc, scale, material, parent=None, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material is not None:
        obj.data.materials.append(material)
        obj.color = material.diffuse_color
    if parent is not None:
        parent_keep_world(obj, parent)
    return obj


def cylinder(name, loc, radius, depth, material, parent=None, vertices=8, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        end_fill_type="NGON",
        location=loc,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    if material is not None:
        obj.data.materials.append(material)
        obj.color = material.diffuse_color
    if parent is not None:
        parent_keep_world(obj, parent)
    return obj


def empty(name, loc, parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.35
    obj.location = loc
    bpy.context.collection.objects.link(obj)
    if parent is not None:
        parent_keep_world(obj, parent)
    return obj


def make_wedge(name, loc, width, length, height, material, parent=None, nose_forward=True):
    half_w = width * 0.5
    half_l = length * 0.5
    low_front = -height * 0.45 if nose_forward else height * 0.45
    high_back = height * 0.45 if nose_forward else -height * 0.45

    verts = [
        (-half_w, -half_l, -height * 0.5),
        (half_w, -half_l, -height * 0.5),
        (half_w, half_l, -height * 0.5),
        (-half_w, half_l, -height * 0.5),
        (-half_w, -half_l, high_back),
        (half_w, -half_l, high_back),
        (half_w, half_l, low_front),
        (-half_w, half_l, low_front),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    ]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = loc
    if material is not None:
        mesh.materials.append(material)
        obj.color = material.diffuse_color
    bpy.context.collection.objects.link(obj)
    if parent is not None:
        parent_keep_world(obj, parent)
    return obj


def set_flat_low_poly():
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue

        obj.data.polygons.foreach_set("use_smooth", [False] * len(obj.data.polygons))
        obj.data.update()


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_track_link(name, side, x, y, z, pitch, material, root, outer_offset=0.0, scale=(0.2, 0.18, 0.08)):
    return cube(
        name,
        (x + side * outer_offset, y, z),
        scale,
        material,
        root,
        rotation=(pitch, 0.0, 0.0),
    )


def build_track_loop(side, outside_x, root, mat_track_pad, mat_dark, mat_dark_metal):
    center_z = 0.48
    radius = 0.38
    top_z = center_z + radius
    bottom_z = center_z - radius
    rear_y = -2.55
    front_y = 2.55
    link_width = 0.22
    link_length = 0.17
    link_thickness = 0.08
    outer_offset = 0.16

    cube(f"SPG_Track_Inner_Shadow_{side}", (outside_x + side * 0.04, 0.0, center_z), (0.08, 5.4, 0.72), mat_dark, root)
    cube(f"SPG_Track_Top_Rubber_Base_{side}", (outside_x + side * 0.08, 0.0, top_z - 0.03), (0.1, 5.08, 0.09), mat_dark, root)
    cube(f"SPG_Track_Bottom_Rubber_Base_{side}", (outside_x + side * 0.08, 0.0, bottom_z + 0.03), (0.1, 5.08, 0.09), mat_dark, root)

    index = 0
    straight_count = 32
    for i in range(straight_count):
        t = i / (straight_count - 1)
        y = rear_y + (front_y - rear_y) * t
        create_track_link(
            f"SPG_TrackLink_Top_{side}_{i}",
            side,
            outside_x,
            y,
            top_z,
            0.0,
            mat_track_pad,
            root,
            outer_offset,
            (link_width, link_length, link_thickness),
        )
        create_track_link(
            f"SPG_TrackLink_Bottom_{side}_{i}",
            side,
            outside_x,
            y,
            bottom_z,
            math.pi,
            mat_track_pad,
            root,
            outer_offset,
            (link_width, link_length, link_thickness),
        )
        if i % 2 == 0:
            create_track_link(
                f"SPG_TrackGrouser_Top_{side}_{i}",
                side,
                outside_x,
                y,
                top_z + 0.06,
                0.0,
                mat_dark_metal,
                root,
                outer_offset + 0.035,
                (0.08, 0.12, 0.04),
            )
        index += 1

    arc_count = 18
    for i in range(arc_count):
        theta = math.radians(90.0 - i * 180.0 / (arc_count - 1))
        y = front_y + math.cos(theta) * radius
        z = center_z + math.sin(theta) * radius
        pitch = math.atan2(math.cos(theta), -math.sin(theta))
        create_track_link(
            f"SPG_TrackLink_FrontArc_{side}_{i}",
            side,
            outside_x,
            y,
            z,
            pitch,
            mat_track_pad,
            root,
            outer_offset,
            (link_width, link_length, link_thickness),
        )

        theta_rear = math.radians(-90.0 + i * 180.0 / (arc_count - 1))
        rear_y_pos = rear_y - math.cos(theta_rear) * radius
        rear_z_pos = center_z + math.sin(theta_rear) * radius
        rear_pitch = math.atan2(math.cos(theta_rear), math.sin(theta_rear))
        create_track_link(
            f"SPG_TrackLink_RearArc_{side}_{i}",
            side,
            outside_x,
            rear_y_pos,
            rear_z_pos,
            rear_pitch,
            mat_track_pad,
            root,
            outer_offset,
            (link_width, link_length, link_thickness),
        )

    for y, label in ((rear_y, "Rear"), (front_y, "Front")):
        cylinder(
            f"SPG_{label}_Track_Inner_Dark_Ring_{side}",
            (outside_x + side * 0.1, y, center_z),
            radius * 0.92,
            0.08,
            mat_dark,
            root,
            vertices=18,
            rotation=(0.0, math.radians(90.0), 0.0),
        )


def build_artillery():
    clear_scene()
    ASSET_DIR.mkdir(parents=True, exist_ok=True)

    mat_olive = make_mat("SPG_Armor_Olive", (0.26, 0.36, 0.21, 1.0), 0.7)
    mat_light_olive = make_mat("SPG_Armor_LightOlive", (0.43, 0.52, 0.32, 1.0), 0.68)
    mat_sand = make_mat("SPG_Camo_Sand", (0.62, 0.55, 0.37, 1.0), 0.72)
    mat_brown = make_mat("SPG_Camo_Brown", (0.25, 0.19, 0.12, 1.0), 0.78)
    mat_dark_green = make_mat("SPG_Camo_DarkGreen", (0.12, 0.22, 0.12, 1.0), 0.75)
    mat_blue = make_mat("SPG_Player_Blue", (0.04, 0.18, 0.86, 1.0), 0.48)
    mat_dark = make_mat("SPG_Track_Dark", (0.035, 0.034, 0.032, 1.0), 0.76)
    mat_track_pad = make_mat("SPG_Track_Pads", (0.12, 0.115, 0.105, 1.0), 0.84)
    mat_rubber = make_mat("SPG_Rubber", (0.08, 0.075, 0.07, 1.0), 0.8)
    mat_metal = make_mat("SPG_Gun_Metal", (0.46, 0.47, 0.46, 1.0), 0.45, 0.08)
    mat_dark_metal = make_mat("SPG_Dark_Metal", (0.18, 0.18, 0.17, 1.0), 0.55, 0.12)
    mat_glow = make_mat("SPG_Lamp_Amber", (1.0, 0.65, 0.13, 1.0), 0.3)

    root = empty("SelfPropelledArtillery_Model", (0.0, 0.0, 0.0))

    # Lower chassis and tracks.
    cube("SPG_Lower_Hull", (0.0, 0.0, 0.58), (3.18, 5.05, 0.82), mat_olive, root)
    cube("SPG_Belly_Plate", (0.0, -0.05, 0.18), (2.75, 4.72, 0.18), mat_dark_green, root)
    make_wedge("SPG_Front_Slope", (0.0, 2.28, 0.9), 3.0, 1.02, 0.72, mat_light_olive, root, True)
    make_wedge("SPG_Rear_Slope", (0.0, -2.22, 0.9), 2.95, 0.95, 0.64, mat_olive, root, False)
    cube("SPG_Rear_Engine_Block", (0.0, -1.65, 1.12), (2.72, 1.18, 0.86), mat_light_olive, root)
    cube("SPG_Left_Track_Backplate", (-1.66, 0.0, 0.43), (0.18, 5.48, 0.66), mat_dark, root)
    cube("SPG_Right_Track_Backplate", (1.66, 0.0, 0.43), (0.18, 5.48, 0.66), mat_dark, root)
    cube("SPG_Left_Track_Belt_Outer", (-2.03, 0.0, 0.43), (0.18, 5.45, 0.68), mat_dark, root)
    cube("SPG_Right_Track_Belt_Outer", (2.03, 0.0, 0.43), (0.18, 5.45, 0.68), mat_dark, root)
    cube("SPG_Left_Armor_Skirt", (-1.72, 0.12, 0.98), (0.82, 4.15, 0.2), mat_light_olive, root)
    cube("SPG_Right_Armor_Skirt", (1.72, 0.12, 0.98), (0.82, 4.15, 0.2), mat_light_olive, root)
    cube("SPG_Left_Blue_Team_Stripe", (-1.74, 0.25, 1.12), (0.84, 2.35, 0.11), mat_blue, root)
    cube("SPG_Right_Blue_Team_Stripe", (1.74, 0.25, 1.12), (0.84, 2.35, 0.11), mat_blue, root)

    for side in (-1.0, 1.0):
        outside_x = side * 2.13
        for y in (-2.15, -1.45, -0.75, -0.05, 0.65, 1.35, 2.05):
            cylinder(
                f"SPG_Wheel_{side}_{y}",
                (outside_x, y, 0.46),
                0.25,
                0.2,
                mat_rubber,
                root,
                vertices=10,
                rotation=(0.0, math.radians(90.0), 0.0),
            )
            cylinder(
                f"SPG_Wheel_Hub_{side}_{y}",
                (outside_x + side * 0.02, y, 0.46),
                0.115,
                0.22,
                mat_sand,
                root,
                vertices=8,
                rotation=(0.0, math.radians(90.0), 0.0),
            )
        for y, radius in ((-2.55, 0.34), (2.55, 0.34)):
            cylinder(
                f"SPG_Idler_{side}_{y}",
                (outside_x, y, 0.48),
                radius,
                0.22,
                mat_rubber,
                root,
                vertices=12,
                rotation=(0.0, math.radians(90.0), 0.0),
            )
            cylinder(
                f"SPG_Idler_Hub_{side}_{y}",
                (outside_x + side * 0.025, y, 0.48),
                0.16,
                0.24,
                mat_dark_metal,
                root,
                vertices=10,
                rotation=(0.0, math.radians(90.0), 0.0),
            )
        build_track_loop(side, outside_x, root, mat_track_pad, mat_dark, mat_dark_metal)
        cube(f"SPG_Track_Guide_Rail_Upper_{side}", (outside_x, 0.0, 0.73), (0.06, 5.1, 0.06), mat_dark_metal, root)
        cube(f"SPG_Track_Guide_Rail_Lower_{side}", (outside_x, 0.0, 0.22), (0.06, 5.1, 0.06), mat_dark_metal, root)

    # Painted armor, vents, lights, and hatches.
    cube("SPG_Top_Blue_Command_Stripe", (0.0, -0.2, 1.38), (2.28, 2.25, 0.12), mat_blue, root)
    cube("SPG_Top_Camo_Sand_Left", (-0.78, 0.65, 1.46), (0.82, 0.78, 0.06), mat_sand, root)
    cube("SPG_Top_Camo_Brown_Right", (0.72, -0.72, 1.47), (0.7, 0.92, 0.06), mat_brown, root)
    cube("SPG_Top_Camo_Dark_Nose", (0.44, 1.72, 1.21), (0.72, 0.42, 0.06), mat_dark_green, root)
    cube("SPG_Engine_Deck_Frame", (0.0, -1.68, 1.6), (2.25, 0.82, 0.09), mat_dark_green, root)
    for i, x in enumerate((-0.8, -0.48, -0.16, 0.16, 0.48, 0.8)):
        cube(f"SPG_Engine_Deck_Slat_{i}", (x, -1.68, 1.68), (0.16, 0.76, 0.08), mat_dark_metal, root)
    for i, y in enumerate((-2.28, -2.1, -1.92, -1.74)):
        cube(f"SPG_Rear_Radiator_Louver_{i}", (-1.16, y, 1.34), (0.1, 0.08, 0.46), mat_dark_metal, root)
        cube(f"SPG_Rear_Radiator_Louver_Right_{i}", (1.16, y, 1.34), (0.1, 0.08, 0.46), mat_dark_metal, root)
    cylinder("SPG_Exhaust_Left", (-1.0, -2.12, 1.72), 0.08, 0.68, mat_dark_metal, root, vertices=8, rotation=(0.0, math.radians(90.0), 0.0))
    cylinder("SPG_Exhaust_Right", (1.0, -2.12, 1.72), 0.08, 0.68, mat_dark_metal, root, vertices=8, rotation=(0.0, math.radians(90.0), 0.0))
    cube("SPG_Engine_Service_Hatch", (0.0, -1.08, 1.64), (1.1, 0.12, 0.08), mat_sand, root)
    cube("SPG_Left_Side_Camo_Sand", (-1.46, -0.75, 1.12), (0.07, 0.82, 0.42), mat_sand, root)
    cube("SPG_Right_Side_Camo_Brown", (1.46, 0.85, 1.12), (0.07, 0.9, 0.38), mat_brown, root)
    cube("SPG_Left_Side_Camo_Dark", (-1.46, 1.25, 1.1), (0.07, 0.66, 0.34), mat_dark_green, root)
    cube("SPG_Right_Side_Camo_Sand", (1.46, -1.2, 1.1), (0.07, 0.74, 0.36), mat_sand, root)
    for x in (-0.78, -0.26, 0.26, 0.78):
        cube(f"SPG_Rear_Vent_{x}", (x, -2.16, 1.58), (0.36, 0.12, 0.16), mat_dark, root)
    cube("SPG_Engine_Grill_Long", (0.0, -2.24, 1.34), (1.8, 0.08, 0.1), mat_dark_metal, root)
    cube("SPG_Front_Lamp_Left", (-0.72, 2.62, 0.94), (0.32, 0.1, 0.12), mat_glow, root)
    cube("SPG_Front_Lamp_Right", (0.72, 2.62, 0.94), (0.32, 0.1, 0.12), mat_glow, root)
    cube("SPG_Front_Tow_Hook_Left", (-0.48, 2.73, 0.55), (0.2, 0.12, 0.16), mat_dark_metal, root)
    cube("SPG_Front_Tow_Hook_Right", (0.48, 2.73, 0.55), (0.2, 0.12, 0.16), mat_dark_metal, root)
    cylinder("SPG_Antenna_Base", (-1.08, -1.55, 1.78), 0.08, 0.16, mat_dark, root, vertices=8)
    cylinder("SPG_Antenna", (-1.08, -1.55, 2.35), 0.018, 1.05, mat_dark, root, vertices=6)
    cylinder("SPG_Antenna_Right", (1.08, -1.48, 2.2), 0.014, 0.9, mat_dark, root, vertices=6)

    # Full artillery turret: the breech is hidden inside the armored cabin,
    # while the oversized barrel defines the unit silhouette.
    turret = empty("TurretPivot", (0.0, 0.02, 1.55), root)
    cylinder("SPG_Turret_Ring", (0.0, 0.02, 1.38), 1.24, 0.16, mat_dark_metal, turret, vertices=16)
    cube("SPG_Turret_Lower_Race", (0.0, -0.05, 1.52), (2.48, 2.05, 0.26), mat_olive, turret)
    cube("SPG_Artillery_Turret_Cabin", (0.0, -0.22, 1.86), (2.58, 2.2, 0.78), mat_light_olive, turret)
    make_wedge("SPG_Turret_Front_Upper_Slope", (0.0, 0.9, 1.96), 2.38, 0.72, 0.86, mat_olive, turret, True)
    cube("SPG_Turret_Left_Armor_Cheek", (-1.34, 0.02, 1.88), (0.22, 2.14, 0.9), mat_dark_green, turret)
    cube("SPG_Turret_Right_Armor_Cheek", (1.34, 0.02, 1.88), (0.22, 2.14, 0.9), mat_dark_green, turret)
    cube("SPG_Turret_Rear_Bustle", (0.0, -1.28, 1.86), (2.3, 0.58, 0.72), mat_olive, turret)
    cube("SPG_Turret_Roof_Plate", (0.0, -0.22, 2.3), (2.36, 1.86, 0.1), mat_dark_green, turret)
    cube("SPG_Massive_Mantlet_Box", (0.0, 1.26, 1.96), (1.5, 0.54, 0.96), mat_dark_metal, turret)
    cube("SPG_Mantlet_Armor_Left", (-0.86, 1.22, 1.96), (0.24, 0.68, 1.02), mat_dark_green, turret)
    cube("SPG_Mantlet_Armor_Right", (0.86, 1.22, 1.96), (0.24, 0.68, 1.02), mat_dark_green, turret)
    cube("SPG_Mantlet_Upper_Lip", (0.0, 1.52, 2.5), (1.62, 0.18, 0.12), mat_olive, turret)
    cube("SPG_Mantlet_Lower_Lip", (0.0, 1.52, 1.38), (1.52, 0.18, 0.12), mat_olive, turret)
    cylinder("SPG_Gun_Trunnion_Hidden", (0.0, 1.22, 1.96), 0.26, 1.64, mat_metal, turret, vertices=12, rotation=(0.0, math.radians(90.0), 0.0))
    cube("SPG_Turret_Camo_Sand_Left", (-0.68, -0.46, 2.38), (0.74, 0.46, 0.08), mat_sand, turret)
    cube("SPG_Turret_Camo_Brown_Right", (0.62, -0.52, 2.39), (0.62, 0.38, 0.08), mat_brown, turret)
    cube("SPG_Turret_Blue_Command_Band", (0.0, -1.08, 2.0), (2.16, 0.11, 0.15), mat_blue, turret)
    cube("SPG_Commander_Hatch", (-0.7, -0.66, 2.5), (0.52, 0.36, 0.14), mat_dark, turret)
    cube("SPG_Loader_Hatch", (0.68, -0.62, 2.48), (0.46, 0.34, 0.12), mat_dark_metal, turret)
    cylinder("SPG_Roof_Optic", (0.0, 0.28, 2.5), 0.065, 0.42, mat_metal, turret, vertices=8, rotation=(math.radians(90.0), 0.0, 0.0))

    gun = empty("GunBarrel", (0.0, 1.46, 1.96), turret)
    cube("SPG_Moving_Mantlet_Cover", (0.0, 1.48, 1.96), (1.08, 0.24, 0.66), mat_metal, gun)
    cylinder(
        "SPG_Gun_Base_Sleeve",
        (0.0, 1.74, 1.96),
        0.34,
        0.62,
        mat_metal,
        gun,
        vertices=12,
        rotation=(math.radians(90.0), 0.0, 0.0),
    )
    cylinder(
        "SPG_Recoil_Jacket",
        (0.0, 2.2, 1.96),
        0.26,
        0.86,
        mat_dark_metal,
        gun,
        vertices=12,
        rotation=(math.radians(90.0), 0.0, 0.0),
    )
    cylinder(
        "SPG_Heavy_Artillery_Barrel",
        (0.0, 3.72, 1.96),
        0.21,
        3.65,
        mat_metal,
        gun,
        vertices=12,
        rotation=(math.radians(90.0), 0.0, 0.0),
    )
    cylinder("SPG_Barrel_Collar_Back", (0.0, 2.68, 1.96), 0.25, 0.2, mat_dark_metal, gun, vertices=12, rotation=(math.radians(90.0), 0.0, 0.0))
    cylinder("SPG_Barrel_Collar_Front", (0.0, 4.66, 1.96), 0.24, 0.2, mat_dark_metal, gun, vertices=12, rotation=(math.radians(90.0), 0.0, 0.0))
    cylinder("SPG_Hydraulic_Recoil_Left", (-0.34, 2.97, 1.68), 0.055, 2.55, mat_metal, gun, vertices=8, rotation=(math.radians(90.0), 0.0, 0.0))
    cylinder("SPG_Hydraulic_Recoil_Right", (0.34, 2.97, 1.68), 0.055, 2.55, mat_metal, gun, vertices=8, rotation=(math.radians(90.0), 0.0, 0.0))
    cube("SPG_Recoil_Rail_Left", (-0.46, 2.54, 1.8), (0.12, 1.65, 0.12), mat_dark_metal, gun)
    cube("SPG_Recoil_Rail_Right", (0.46, 2.54, 1.8), (0.12, 1.65, 0.12), mat_dark_metal, gun)
    cube("SPG_Massive_Muzzle_Brake", (0.0, 5.57, 1.96), (0.9, 0.52, 0.5), mat_metal, gun)
    cube("SPG_Muzzle_Brake_Top_Port", (0.0, 5.57, 2.24), (0.68, 0.58, 0.1), mat_dark, gun)
    cube("SPG_Muzzle_Brake_Left_Port", (-0.43, 5.57, 1.96), (0.1, 0.64, 0.26), mat_dark, gun)
    cube("SPG_Muzzle_Brake_Right_Port", (0.43, 5.57, 1.96), (0.1, 0.64, 0.26), mat_dark, gun)
    cylinder("SPG_Muzzle_Bore", (0.0, 5.87, 1.96), 0.17, 0.08, mat_dark, gun, vertices=12, rotation=(math.radians(90.0), 0.0, 0.0))
    empty("MuzzlePoint", (0.0, 5.97, 1.96), gun)

    # Small side armor plates add silhouette without making the model noisy.
    for side in (-1.0, 1.0):
        cube(f"SPG_Side_Armor_{side}", (side * 1.3, 0.52, 1.26), (0.16, 2.18, 0.44), mat_light_olive, root)
        cube(f"SPG_Side_Blue_Mark_{side}", (side * 1.39, 1.18, 1.32), (0.08, 0.72, 0.22), mat_blue, root)
        cube(f"SPG_Side_Toolbox_{side}", (side * 1.34, -1.58, 1.3), (0.18, 0.68, 0.32), mat_sand, root)

    set_flat_low_poly()

    bpy.ops.object.light_add(type="AREA", location=(0.0, -4.0, 6.0))
    bpy.context.object.name = "Preview_Key_Light"
    bpy.context.object.data.energy = 500
    bpy.context.object.data.size = 5

    bpy.ops.object.camera_add(location=(5.7, -7.2, 4.1))
    bpy.context.scene.camera = bpy.context.object
    look_at(bpy.context.object, (0.0, 1.45, 1.2))
    bpy.context.object.data.type = "ORTHO"
    bpy.context.object.data.ortho_scale = 9.1

    # Keep transforms readable for Unity and export only the model hierarchy.
    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=False,
        add_leaf_bones=False,
    )


if __name__ == "__main__":
    build_artillery()
