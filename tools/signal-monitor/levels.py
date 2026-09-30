"""
levels.py — All level calculations for the WSGTA Signal Monitor.

Computes per-timeframe:
  - EMA 9, 15 (5min only)
  - EMA 30, 65, 200 (all timeframes)
  - VWAP + ±2σ bands (5min only, daily anchor)
  - Previous Day High / Low / Close
  - Previous Week High / Low / Close
  - Woodie Pivot Points (PP, R1-R3, S1-S3)
  - 14-period Wilder's ATR (always from 5min bars)
"""
from __future__ import annotations

import math
from datetime import datetime, timezone, timedelta
from typing import Optional

# ─────────────────────────────────────────────
#  Low-level math
# ─────────────────────────────────────────────

def ema(closes: list[float], period: int) -> float:
    """Exponential moving average (standard, not Wilder's)."""
    if not closes:
        return 0.0
    k = 2.0 / (period + 1)
    result = closes[0]
    for c in closes[1:]:
        result = c * k + result * (1 - k)
    return result


def wilders_atr(highs: list[float], lows: list[float], closes: list[float], period: int = 14) -> float:
    """Wilder's smoothed ATR."""
    if len(closes) < period + 1:
        return 0.0
    tr: list[float] = []
    for i in range(1, len(closes)):
        tr.append(max(
            highs[i] - lows[i],
            abs(highs[i] - closes[i - 1]),
            abs(lows[i] - closes[i - 1]),
        ))
    if len(tr) < period:
        return 0.0
    atr_val = sum(tr[:period]) / period
    for i in range(period, len(tr)):
        atr_val = (atr_val * (period - 1) + tr[i]) / period
    return atr_val


def woodie_pivots(pdh: float, pdl: float, pdc: float) -> dict[str, float]:
    """Woodie pivot points for the current session."""
    pp = (pdh + pdl + 2 * pdc) / 4
    r1 = 2 * pp - pdl
    s1 = 2 * pp - pdh
    r2 = pp + (pdh - pdl)
    s2 = pp - (pdh - pdl)
    r3 = pdh + 2 * (pp - pdl)
    s3 = pdl - 2 * (pdh - pp)
    return {"PP": pp, "R1": r1, "R2": r2, "R3": r3, "S1": s1, "S2": s2, "S3": s3}


# ─────────────────────────────────────────────
#  Previous day / week helpers
# ─────────────────────────────────────────────

def _ms_to_dt(ms: int) -> datetime:
    return datetime.fromtimestamp(ms / 1000, tz=timezone.utc)


def prev_day_hlc(daily_bars: list[dict]) -> Optional[dict[str, float]]:
    """Extract PDH, PDL, PDC from daily bars. 'Previous' = last completed bar."""
    if len(daily_bars) < 2:
        return None
    bar = daily_bars[-2]  # second-to-last = yesterday
    return {"PDH": bar["high"], "PDL": bar["low"], "PDC": bar["close"]}


def prev_week_hlc(daily_bars: list[dict]) -> Optional[dict[str, float]]:
    """Extract PWH, PWL, PWC from the last completed ISO week."""
    if not daily_bars:
        return None

    bars_by_week: dict[tuple[int, int], list[dict]] = {}
    for bar in daily_bars:
        dt = _ms_to_dt(bar["timestamp"])
        iso = dt.isocalendar()
        key = (iso.year, iso.week)
        bars_by_week.setdefault(key, []).append(bar)

    sorted_weeks = sorted(bars_by_week.keys())
    if len(sorted_weeks) < 2:
        return None

    # Use the second-to-last completed week
    prev_week_key = sorted_weeks[-2]
    week_bars = bars_by_week[prev_week_key]
    return {
        "PWH": max(b["high"] for b in week_bars),
        "PWL": min(b["low"] for b in week_bars),
        "PWC": week_bars[-1]["close"],
    }


# ─────────────────────────────────────────────
#  VWAP (daily anchor, 5min bars only)
# ─────────────────────────────────────────────

def compute_vwap_bands(bars_5min: list[dict]) -> Optional[dict[str, float]]:
    """
    Compute VWAP and ±2σ bands anchored to session open (9:30 AM ET).
    Returns {VWAP, VWAP_UPPER, VWAP_LOWER} or None if insufficient data.
    """
    # ET = UTC-4 (EDT) or UTC-5 (EST)
    # Use UTC-4 for simplicity; good enough for futures CME session
    ET_OFFSET = timedelta(hours=-4)

    def bar_et_hour_min(bar: dict) -> tuple[int, int]:
        dt = _ms_to_dt(bar["timestamp"])
        et = dt + ET_OFFSET
        return et.hour, et.minute

    # Find bars in today's session (9:30 AM ET onwards)
    session_bars = []
    for bar in reversed(bars_5min):
        h, m = bar_et_hour_min(bar)
        if h < 9 or (h == 9 and m < 30):
            break
        session_bars.insert(0, bar)

    if not session_bars:
        return None

    cum_tpv = 0.0
    cum_vol = 0.0
    cum_tp2v = 0.0  # sum of (tp^2 * vol) for variance

    for bar in session_bars:
        tp = (bar["high"] + bar["low"] + bar["close"]) / 3.0
        vol = max(bar["volume"], 1.0)
        cum_tpv += tp * vol
        cum_vol += vol
        cum_tp2v += (tp * tp) * vol

    if cum_vol == 0:
        return None

    vwap = cum_tpv / cum_vol
    variance = max(0.0, cum_tp2v / cum_vol - vwap ** 2)
    std_dev = math.sqrt(variance)

    return {
        "VWAP": vwap,
        "VWAP_UPPER": vwap + 2 * std_dev,
        "VWAP_LOWER": vwap - 2 * std_dev,
    }


# ─────────────────────────────────────────────
#  Main: compute all levels for one instrument
# ─────────────────────────────────────────────

def compute_levels(bars_by_tf: dict[str, list[dict]]) -> dict:
    """
    Given bars for each timeframe, compute all levels.

    bars_by_tf keys: '5min', '15min', '30min', '1hr', '2hr', '4hr', '1Day'
    Returns a dict with:
      - 'atr_5min': float
      - 'atr_threshold': float
      - 'atr_status': str
      - 'ema_9_5min', 'ema_15_5min': float (trend trade)
      - 'ema_trend_gap': float
      - 'ema_trend_valid': bool
      - 'levels': list of level dicts:
          {name, timeframe, price, color_key, include_in_ruler: True}
      - 'pivots': dict of Woodie pivots
      - 'vwap': dict or None
    """
    # ── ATR from 5min ──────────────────────────────────────────
    bars5 = bars_by_tf.get("5min", [])
    highs5 = [b["high"] for b in bars5]
    lows5 = [b["low"] for b in bars5]
    closes5 = [b["close"] for b in bars5]

    atr_val = wilders_atr(highs5, lows5, closes5, 14) if len(bars5) >= 15 else 0.0
    # Threshold = 2 × ATR — minimum distance for a level to qualify as a signal.
    # No hard floor: the ATR itself is the measure of meaningful distance.
    threshold = 2.0 * atr_val if atr_val > 0 else 10.0

    if atr_val < 4.0:
        atr_status = "no-trade-low"
    elif atr_val < 5.0:
        atr_status = "ideal"
    elif atr_val < 7.0:
        atr_status = "normal"
    elif atr_val < 10.0:
        atr_status = "caution"
    else:
        atr_status = "no-trade-high"

    # ── Trend EMAs (5min only) ─────────────────────────────────
    ema9 = ema(closes5, 9) if len(closes5) >= 9 else 0.0
    ema15 = ema(closes5, 15) if len(closes5) >= 15 else 0.0
    trend_gap = abs(ema9 - ema15)
    trend_valid = trend_gap <= 2.5

    # ── Daily + weekly pivot inputs ────────────────────────────
    daily_bars = bars_by_tf.get("1Day", [])
    pdhlc = prev_day_hlc(daily_bars)
    pwhlc = prev_week_hlc(daily_bars)
    pivots: dict[str, float] = {}
    if pdhlc:
        pivots = woodie_pivots(pdhlc["PDH"], pdhlc["PDL"], pdhlc["PDC"])

    # ── VWAP ──────────────────────────────────────────────────
    vwap_data = compute_vwap_bands(bars5) if bars5 else None

    # ── Collect all levels ────────────────────────────────────
    levels: list[dict] = []

    # EMA levels per timeframe
    ema_defs = [
        (9,   "EMA9",   "ema9",   ["5min"]),
        (15,  "EMA15",  "ema15",  ["5min"]),
        (30,  "EMA30",  "ema30",  ["5min", "15min", "30min", "1hr", "2hr", "4hr", "1Day"]),
        (65,  "EMA65",  "ema65",  ["5min", "15min", "30min", "1hr", "2hr", "4hr", "1Day"]),
        (200, "EMA200", "ema200", ["5min", "15min", "30min", "1hr", "2hr", "4hr", "1Day"]),
    ]

    for period, name, color_key, tfs in ema_defs:
        for tf in tfs:
            bars = bars_by_tf.get(tf, [])
            closes = [b["close"] for b in bars]
            if len(closes) >= period:
                price = ema(closes, period)
                levels.append({
                    "name": name,
                    "timeframe": tf,
                    "price": price,
                    "color_key": color_key,
                })

    # VWAP levels (5min only)
    if vwap_data:
        for key, color_key in [("VWAP", "vwap"), ("VWAP_UPPER", "vwap_band"), ("VWAP_LOWER", "vwap_band")]:
            levels.append({
                "name": key.replace("_", " ").replace("UPPER", "+2σ").replace("LOWER", "-2σ"),
                "timeframe": "5min",
                "price": vwap_data[key],
                "color_key": color_key,
            })

    # Previous Day levels
    if pdhlc:
        for key, color_key in [("PDH", "pdh"), ("PDL", "pdl"), ("PDC", "pdc")]:
            levels.append({"name": key, "timeframe": "Daily", "price": pdhlc[key], "color_key": color_key})

    # Previous Week levels
    if pwhlc:
        for key, color_key in [("PWH", "pwh"), ("PWL", "pwl"), ("PWC", "pdc")]:
            levels.append({"name": key, "timeframe": "Weekly", "price": pwhlc[key], "color_key": color_key})

    # Pivot points
    pivot_colors = {
        "PP": "pivot_pp", "R1": "pivot_r", "R2": "pivot_r", "R3": "pivot_r",
        "S1": "pivot_s", "S2": "pivot_s", "S3": "pivot_s",
    }
    for pname, price in pivots.items():
        levels.append({
            "name": pname,
            "timeframe": "Daily",
            "price": price,
            "color_key": pivot_colors.get(pname, "pivot_pp"),
        })

    return {
        "atr_5min": atr_val,
        "atr_threshold": threshold,
        "atr_status": atr_status,
        "ema_9_5min": ema9,
        "ema_15_5min": ema15,
        "ema_trend_gap": trend_gap,
        "ema_trend_valid": trend_valid,
        "levels": levels,
        "pivots": pivots,
        "vwap": vwap_data,
    }
