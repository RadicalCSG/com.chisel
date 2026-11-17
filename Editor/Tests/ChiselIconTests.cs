using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Every icon Chisel shows is there, in both skins, and is loaded by its file's exact name: Windows and macOS find a
    // file whatever the case of its name, but Linux only by its exact name, so an icon asked for by a name in another
    // case is missing there alone. These tests see it on any of them, as the names are compared exactly.
    [TestFixture]
    public class ChiselIconTests
    {
        const string kPackageFolder = "Packages/com.chisel/";

        [TearDown]
        public void TearDown()
        {
            ChiselEditorResources.ClearCache();
        }

        // The package's folder on disk
        static string PackageFolderOnDisk()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ChiselEditorResources).Assembly);
            Assert.That(package, Is.Not.Null, "Chisel is not a package here");
            return package.resolvedPath.Replace('\\', '/') + "/";
        }

        // The files in a folder of the package, by their names exactly as they are on disk
        static HashSet<string> FileNamesOnDisk(string folderInPackage)
        {
            var folder = PackageFolderOnDisk() + folderInPackage;
            return new HashSet<string>(Directory.GetFiles(folder).Select(Path.GetFileName), StringComparer.Ordinal);
        }

        static bool IsChisels(Type type)
        {
            var assembly = type.Assembly.GetName().Name;
            return assembly.StartsWith("com.chisel", StringComparison.Ordinal) && !assembly.Contains("tests");
        }

        // Every icon Chisel asks for, and what asks for it: the fixed ones, every generator's placement tool's, and the
        // ones the toggle flags of every enum that is drawn as toggles name
        static List<(string what, ChiselIcon icon)> IconsChiselUses()
        {
            var icons = new List<(string, ChiselIcon)>();
            foreach (var field in typeof(ChiselIcon).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(ChiselIcon))
                    icons.Add(($"ChiselIcon.{field.Name}", (ChiselIcon)field.GetValue(null)));
            }

            Assert.That(ChiselGeneratorManager.s_GeneratorModes, Is.Not.Null.And.Not.Empty, "no generators found");
            foreach (var generator in ChiselGeneratorManager.s_GeneratorModes)
                icons.Add(($"generator {generator.ToolName}", ChiselIcon.OfGenerator(generator.ToolName)));

            var toggled = new HashSet<Type>();
            foreach (var field in TypeCache.GetFieldsWithAttribute<ToggleFlagsAttribute>())
            {
                if (IsChisels(field.DeclaringType) && field.FieldType.IsEnum)
                    toggled.Add(field.FieldType);
            }
            Assert.That(toggled, Is.Not.Empty, "no toggle flags found");
            foreach (var type in toggled)
            {
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!ToggleFlagsPropertyDrawer.ToggleIconsOf(member, out _, out var inactive, out var active))
                        continue;
                    icons.Add(($"toggle flag {type.Name}.{member.Name}", inactive));
                    icons.Add(($"toggle flag {type.Name}.{member.Name}, active", active));
                }
            }
            return icons;
        }

        // Each icon Chisel asks for is there in both skins, inactive and active (or its plain look in their place), by
        // the exact name of its file
        [Test]
        public void EveryIconChiselUses_IsThere_InBothSkins_ByItsFilesExactName()
        {
            var files = FileNamesOnDisk("Editor Resources/Icons");
            var missing = new List<string>();
            foreach (var (what, icon) in IconsChiselUses())
            {
                foreach (var dark in new[] { false, true })
                {
                    foreach (var active in new[] { false, true })
                    {
                        var look = $"{(dark ? "dark" : "light")} skin, {(active ? "active" : "inactive")}";
                        var path = ChiselEditorResources.FindIconPath(icon, dark, active);
                        if (path == null)
                            missing.Add($"{what} ({icon.name}): {look}: no image");
                        else if (!path.StartsWith(kPackageFolder + "Editor Resources/Icons/", StringComparison.Ordinal) || !files.Contains(Path.GetFileName(path)))
                            missing.Add($"{what} ({icon.name}): {look}: {path} is not a file by that exact name");
                    }
                }
            }
            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        }

        // And it loads, in the editor's own skin
        [Test]
        public void EveryIconChiselUses_Loads()
        {
            var missing = new List<string>();
            foreach (var (what, icon) in IconsChiselUses())
            {
                var images = ChiselEditorResources.LoadIconImages(icon);
                if (images == null || images[0] == null || images[1] == null)
                    missing.Add($"{what} ({icon.name})");
            }
            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        }

        // The guidelines (CONTRIBUTING.md, Icons): an icon's name is lowercase words joined by underscores
        static readonly System.Text.RegularExpressions.Regex kIconName = new(@"^[a-z0-9]+(_[a-z0-9]+)*$");

        // and its files are "<name>.png", with "d_" in front for the dark skin, " on" behind for the active look and
        // "@2x" last for double resolution
        static readonly System.Text.RegularExpressions.Regex kIconFileName = new(@"^(d_)?[a-z0-9]+(_[a-z0-9]+)*( on)?(@2x)?\.png$");

        // Chisel asks for each of its icons by a name that follows the guidelines
        [Test]
        public void EveryIconChiselUses_IsNamedByTheGuidelines()
        {
            var wrong = IconsChiselUses().Where(icon => icon.icon.name == null || !kIconName.IsMatch(icon.icon.name))
                                         .Select(icon => $"{icon.what}: \"{icon.icon.name}\"")
                                         .ToList();
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        // Every file in the icons folder is an icon named by the guidelines, with its .meta
        [Test]
        public void EveryIconFile_IsNamedByTheGuidelines()
        {
            var files = FileNamesOnDisk("Editor Resources/Icons");
            var wrong = new List<string>();
            foreach (var file in files)
            {
                if (file.EndsWith(".meta", StringComparison.Ordinal))
                {
                    if (!files.Contains(file.Substring(0, file.Length - ".meta".Length)))
                        wrong.Add($"{file}: a .meta without its file");
                    continue;
                }
                if (!kIconFileName.IsMatch(file))
                    wrong.Add($"{file}: not named [d_]<name>[ on][@2x].png, all lowercase");
                if (!files.Contains(file + ".meta"))
                    wrong.Add($"{file}: no .meta");
            }
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        // A dark, active or double resolution file has the file without that next to it, which the editor falls back to
        [Test]
        public void EveryIconFilesVariant_HasTheFileWithoutIt()
        {
            var files = FileNamesOnDisk("Editor Resources/Icons");
            var missing = new List<string>();
            foreach (var file in files.Where(file => kIconFileName.IsMatch(file)))
            {
                var name = file.Substring(0, file.Length - ".png".Length);
                if (name.EndsWith("@2x", StringComparison.Ordinal))
                    Expect(file, name.Substring(0, name.Length - "@2x".Length) + ".png");
                else if (name.EndsWith(" on", StringComparison.Ordinal))
                    Expect(file, name.Substring(0, name.Length - " on".Length) + ".png");
                else if (name.StartsWith("d_", StringComparison.Ordinal))
                    Expect(file, name.Substring("d_".Length) + ".png");
            }
            Assert.That(missing, Is.Empty, string.Join("\n", missing));

            void Expect(string file, string without)
            {
                if (!files.Contains(without))
                    missing.Add($"{file} without {without}");
            }
        }

        // Unity loads a component's [Icon] by the path given, which on Linux must name the file exactly
        [Test]
        public void EveryIconAttribute_NamesItsFileExactly()
        {
            var checkedAny = false;
            var wrong = new List<string>();
            var pathMember = typeof(IconAttribute).GetProperty("path", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(pathMember, Is.Not.Null, "IconAttribute has no path");
            foreach (var type in TypeCache.GetTypesWithAttribute<IconAttribute>())
            {
                if (!IsChisels(type))
                    continue;
                var attribute = type.GetCustomAttribute<IconAttribute>(inherit: false);
                if (attribute == null)
                    continue;
                checkedAny = true;
                var path = (string)pathMember.GetValue(attribute);
                if (!path.StartsWith(kPackageFolder, StringComparison.Ordinal))
                {
                    wrong.Add($"{type.Name}: {path} is not in the package");
                    continue;
                }
                var inPackage = path.Substring(kPackageFolder.Length);
                var folder = Path.GetDirectoryName(inPackage).Replace('\\', '/');
                if (!Directory.Exists(PackageFolderOnDisk() + folder) || !FileNamesOnDisk(folder).Contains(Path.GetFileName(inPackage)))
                    wrong.Add($"{type.Name}: no file named exactly {path}");
            }
            Assert.That(checkedAny, Is.True, "no [Icon] found");
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        // The edit tools' icons are set on their scripts, as references to the images (by guid, whatever their names)
        [Test]
        public void EveryEditTool_HasItsIcons_InBothSkins()
        {
            var tools = TypeCache.GetTypesDerivedFrom<ChiselEditToolBase>().Where(type => !type.IsAbstract).ToList();
            Assert.That(tools, Is.Not.Empty, "no edit tools found");
            var missing = new List<string>();
            foreach (var tool in tools)
            {
                MonoImporter importer = null;
                foreach (var guid in AssetDatabase.FindAssets($"t:MonoScript {tool.Name}"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass() == tool)
                        importer = AssetImporter.GetAtPath(path) as MonoImporter;
                }
                if (importer == null)
                {
                    missing.Add($"{tool.Name}: its script is not found");
                    continue;
                }
                foreach (var name in new[] { nameof(ChiselEditToolBase.m_ToolIcon), nameof(ChiselEditToolBase.m_ToolIconDark) })
                {
                    if (importer.GetDefaultReference(name) == null)
                        missing.Add($"{tool.Name}: {name}");
                }
            }
            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        }

        // Docked or collapsed, the Chisel Snap Settings overlay shows its own icon, not the editor's default
        [Test]
        public void TheSnapSettingsOverlay_HasItsOwnIcon()
        {
            var overlay = (ChiselSnappingOptionsToolbar)Activator.CreateInstance(typeof(ChiselSnappingOptionsToolbar), nonPublic: true);
            // the icon can be set but not read back: it is kept in a field of the editor's own
            var field = typeof(UnityEditor.Overlays.Overlay).GetField("m_CollapsedIcon", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "the editor no longer keeps an overlay's icon in m_CollapsedIcon");
            var icon = field.GetValue(overlay) as Texture2D;
            Assert.That(icon, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(icon), Does.EndWith("snapping.png"));
        }

        // The Surface Browser's tab shows its icon with its name
        [Test]
        public void TheSurfaceBrowsersTab_HasItsIcon()
        {
            var window = ScriptableObject.CreateInstance<ChiselSurfaceBrowserWindow>();
            try
            {
                Assert.That(window.titleContent.text, Is.EqualTo("Surface Browser"));
                Assert.That(window.titleContent.image, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(window.titleContent.image), Does.EndWith("box_surfaces.png"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // The editor's own icons Chisel shows
        [TestCase("MoveTool")]
        [TestCase("MoveTool On")]
        [TestCase("eyeDropper.Large")]
        [TestCase("d_eyeDropper.Large")]
        public void TheEditorsOwnIconsChiselUses_AreThere(string name)
        {
            Assert.That(EditorGUIUtility.FindTexture(name), Is.Not.Null);
        }

        // The images are found by their names whatever the case of either, and loaded by the path they are at
        [Test]
        public void AnImage_IsFoundWhateverTheCaseOfItsName_AtItsOwnPath()
        {
            var folders = new[] { "Packages/p/Editor Resources", "Assets/Editor Resources" };
            var index = ChiselEditorResources.IndexImages(folders, new[]
            {
                "Packages/p/Editor Resources/Icons/BoundsSnap ON.png",
                "Packages/p/Editor Resources/Icons/d_convertToBrush.png",
                "Packages/p/Editor Resources/Icons/csg_additive On@2x.png",
                "Packages/p/Editor Resources/Icons/readme.txt",
                "Assets/Other/BoundsSnap.png",
            });
            Assert.That(index["icons/boundssnap on"], Is.EqualTo("Packages/p/Editor Resources/Icons/BoundsSnap ON.png"));
            Assert.That(index["icons/d_converttobrush"], Is.EqualTo("Packages/p/Editor Resources/Icons/d_convertToBrush.png"));
            Assert.That(index["icons/csg_additive on@2x"], Is.EqualTo("Packages/p/Editor Resources/Icons/csg_additive On@2x.png"));
            Assert.That(index.Count, Is.EqualTo(3), "only the images in the folders");
        }

        // The package's own image is found before one by the same name in the project
        [Test]
        public void ThePackagesImage_IsFoundBeforeTheProjects()
        {
            var folders = new[] { "Packages/p/Editor Resources", "Assets/Editor Resources" };
            var index = ChiselEditorResources.IndexImages(folders, new[]
            {
                "Assets/Editor Resources/Icons/rebuild.png",
                "Packages/p/Editor Resources/Icons/Rebuild.png",
            });
            Assert.That(index["icons/rebuild"], Is.EqualTo("Packages/p/Editor Resources/Icons/Rebuild.png"));
        }

        // The icons Linux didn't find, asked for by a name in another case than their file's: found by the file's name
        [TestCase("Bounds_Snap")]
        [TestCase("UV_VERTEX_SNAP")]
        [TestCase("Convert_To_Brush")]
        [TestCase("center_pivot_on_selection_Center")]
        [TestCase("CENTER_PIVOT_ON_EACH")]
        public void AnIconAskedForInAnotherCase_IsFoundByItsFilesExactName(string name)
        {
            var files = FileNamesOnDisk("Editor Resources/Icons");
            foreach (var dark in new[] { false, true })
            {
                var path = ChiselEditorResources.FindIconPath(new ChiselIcon(name), dark, active: false);
                Assert.That(path, Is.Not.Null, $"{name}, {(dark ? "dark" : "light")} skin");
                Assert.That(files, Does.Contain(Path.GetFileName(path)), $"{name}, {(dark ? "dark" : "light")} skin");
            }
        }
    }
}
