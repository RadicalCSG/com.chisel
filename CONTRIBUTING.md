# Contributing to Chisel

Thanks for wanting to help! There are many ways to do so:

* **Report problems** you run into, with a scene or steps that show them: [open an issue](https://github.com/RadicalCSG/com.chisel/issues/new/choose).
* **Suggest features**, or add your thoughts to the [planned features](https://github.com/orgs/RadicalCSG/projects/1).
* **Improve the documentation** on [the wiki](https://github.com/RadicalCSG/com.chisel/wiki).
* **Fix bugs or build features**, see below.
* **Answer questions** in the [Discord server](https://discord.gg/zttNkPQ).

## Setting up

Chisel is a Unity package. To work on it, put it in a Unity project as an embedded package:

1. Create a project with **Unity 6.6 or newer** and the **Universal Render Pipeline (URP)**.
2. Clone the repository into the project's `Packages` folder:

   ```
   cd <your project>/Packages
   git clone https://github.com/<you>/com.chisel.git
   ```

   (Fork the repository first, and clone your fork.)
3. Add its dependency, [Custom Asset Metadata](https://github.com/RadicalCSG/CustomAssetMetadata), to the project's
   `Packages/manifest.json`:

   ```
   "com.custom_asset_metadata": "https://github.com/RadicalCSG/CustomAssetMetadata.git",
   ```
4. Open the project in Unity. The first time takes a while, because Burst compiles a lot of code.

Users install Chisel by its own git URL. The bootstrap (`Bootstrap`), which references nothing else, adds Custom Asset
Metadata when a project doesn't have it, and Chisel's other assemblies only compile once it's there: they have the
`CHISEL_ASSET_METADATA` define constraint, set by a version define on the package. An assembly that references Custom
Asset Metadata, or one of Chisel's assemblies that do, needs that constraint and version define too.

The older installer (`Installer~`) is a package of its own, kept so its URL keeps working. To work on it, add it to a
project with a `file:` path to that folder; it does nothing there, so its tests can run.

## Where things are

* `Core`: the CSG itself, the generators' shapes and everything that turns brushes into meshes. It doesn't use
  GameObjects.
* `Components`: the MonoBehaviours you put in a scene (models, generators, composites, decals) and their managers.
* `Editor`: the inspectors, scene view tools, overlays and windows.

Generators only describe their shapes; the CSG does everything else. If a generator's output looks wrong, the fix
almost always belongs in the CSG, not in a special case in the generator.

## Pull requests

* Make a branch for each change, and keep a pull request to one thing: a fix, or a feature.
* Write code that reads like the code around it: its naming, its style, and few, short comments.
* Say in the pull request what it changes and why, and how you tested it: which scene, which steps, in which Unity
  version.
* For a bug fix, link the issue it fixes.
* Make sure the package compiles without warnings, in the editor and in a player build.
* New features should work for any level someone builds by hand, not only for one kind of content, and be reachable
  from the editor's UI.

Pull requests are squash-merged into a single commit, which keeps you as its author.

## Tests

The tests are in `Core/Tests`, `Components/Tests` and `Editor/Tests`. With Chisel embedded in your project's
`Packages` folder, run them from **Window > General > Test Runner** (EditMode), in a scene without a Chisel model.
Please run them before opening a pull request, and add tests for what you fix or build.

## License

By contributing, you agree that your contributions are licensed under the [MIT license](LICENSE), like the rest of
Chisel.
