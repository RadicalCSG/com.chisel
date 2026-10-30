# Changelog

## 0.9.0-preview
- What decals and hotspot sections cut into the drawn meshes without changing how they look is taken out again
- Normals are smoothed by smoothing group and by the model's smoothing angle without seams, however a surface is cut by other brushes, decals, paint or hotspot sections
- The sphere, capsule and torus have a smoothing group, like the cylinder
- A sphere's texture runs on from one face to the next
- A surface's paint grid grows with the surface
- Every project gets a brush contents list and paint settings
- Dragging in the hotspot layout window can be undone
- The paint brush settings are kept across domain reloads
- Entering play mode without a domain reload gives the same output
- Fixed player builds and the paint tool's color picker
- Fixed live paint, paint after a rebuild, and decal surfaces
- Fixed memory leaks: brush meshes, brush ids and wireframes are freed when nothing uses them or a scene closes
- The URP materials are on material version 11
- Requires the Unity 6.6 versions of Burst, Entities, Collections and Mathematics
- Custom Asset Metadata is a package of its own instead of a git submodule inside Chisel; the Chisel Installer installs both
- Removed the debug windows and menus that were for working on Chisel itself

## 0.4.0-preview
- Requires Unity 6.6 and the Universal Render Pipeline
