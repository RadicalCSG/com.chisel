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
2. Clone the repository, with its submodules, into the project's `Packages` folder:

   ```
   cd <your project>/Packages
   git clone --recurse-submodules https://github.com/<you>/com.chisel.git
   ```

   (Fork the repository first, and clone your fork.) If you already cloned without `--recurse-submodules`, run
   `git submodule update --init` in it.
3. Open the project in Unity. The first time takes a while, because Burst compiles a lot of code.

The package's dependencies, BurstTriangulator and CustomAssetMetadata, are git submodules in `Dependencies`.

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

The automated test suites aren't part of this repository yet, so please describe how you checked your change.

## License

By contributing, you agree that your contributions are licensed under the [MIT license](LICENSE), like the rest of
Chisel.
