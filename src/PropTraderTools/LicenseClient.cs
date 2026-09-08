namespace PropTraderTools
{
    internal static class LicenseClient
    {
        // Test injection hook -- null means real path
        internal static string _testCachePath = null;

        private const string ProductId = "PTT_COPIER_V1";
        private const int CryptolensProductId = 1234;
        private const string AccessToken = "CRYPTOLENS_ACCESS_TOKEN_PLACEHOLDER";
        private const int CacheDaysValid = 7;

        private static string CachePath =>
            _testCachePath
            ?? System.IO.Path.Combine(
                NinjaTrader.Core.Globals.UserDataDir,
                "PropTraderTools",
                "license_cache.json"
            );

        // CYC=4. Never throws. Returns Starter on any failure.
        public static FeatureFlags Validate(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) // branch 1
                return FeatureFlags.Starter();
            var cached = TryReadCache(key);
            if (cached != null) // branch 2
                return cached;
            var remote = TryRemoteValidate(key);
            if (remote != null) // branch 3
            {
                WriteCache(key, remote);
                return remote;
            }
            return FeatureFlags.Starter();
        }

        // SKGL integration deferred -- remote validate returns null (Starter tier until licensed)
        private static FeatureFlags TryRemoteValidate(string key) => null;

        // CYC=5. Returns null if missing/expired/wrong key.
        private static FeatureFlags TryReadCache(string key)
        {
            try
            {
                var path = CachePath;
                if (!System.IO.File.Exists(path)) // branch 1
                    return null;
                var json = System.IO.File.ReadAllText(path);
                var entry = DeserializeCache(json);
                if (entry == null || entry.Key != key) // branch 2
                    return null;
                if (System.DateTime.UtcNow > entry.ExpiresUtc) // branch 3
                    return null;
                var features = entry.Features ?? new System.Collections.Generic.List<string>();
                return FeatureFlags.FromFeatureList(features);
            }
            catch
            {
                return null; // branch 4
            }
        }

        // CYC=5. Returns null on any parse error.
        private static CacheEntry DeserializeCache(string json)
        {
            try
            {
                var key = ExtractJsonString(json, "key");
                var exp = ExtractJsonString(json, "expires_utc");
                var feats = ExtractJsonArray(json, "features");
                if (key == null || exp == null) // branch 1
                    return null;
                return new CacheEntry
                {
                    Key = key,
                    ExpiresUtc = System.DateTime.Parse(
                        exp,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind
                    ),
                    Features = feats,
                    CachedUtc = System.DateTime.UtcNow,
                };
            }
            catch
            {
                return null;
            } // branch 2
        }

        // CYC=3. Extracts "fieldName":"value" from a flat JSON string.
        private static string ExtractJsonString(string json, string field)
        {
            var marker = "\"" + field + "\":\"";
            int start = json.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0)
                return null; // branch 1
            start += marker.Length;
            int end = json.IndexOf('"', start);
            return end < 0 ? null : json.Substring(start, end - start); // branch 2
        }

        // CYC=5. Extracts ["v1","v2",...] array values from a flat JSON string.
        private static System.Collections.Generic.List<string> ExtractJsonArray(
            string json,
            string field
        )
        {
            var result = new System.Collections.Generic.List<string>();
            var marker = "\"" + field + "\":[";
            int start = json.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0)
                return result; // branch 1
            start += marker.Length;
            int end = json.IndexOf(']', start);
            if (end < 0)
                return result; // branch 2
            var inner = json.Substring(start, end - start).Trim();
            if (inner.Length == 0)
                return result; // branch 3
            foreach (var raw in inner.Split(',')) // branch 4 (loop)
            {
                var v = raw.Trim().Trim('"');
                if (v.Length > 0)
                    result.Add(v);
            }
            return result;
        }

        // CYC=3. Swallows all exceptions.
        private static void WriteCache(string key, FeatureFlags flags)
        {
            try
            {
                var path = CachePath;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                var feats = GetFeatureList(flags);
                var sb = new System.Text.StringBuilder();
                sb.Append("{");
                sb.Append("\"key\":\"");
                sb.Append(EscapeJson(key));
                sb.Append("\",");
                sb.Append("\"cached_utc\":\"");
                sb.Append(System.DateTime.UtcNow.ToString("o"));
                sb.Append("\",");
                sb.Append("\"expires_utc\":\"");
                sb.Append(System.DateTime.UtcNow.AddDays(CacheDaysValid).ToString("o"));
                sb.Append("\",");
                sb.Append("\"features\":[");
                for (int i = 0; i < feats.Count; i++) // branch 1 (loop)
                {
                    if (i > 0)
                        sb.Append(","); // branch 2
                    sb.Append("\"");
                    sb.Append(EscapeJson(feats[i]));
                    sb.Append("\"");
                }
                sb.Append("]}");
                System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            }
            catch { }
        }

        // CYC=2. Minimal JSON string escaping (only chars that appear in license keys/feature names).
        private static string EscapeJson(string s)
        {
            return s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\""); // branch 1
        }

        private static System.Collections.Generic.List<string> GetFeatureList(FeatureFlags f)
        {
            var list = new System.Collections.Generic.List<string>();
            if (f.MultiRule)
                list.Add("multi_rule");
            if (f.TrimFlatten)
                list.Add("trim_flatten");
            if (f.BreakEven)
                list.Add("break_even");
            if (f.AtrSizing)
                list.Add("atr_sizing");
            if (f.ClickTrader)
                list.Add("click_trader");
            if (f.MirrorMode)
                list.Add("mirror_mode");
            if (f.QxGlobalExit)
                list.Add("qx_global_exit");
            return list;
        }

        // CYC=3. Returns "ELITE"/"PRO"/"STARTER".
        private static string InferTierName(FeatureFlags f)
        {
            if (f.AtrSizing)
                return "ELITE"; // branch 1
            if (f.MultiRule)
                return "PRO"; // branch 2
            return "STARTER";
        }

        private sealed class CacheEntry
        {
            public string Key { get; set; }
            public System.Collections.Generic.List<string> Features { get; set; }
            public System.DateTime CachedUtc { get; set; }
            public System.DateTime ExpiresUtc { get; set; }
        }
    }
}
