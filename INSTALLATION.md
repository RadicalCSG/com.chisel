### Installation

Chisel requires **Unity 6.6 or newer**, and its materials are made for the **Universal Render Pipeline (URP)**, which
is installed along with it.

Go to **Window > Package Manager**.

In the package manager press the **+** and select **Install package from git URL**

![Dropdown](Documentation~/Images/package_manager_dropdown.png)
---
In the textbox type in

https://github.com/RadicalCSG/com.chisel.git

And press enter. This installs Chisel and the packages it depends on.

![URL Textbox](Documentation~/Images/package_manager_url.png)
---

Chisel's own dependencies (BurstTriangulator and CustomAssetMetadata) are git submodules of its repository, so git
needs to be installed and on your path.

The first time you start up it might take some time, because Unity Burst compiles a lot of code.
This should only happen once.

### If the installation fails

* **"Filename too long"** (Windows): git can't write the dependencies inside a deep project folder. Allow long
  paths in git once, with `git config --global core.longpaths true`, or move the project to a shorter path, then
  install again.
* **"Cannot checkout repository"**: make sure git is installed and that `git` works in a command prompt; Unity uses
  it to download Chisel and its dependencies.

Then see [Getting Started](GETTING_STARTED.md).
