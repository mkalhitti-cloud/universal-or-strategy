// NT8 Roslyn records shim -- CS0518 workaround (same pattern as FollowerAtmMode)
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace PropTraderTools
{
    internal sealed record FeatureFlags(
        bool MultiRule,
        bool TrimFlatten,
        bool BreakEven,
        bool AtrSizing,
        bool ClickTrader,
        bool MirrorMode,
        bool QxGlobalExit
    )
    {
        public static FeatureFlags Starter() =>
            new(false, false, false, false, false, false, false);

        public static FeatureFlags Pro() => new(true, true, true, false, false, false, false);

        public static FeatureFlags Elite() => new(true, true, true, true, true, true, true);

        public static FeatureFlags FromFeatureList(
            System.Collections.Generic.IEnumerable<string> feats
        ) =>
            new(
                MultiRule: System.Linq.Enumerable.Contains(feats, "multi_rule"),
                TrimFlatten: System.Linq.Enumerable.Contains(feats, "trim_flatten"),
                BreakEven: System.Linq.Enumerable.Contains(feats, "break_even"),
                AtrSizing: System.Linq.Enumerable.Contains(feats, "atr_sizing"),
                ClickTrader: System.Linq.Enumerable.Contains(feats, "click_trader"),
                MirrorMode: System.Linq.Enumerable.Contains(feats, "mirror_mode"),
                QxGlobalExit: System.Linq.Enumerable.Contains(feats, "qx_global_exit")
            );
    }
}
