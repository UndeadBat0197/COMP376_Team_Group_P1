# Polished menu + character select: setup

1. Copy the `Group_Prototype_1` folder from this zip over the one in the repo
   (it only adds files, nothing existing is replaced).
2. Open the project in Unity and wait for it to compile and import.
3. Run **Tools > Foxy Adventure > Set Up Everything**.
4. Look at the `Main_Menu` scene, press Play to try it, then save (Ctrl+S).

The second character is the cat from Kenney's "Cube Pets" pack (CC0, free
for any use, credit optional). It is already in `Assets/Characters/Cat/`
with its texture and licence file.

## What the tool creates
- `Main_Menu` scene: a `Polished_Menu` object (canvas, panels, buttons,
  3D preview stages, MenuController). The old `Main_Menu_Canvas` is switched
  off, not deleted.
- `Third Person Player` prefab: `GFX > Cat > Rig > Fit > Model`, plus a
  `PlayerCharacterSwitcher` component listing Fox and Cat.
- `Assets/Characters/Cat/Cat_Locomotion.controller`: idle / walk / run blend
  tree driven by the same `speed` parameter the fox uses.
- `Assets/Characters/Cat/Cat_Toon.mat`: the cat's cartoon material, using the
  project's `Toon/Lit Outline` shader (two-tone shading, rim light, outline).
- `Assets/UI/PolishedMenu/`: sprites, render textures and a material.

## Adjusting
- Cat faces the wrong way / wrong size: open the player prefab and rotate or
  scale `GFX > Cat > Rig > Fit`. Same for the menu preview under
  `Polished_Menu > Character_Previews > Stage_Cat`.
- Toon look (outline width, shade colour, rim): edit `Cat_Toon.mat`. It is
  kept when you run the tool again; delete it to get the defaults back.
- Jump stretch / landing squash: `ProceduralCharacterAnimator` on `GFX > Cat`.
- Test a level as the cat without the menu: set `Force Character Id` to
  `Cat` on `PlayerCharacterSwitcher`.
- Menu text, colours, button order: edit the objects in the Hierarchy.
  Re-running "Build Polished Main Menu" rebuilds `Polished_Menu` from
  scratch, so do that before hand edits, not after.
- Another character later: put its model in `Assets/Characters/<Name>/`, add
  one entry to the `Extras` list at the top of
  `Assets/Script/Editor/FoxyMenuBuilder.cs`, and run the tool again.
