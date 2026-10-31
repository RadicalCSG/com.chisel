using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Installed from its own git URL Chisel comes without Custom Asset Metadata: only the bootstrap compiles until it has
    // added it, so every assembly that needs it, directly or through another of Chisel's, waits for it
    [TestFixture]
    public class ChiselInstallTests
    {
        const string kConstraint = "CHISEL_ASSET_METADATA";

        [System.Serializable]
        class AssemblyDefinition
        {
            public string   name;
            public string[] references         = new string[0];
            public string[] defineConstraints  = new string[0];
        }

        static (string path, AssemblyDefinition definition)[] Definitions()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ChiselEditorResources).Assembly);
            Assert.That(package, Is.Not.Null, "Chisel's package");
            return Directory.GetFiles(package.resolvedPath, "*.asmdef", SearchOption.AllDirectories)
                            .Where(path => !path.Replace(Path.DirectorySeparatorChar, '/').Split('/').Any(part => part.EndsWith("~")))
                            .Select(path => (path, JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path))))
                            .ToArray();
        }

        [Test]
        public void EveryAssemblyThatNeedsCustomAssetMetadata_WaitsForIt()
        {
            var definitions = Definitions();
            var gated = definitions.Where(d => d.definition.defineConstraints.Contains(kConstraint)).Select(d => d.definition.name).ToHashSet();
            foreach (var (path, definition) in definitions)
            {
                var needs = definition.references.Any(r => r.StartsWith("AssetMetadata") || gated.Contains(r));
                if (needs)
                    Assert.That(definition.defineConstraints, Does.Contain(kConstraint), $"{definition.name} ({path})");
            }
        }

        [Test]
        public void TheBootstrap_NeedsNothingItInstalls()
        {
            var bootstrap = Definitions().Single(d => d.definition.name == "com.chisel.bootstrap.editor").definition;
            Assert.That(bootstrap.references, Is.Empty);
            Assert.That(bootstrap.defineConstraints, Is.Empty);
        }
    }
}
