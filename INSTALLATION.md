### Installation

Chisel requires **Unity 6.6 or newer**, and its materials are made for the **Universal Render Pipeline (URP)**, which
is installed along with it.

Go to **Window > Package Manager**.

In the package manager press the **+** and select **Install package from git URL**

![Dropdown](Documentation~/Images/package_manager_dropdown.png)
---
In the textbox type in

https://github.com/RadicalCSG/com.chisel.git

And press enter. Chisel then installs the package it depends on by itself; until that's there, only that small part
of Chisel is compiled, and the rest follows once it is.

![URL Textbox](Documentation~/Images/package_manager_url.png)
---

Chisel depends on [Custom Asset Metadata](https://github.com/RadicalCSG/CustomAssetMetadata), which is installed
from git too: the Package Manager doesn't install a package's dependencies from git by itself, which is why Chisel
does. So git needs to be installed and on your path.

If Chisel can't install it, install it yourself the same way, with
https://github.com/RadicalCSG/CustomAssetMetadata.git

### Updating from a version with Custom Asset Metadata inside it

Older versions of Chisel had Custom Asset Metadata inside them, as a git submodule. Install Chisel's URL again as
above: it updates Chisel, which then installs Custom Asset Metadata.

The first time you start up it might take some time, because Unity Burst compiles a lot of code.
This should only happen once.

### If the installation fails

* **"Filename too long"** (Windows): git can't write the dependencies inside a deep project folder. Allow long
  paths in git once, with `git config --global core.longpaths true`, or move the project to a shorter path, then
  install again.
* **"Cannot checkout repository"**: make sure git is installed and that `git` works in a command prompt; Unity uses
  it to download Chisel and its dependencies.

Then see [Getting Started](GETTING_STARTED.md).
