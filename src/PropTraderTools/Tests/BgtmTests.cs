// xUnit-only (JS testing mandate). No NUnit, no MSTest.
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class BgtmTests : IDisposable
    {
        private readonly string _tempDir;

        public BgtmTests()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                "BgtmTests_" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_tempDir);
            // Redirect LicenseClient cache to temp dir so tests do not touch production paths
            LicenseClient._testCachePath = Path.Combine(_tempDir, "license_cache.json");
        }

        public void Dispose()
        {
            LicenseClient._testCachePath = null;
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_NullKey_ReturnsStarter()
        {
            var f = LicenseClient.Validate(null);
            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_EmptyKey_ReturnsStarter()
        {
            var f = LicenseClient.Validate(string.Empty);
            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_WhitespaceKey_ReturnsStarter()
        {
            var f = LicenseClient.Validate("  ");
            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_OfflineCache_HitReturnsCachedFlags()
        {
            // Write a valid unexpired cache entry for key "TEST-PRO"
            var cacheJson = BuildCacheJson(
                "TEST-PRO",
                new[] { "multi_rule", "trim_flatten", "break_even" },
                DateTime.UtcNow.AddDays(7)
            );
            File.WriteAllText(LicenseClient._testCachePath, cacheJson);

            var f = LicenseClient.Validate("TEST-PRO");

            Assert.True(f.MultiRule);
            Assert.True(f.TrimFlatten);
            Assert.True(f.BreakEven);
            Assert.False(f.AtrSizing);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_OfflineCache_ExpiredReturnsStarter()
        {
            // Write an expired cache entry (ExpiresUtc in the past)
            var cacheJson = BuildCacheJson(
                "TEST-PRO",
                new[] { "multi_rule", "trim_flatten", "break_even" },
                DateTime.UtcNow.AddDays(-1)
            );
            File.WriteAllText(LicenseClient._testCachePath, cacheJson);

            // No network in test; TryRemoteValidate will fail => fallback to Starter
            var f = LicenseClient.Validate("TEST-PRO");

            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_WrongKeyCache_ReturnsStarter()
        {
            // Write a valid unexpired cache entry for key "KEY-A"
            var cacheJson = BuildCacheJson(
                "KEY-A",
                new[] { "multi_rule", "trim_flatten", "break_even" },
                DateTime.UtcNow.AddDays(7)
            );
            File.WriteAllText(LicenseClient._testCachePath, cacheJson);

            // Validate with "KEY-B" -- cache is keyed to "KEY-A", so cache miss.
            // No network in test; TryRemoteValidate will fail => fallback to Starter.
            var f = LicenseClient.Validate("KEY-B");

            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_FeatureFlags_Starter_AllFalse()
        {
            var f = FeatureFlags.Starter();
            Assert.False(f.MultiRule);
            Assert.False(f.TrimFlatten);
            Assert.False(f.BreakEven);
            Assert.False(f.AtrSizing);
            Assert.False(f.ClickTrader);
            Assert.False(f.MirrorMode);
            Assert.False(f.QxGlobalExit);
        }

        [Fact]
        public void T_BGTM1_FeatureFlags_Pro_MultiRuleTrimBreakEvenTrue()
        {
            var f = FeatureFlags.Pro();
            Assert.True(f.MultiRule);
            Assert.True(f.TrimFlatten);
            Assert.True(f.BreakEven);
            Assert.False(f.AtrSizing);
            Assert.False(f.ClickTrader);
            Assert.False(f.MirrorMode);
            Assert.False(f.QxGlobalExit);
        }

        [Fact]
        public void T_BGTM1_FeatureFlags_Elite_AllTrue()
        {
            var f = FeatureFlags.Elite();
            Assert.True(f.MultiRule);
            Assert.True(f.TrimFlatten);
            Assert.True(f.BreakEven);
            Assert.True(f.AtrSizing);
            Assert.True(f.ClickTrader);
            Assert.True(f.MirrorMode);
            Assert.True(f.QxGlobalExit);
        }

        [Fact]
        public void T_BGTM1_FeatureFlags_FromFeatureList_OnlyMultiRule()
        {
            var f = FeatureFlags.FromFeatureList(new[] { "multi_rule" });
            Assert.True(f.MultiRule);
            Assert.False(f.TrimFlatten);
            Assert.False(f.BreakEven);
            Assert.False(f.AtrSizing);
            Assert.False(f.ClickTrader);
            Assert.False(f.MirrorMode);
            Assert.False(f.QxGlobalExit);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_ValidKey_FromFeatureList()
        {
            var feats = new List<string> { "multi_rule", "trim_flatten", "break_even" };
            var f = FeatureFlags.FromFeatureList(feats);
            Assert.True(f.MultiRule);
            Assert.True(f.TrimFlatten);
            Assert.True(f.BreakEven);
            Assert.False(f.AtrSizing);
            Assert.False(f.ClickTrader);
            Assert.False(f.MirrorMode);
            Assert.False(f.QxGlobalExit);
        }

        // Helper: build a JSON string in ISO-8601 round-trip format matching
        // what LicenseClient.WriteCache produces and DeserializeCache expects.
        private static string BuildCacheJson(string key, string[] features, DateTime expiresUtc)
        {
            var featureItems = string.Join(",", Array.ConvertAll(features, f => "\"" + f + "\""));
            return "{\"key\":\""
                + key
                + "\","
                + "\"features\":["
                + featureItems
                + "],"
                + "\"cached_utc\":\""
                + DateTime.UtcNow.ToString("o")
                + "\","
                + "\"expires_utc\":\""
                + expiresUtc.ToString("o")
                + "\"}";
        }
    }
}
