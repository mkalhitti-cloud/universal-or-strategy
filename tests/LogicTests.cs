using NUnit.Framework;
using NinjaTrader.NinjaScript.Strategies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace UniversalOrStrategy.Tests
{
    [TestFixture]
    public class LogicTests
    {
        private sealed class StickyStateSection
        {
            public StickyStateSection(string name)
            {
                Name = name;
                Entries = new List<string>();
            }

            public string Name { get; }

            public List<string> Entries { get; }
        }

        [Test]
        [TestCase(10, 1, new[] { 10, 0, 0, 0, 0 })]
        [TestCase(10, 2, new[] { 5, 5, 0, 0, 0 })]
        [TestCase(10, 3, new[] { 4, 3, 3, 0, 0 })] // Remainder 10%3=1 goes to T1
        [TestCase(10, 4, new[] { 3, 3, 2, 2, 0 })] // Remainder 10%4=2 goes to T1, T2
        [TestCase(7, 5, new[] { 2, 2, 1, 1, 1 })]  // Remainder 7%5=2 goes to T1, T2
        public void GetTargetDistribution_ValidInputs_ReturnsExpectedBuckets(int contracts, int count, int[] expected)
        {
            var result = V12_PureLogic.GetTargetDistribution(contracts, count);
            Assert.AreEqual(expected, result, $"Failed for {contracts} contracts with {count} targets");
        }

        [Test]
        public void CalculatePositionSize_BasicRisk_ReturnsCorrectQty()
        {
            // Stop = 10 points, PV = $50, Risk = $1000, No cushion
            // Contracts = 1000 / (10 * 50) = 2
            int qty = V12_PureLogic.CalculatePositionSize(10, 1000, 0, 50, 1, 100);
            Assert.AreEqual(2, qty);
        }

        [Test]
        public void CalculatePositionSize_WithCushion_ReturnsCorrectQty()
        {
            // Stop = 10 points, PV = $50, Risk = $1100, Cushion = 2 points ($100)
            // Effective Risk = 1100 - 100 = 1000
            // Contracts = 1000 / (10 * 50) = 2
            int qty = V12_PureLogic.CalculatePositionSize(10, 1100, 2, 50, 1, 100);
            Assert.AreEqual(2, qty);
        }

        [Test]
        public void CalculatePositionSize_MinMaxClamp_ClampsCorrectly()
        {
            // Math results in 10, but max is 5
            int qty = V12_PureLogic.CalculatePositionSize(2, 1000, 0, 50, 1, 5);
            Assert.AreEqual(5, qty);

            // Math results in 0, but min is 1
            qty = V12_PureLogic.CalculatePositionSize(100, 10, 0, 50, 1, 100);
            Assert.AreEqual(1, qty);
        }

        [Test]
        public void CalculateATRStopDistance_ValidATR_ReturnsCeilingStop()
        {
            // 2.3 ATR * 2.0 Mult = 4.6 -> Ceiling = 5.0
            double stop = V12_PureLogic.CalculateATRStopDistance(2.3, 2.0, 1.0, 100.0);
            Assert.AreEqual(5.0, stop);
        }

        [Test]
        public void StickyState_RoundTrip_PreservesState()
        {
            string fixture = string.Join(
                Environment.NewLine,
                "# V12 StickyState v1",
                "# Symbol: MES 06-26",
                "[CONFIG]",
                "MODE=RMA",
                "COUNT=3",
                "T1=10.5",
                "T1TYPE=Points",
                "T2=12",
                "T2TYPE=ATR",
                "T3=18.25",
                "T3TYPE=Runner",
                "STR=2.5",
                "MAX=750",
                "CIT=4",
                "TRMA=1",
                "RRMA=0",
                "",
                "[FLEET]",
                "LEADER=Apex_Main",
                "Apex_F01=1",
                "Apex_F02=0",
                "",
                "[ANCHOR]",
                "TYPE=EMA65",
                "MNL_PRICE=5312.25",
                "",
                "[CONFIG_OR]",
                "COUNT=2",
                "T1=8",
                "T1TYPE=Ticks",
                "STR=1.5",
                "MAX=500",
                "",
                "[CONFIG_RMA]",
                "COUNT=3",
                "T1=10.5",
                "T1TYPE=Points",
                "T2=12",
                "T2TYPE=ATR",
                "STR=2.5",
                "MAX=750",
                "",
                "[POSITIONS]",
                "# key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount",
                "ENTRY_1|5315.75|2|1|0|3") + Environment.NewLine;
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".v12state");

            try
            {
                File.WriteAllText(tempPath, fixture, new UTF8Encoding(false));

                List<StickyStateSection> original = ParseStickyStateSections(fixture);
                List<StickyStateSection> loaded = LoadStickyStateFixture(tempPath);
                string roundTrip = SerializeStickyStateSections(loaded);
                List<StickyStateSection> reparsed = ParseStickyStateSections(roundTrip);

                Assert.That(original.Count, Is.GreaterThan(0));
                Assert.That(StickyStateSectionsEqual(loaded, original), Is.True);
                Assert.That(StickyStateSectionsEqual(reparsed, original), Is.True);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private static List<StickyStateSection> LoadStickyStateFixture(string path)
        {
            string content = File.ReadAllText(path, Encoding.UTF8);
            return ParseStickyStateSections(content);
        }

        private static List<StickyStateSection> ParseStickyStateSections(string content)
        {
            var sections = new List<StickyStateSection>();
            StickyStateSection currentSection = null;

            using (var reader = new StringReader(content ?? string.Empty))
            {
                string rawLine;
                while ((rawLine = reader.ReadLine()) != null)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal) &&
                        line.Length > 2)
                    {
                        currentSection = new StickyStateSection(line.Substring(1, line.Length - 2).ToUpperInvariant());
                        sections.Add(currentSection);
                        continue;
                    }

                    if (currentSection == null)
                        continue;

                    currentSection.Entries.Add(NormalizeStickyStateEntry(currentSection.Name, line));
                }
            }

            return sections;
        }

        private static string SerializeStickyStateSections(IEnumerable<StickyStateSection> sections)
        {
            var builder = new StringBuilder();
            bool isFirstSection = true;

            foreach (StickyStateSection section in sections)
            {
                if (!isFirstSection)
                    builder.AppendLine();

                builder.Append('[').Append(section.Name).AppendLine("]");
                foreach (string entry in section.Entries)
                    builder.AppendLine(entry);

                isFirstSection = false;
            }

            return builder.ToString();
        }

        private static bool StickyStateSectionsEqual(
            IReadOnlyList<StickyStateSection> left,
            IReadOnlyList<StickyStateSection> right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                StickyStateSection leftSection = left[i];
                StickyStateSection rightSection = right[i];
                if (!string.Equals(leftSection.Name, rightSection.Name, StringComparison.Ordinal))
                    return false;
                if (!leftSection.Entries.SequenceEqual(rightSection.Entries))
                    return false;
            }

            return true;
        }

        private static string NormalizeStickyStateEntry(string sectionName, string line)
        {
            if (string.Equals(sectionName, "POSITIONS", StringComparison.Ordinal))
                return line;

            int eq = line.IndexOf('=');
            if (eq < 1)
                return line;

            string key = line.Substring(0, eq).Trim().ToUpperInvariant();
            string value = line.Substring(eq + 1).Trim();
            return key + "=" + value;
        }
    }
}
