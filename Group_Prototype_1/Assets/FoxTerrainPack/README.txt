Fox Terrain Pack for Unity
========================

9 prefabs (generated automatically once after importing, or using Tools menu):
OUTSIDE / NATURAL
  GrassLedge         curved grassy terrace
  MossyCliff         rock/grass ledge with rocks
  RockStep           short stone jumping platform
  WoodBridge         wooden planked bridge with rails
  ForestRamp         dirt + grass incline
INSIDE / SCI-FI
  SciFiCatwalk       curved lit metal walkway
  MetalPlatform      modular metal block platform
  EnergyPlatform     dark platform with glowing side strips
  TechRamp           metallic sloped platform

Install
-------
1. Merge only Assets/FoxTerrainPack into your Unity project Assets folder.
2. Wait for Unity compilation and asset import to complete.
3. Open Assets/FoxTerrainPack/Prefabs/. Nine prefabs should appear.
4. If not, use Tools > Fox Terrain Pack > Generate or Rebuild Terrain Prefabs.
5. Drag a prefab into your scene. For a cylinder, position it roughly near the
   intended outer or inner wall, then choose
   Tools > Fox Terrain Pack > Snap Selected Terrain to Cylinder Wall.
6. It looks for an object named CylinderPivot or CylinderRotator, and puts the
   terrain under that object so Q/E rotation will carry it with the cylinder.

IMPORTANT
---------
- The snap tool uses the pivot origin as the assumed cylinder axis center.
  If your CylinderPivot is offset from the actual mesh center, snap manually.
- Default cylinder radius in inspector is 18. Adjust on each instance before
  snapping if your project's cylinder radius differs.
- For inner-wall SciFi prefabs, insideSurface=true.
- MeshCollider is included in every solid terrain prefab. The collision meshes
  are real, serialized mesh assets, not non-readable imported FBX meshes.
- Generated visuals are low-poly and editable. No gameplay scripts/health
  dependencies; nothing here modifies your bat, potion, fruit or thorn assets.
- This package does not auto-edit your scene or hierarchy on import.
- Prefabs are generated through the Unity Editor and are not included as
  pre-baked YAML in this archive.
