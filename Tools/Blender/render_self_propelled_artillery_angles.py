from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "Assets" / "Models" / "SelfPropelledArtillery" / "Renders"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def ensure_camera():
    camera = bpy.context.scene.camera
    if camera is None:
        bpy.ops.object.camera_add()
        camera = bpy.context.object
        bpy.context.scene.camera = camera

    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 8.0
    return camera


def setup_render():
    scene = bpy.context.scene
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 900
    scene.render.film_transparent = False

    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = 32

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.18, 0.18, 0.18)

    for obj in bpy.context.scene.objects:
        if obj.type == "LIGHT":
            obj.data.energy = max(obj.data.energy, 500)

    if not any(obj.type == "LIGHT" for obj in bpy.context.scene.objects):
        bpy.ops.object.light_add(type="AREA", location=(0.0, -4.5, 6.0))
        light = bpy.context.object
        light.name = "Render_Key_Light"
        light.data.energy = 700
        light.data.size = 6


def render_angle(name, location, target=(0.0, 1.55, 1.15), scale=8.0):
    camera = ensure_camera()
    camera.location = location
    camera.data.ortho_scale = scale
    look_at(camera, target)

    bpy.context.scene.render.filepath = str(OUTPUT_DIR / f"{name}.png")
    bpy.ops.render.render(write_still=True)


def main():
    setup_render()
    render_angle("artillery_front_left", (5.6, -8.0, 3.55), scale=9.0)
    render_angle("artillery_side", (8.8, 1.3, 2.75), scale=8.8)
    render_angle("artillery_side_opposite", (-8.8, 1.3, 2.75), scale=8.8)
    render_angle("artillery_rear_right", (-5.8, 7.8, 3.35), scale=9.0)
    render_angle("artillery_top", (0.05, 1.3, 9.8), scale=9.4)
    render_angle("artillery_front_low", (0.0, -8.7, 2.1), scale=8.6)
    render_angle("artillery_muzzle_angle", (2.8, 8.9, 2.2), target=(0.0, 3.0, 1.75), scale=5.4)
    render_angle("artillery_turret_close", (4.2, -4.2, 2.7), target=(0.0, 0.9, 1.9), scale=4.9)
    render_angle("artillery_rear_top", (-3.9, 6.2, 5.2), scale=7.2)
    render_angle("artillery_front_right_top", (-5.5, -7.7, 4.2), scale=9.0)


if __name__ == "__main__":
    main()
