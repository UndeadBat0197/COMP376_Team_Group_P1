FOX SAW PACK - Unity saw obstacles

Contains two prefab generators:
- FixedSaw: rotating blade, stays in place.
- MovingSaw: rotating blade travels along the rail then returns.

INSTALL
1. Copy Assets/FoxSawPack into your Unity project's Assets folder.
2. Wait for the Unity scripts to compile.
3. Use Tools > Fox Saw Pack > Create or Rebuild Saw Prefabs.
4. Drag Assets/FoxSawPack/Prefabs/FixedSaw.prefab or MovingSaw.prefab into the scene.
5. Place and rotate them against a suitable surface (e.g. inside the cylinder).
6. Parent them to the CylinderPivot to rotate with the barrel.

INSPECTOR
SawTrap / Moving: turn moving on or off.
Travel Distance: 6 units by default. Travel Speed: 2.5 units/sec.
Travel Direction: local X of the saw root. Rotate the root to change travel direction.
Spin Degrees Per Second: 420.
Damage: 1 HP via existing PlayerHealth.TakeDamage(float).
Hit Cooldown: 1 second.

NOTES
- The builder is in an Editor folder and creates reusable prefabs, materials and the blade mesh.
- No automatic scene modifications or load-time spawning.
- Box collider on pedestal, spherical trigger on blade; visual blade itself has no solid collider.
- Assign saw root as child of CylinderPivot if the saw should follow cylinder rotation.
- If moving saw is very close to a curved cylinder, its rail follows a straight line; keep it on a platform or short span.
- This pack expects the PlayerHealth script from your existing game. Do not duplicate PlayerHealth.
