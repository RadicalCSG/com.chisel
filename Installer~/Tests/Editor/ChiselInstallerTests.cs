using NUnit.Framework;

namespace Chisel.Installer.Tests
{
    public class ChiselInstallerTests
    {
        [Test]
        public void AnEmptyProject_GetsTheMetadataPackage_ThenChisel()
        {
            Assert.That(ChiselInstaller.ToAdd(hasMetadata: false, hasChisel: false, chiselFromGit: false),
                        Is.EqualTo(new[] { ChiselInstaller.kMetadataUrl, ChiselInstaller.kChiselUrl }));
        }

        [Test]
        public void AProjectWithEverything_GetsNothing()
        {
            Assert.That(ChiselInstaller.ToAdd(hasMetadata: true, hasChisel: true, chiselFromGit: true), Is.Empty);
        }

        [Test]
        public void AProjectWithTheMetadataPackage_GetsChisel()
        {
            Assert.That(ChiselInstaller.ToAdd(hasMetadata: true, hasChisel: false, chiselFromGit: false),
                        Is.EqualTo(new[] { ChiselInstaller.kChiselUrl }));
        }

        // a Chisel from git that still had the metadata package inside it
        [Test]
        public void AnOlderChiselFromGit_IsUpdatedAlongWithTheMetadataPackage()
        {
            Assert.That(ChiselInstaller.ToAdd(hasMetadata: false, hasChisel: true, chiselFromGit: true),
                        Is.EqualTo(new[] { ChiselInstaller.kMetadataUrl, ChiselInstaller.kChiselUrl }));
        }

        // a local or embedded Chisel is someone's own copy
        [Test]
        public void ALocalChisel_IsLeftAlone()
        {
            Assert.That(ChiselInstaller.ToAdd(hasMetadata: false, hasChisel: true, chiselFromGit: false),
                        Is.EqualTo(new[] { ChiselInstaller.kMetadataUrl }));
        }
    }
}
