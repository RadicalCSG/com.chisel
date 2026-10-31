# Changelog

## 0.9.2-preview
- With the GPU Resident Drawer on, a brush no longer vanishes when an edit changes how many triangles its material's mesh has

## 0.9.1-preview
- Chisel installs from its own git URL, https://github.com/RadicalCSG/com.chisel.git: it adds Custom Asset Metadata by itself when it's missing, and the rest of Chisel compiles once it's there
- Removed the Chisel Installer and its URL (?path=/Installer~)
- A model saved in a prefab keeps its generated meshes in the prefab: instances draw them without overrides, an instance whose geometry is overridden builds its own, and the prefab only keeps the meshes it uses
- Chisel's generated objects can be made in prefab mode
- The editor's helper meshes for hidden brushes and picking aren't saved with the model anymore
- Corners a hair apart are only snapped together along open outline edges, where a crack shows, and alike in every surface of a brush
- The paint tool and the operation buttons show their icons after a new install

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
- Cracks between surfaces of neighbouring brushes are closed: corners a hair apart become one, and an outline edge is split where another surface's corner lies on it
- A collider is only split where another collider has a corner
- Where two brushes of different contents have a face in the same place, the later brush's face is drawn
- A brush's contents can be changed with its Surfaces foldout open
- Clicking a generated mesh in the scene view picks the brush, not the generated object
- The tools show their icons after a new install
- An extruded shape's outline and path points can be edited again
- Changing a model's settings no longer makes Unity warn about SendMessage
- Removed the debug windows and menus that were for working on Chisel itself

## 0.4.0-preview
- Requires Unity 6.6 and the Universal Render Pipeline
