using System;
using System.IO;
using Xunit;

namespace V12_002.HybridArena.PlanContract.Tests
{
    public sealed class HybridArenaPlanContractTests
    {
        private static readonly string RepoRoot = FindRepoRoot();

        private static string ReadRepoFile(string relativePath) =>
            File.ReadAllText(Path.Combine(RepoRoot, relativePath));

        private static string FindRepoRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string srcPath = Path.Combine(dir.FullName, "src");
                string agentsPath = Path.Combine(dir.FullName, "AGENTS.md");
                if (Directory.Exists(srcPath) && File.Exists(agentsPath))
                    return dir.FullName;

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
        }

        [Fact]
        public void FleetDispatchSlot_Matches_V29_Contract()
        {
            string pool = ReadRepoFile(Path.Combine("src", "V12_002.Photon.Pool.cs"));

            Assert.Contains("[FieldOffset(24)] public int    AccountIndex", pool);
            Assert.Contains("[FieldOffset(28)] public int    PoolRecordId", pool);
            Assert.Contains("[FieldOffset(48)] public ulong  SignalHash", pool);
            Assert.DoesNotContain("PoolSlotIndex", pool);
            Assert.DoesNotContain("FleetDispatchSideband", pool);
        }

        [Fact]
        public void MmioDispatchMirror_Uses_V2_CacheLine_Layout()
        {
            string mmio = ReadRepoFile(Path.Combine("src", "V12_002.Photon.MmioMirror.cs"));

            Assert.Contains("private const int HeaderBytes           = 192;", mmio);
            Assert.Contains("private const long ProducerCursorOffset = 0;", mmio);
            Assert.Contains("private const long ConsumerCursorOffset = 64;", mmio);
            Assert.Contains("private const long SlotsBaseOffset      = 192;", mmio);
            Assert.Contains("MmioDispatchMirror v2", mmio);
        }

        [Fact]
        public void ManagementMembrane_Source_And_HotPath_Wiring_Exist()
        {
            string substratePath = Path.Combine(RepoRoot, "src", "V12_002.Photon.Substrate.cs");
            string dispatch = ReadRepoFile(Path.Combine("src", "V12_002.SIMA.Dispatch.cs"));
            string fleet = ReadRepoFile(Path.Combine("src", "V12_002.SIMA.Fleet.cs"));

            Assert.True(File.Exists(substratePath), "Expected src/V12_002.Photon.Substrate.cs to exist.");

            string substrate = File.ReadAllText(substratePath);
            Assert.Contains("private sealed class FlattenedSubstrateState", substrate);
            Assert.Contains("private FlattenedSubstrateState _membrane;", substrate);
            Assert.Contains("Volatile.Read(ref _membrane)", dispatch);
            Assert.Contains("Volatile.Read(ref _membrane)", fleet);
        }

        [Fact]
        public void Dispatch_And_Pump_Use_IndexBased_Slot_Fields()
        {
            string dispatch = ReadRepoFile(Path.Combine("src", "V12_002.SIMA.Dispatch.cs"));
            string fleet = ReadRepoFile(Path.Combine("src", "V12_002.SIMA.Fleet.cs"));

            Assert.Contains("AccountIndex =", dispatch);
            Assert.Contains("PoolRecordId =", dispatch);
            Assert.DoesNotContain("_photonSideband", dispatch);
            Assert.DoesNotContain("PoolSlotIndex", dispatch);

            Assert.Contains("abortSlot.AccountIndex", fleet);
            Assert.Contains("ringSlot.PoolRecordId", fleet);
            Assert.DoesNotContain("_photonSideband", fleet);
            Assert.DoesNotContain("PoolSlotIndex", fleet);
        }
    }
}
