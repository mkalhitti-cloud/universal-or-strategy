"""
signals.py — Level state machine, signal detection, and confluence scoring.

State machine rules:
  ACTIVE  → INACTIVE : abs(current_price - level_price) < 10.0
  INACTIVE → ACTIVE  : abs(current_price - inactive_trigger_price) >= 10.0
  On server restart: all levels reset to ACTIVE (in-memory only)
"""
from __future__ import annotations

import math
from typing import Optional

# ─────────────────────────────────────────────
#  State store (in-memory, keyed by state key)
# ─────────────────────────────────────────────

# state_key → {"state": "ACTIVE"|"INACTIVE", "trigger_price": float}
_level_state: dict[str, dict] = {}


def _state_key(instrument: str, level_name: str, timeframe: str) -> str:
    return f"{instrument}::{level_name}::{timeframe}"


def reset_states() -> None:
    """Reset all level states (called on server start)."""
    _level_state.clear()


def _get_state(key: str) -> str:
    return _level_state.get(key, {}).get("state", "ACTIVE")


def _get_trigger_price(key: str) -> Optional[float]:
    return _level_state.get(key, {}).get("trigger_price")


def _set_state(key: str, state: str, trigger_price: Optional[float] = None) -> None:
    entry = _level_state.setdefault(key, {})
    entry["state"] = state
    if trigger_price is not None:
        entry["trigger_price"] = trigger_price


# ─────────────────────────────────────────────
#  State machine update
# ─────────────────────────────────────────────

# A level is INACTIVE when price is within this many ATR of it (price touched/too close).
# It reactivates once price moves away by the same ATR distance.
DEACTIVATION_ATR = 1.0   # 1 ATR — level is "touched", deactivate it
REACTIVATION_ATR = 1.0   # 1 ATR — price moved away enough to re-arm


def update_level_state(
    instrument: str, level: dict, current_price: float, atr: float = 5.0
) -> str:
    """
    Apply the state machine rules to a single level.
    Returns the updated state: 'ACTIVE' or 'INACTIVE'.

    Deactivation: when price comes within 1 ATR of the level (price touched it).
    Reactivation: when price moves at least 1 ATR away from where it deactivated.
    """
    key = _state_key(instrument, level["name"], level["timeframe"])
    current_state = _get_state(key)
    dist = abs(current_price - level["price"])
    deact_dist = DEACTIVATION_ATR * atr if atr > 0 else 5.0
    react_dist = REACTIVATION_ATR * atr if atr > 0 else 5.0

    if current_state == "ACTIVE":
        if dist < deact_dist:
            _set_state(key, "INACTIVE", trigger_price=current_price)
            return "INACTIVE"
        return "ACTIVE"
    else:  # INACTIVE
        trigger = _get_trigger_price(key) or current_price
        if abs(current_price - trigger) >= react_dist:
            _set_state(key, "ACTIVE")
            return "ACTIVE"
        return "INACTIVE"


# ─────────────────────────────────────────────
#  Confluence detection
# ─────────────────────────────────────────────

def find_confluences(levels: list[dict], atr_5min: float) -> list[dict]:
    """
    Group levels within ±0.25 × ATR into confluence zones.
    Adds 'confluence_with' list to each level that has confluence partners.
    Returns enriched levels list.
    """
    zone_radius = 0.25 * atr_5min if atr_5min > 0 else 1.0

    enriched = [dict(l, confluence_with=[]) for l in levels]

    for i, lvl_a in enumerate(enriched):
        for j, lvl_b in enumerate(enriched):
            if i == j:
                continue
            if abs(lvl_a["price"] - lvl_b["price"]) <= zone_radius:
                name_b = f"{lvl_b['name']} ({lvl_b['timeframe']})"
                if name_b not in lvl_a["confluence_with"]:
                    lvl_a["confluence_with"].append(name_b)

    return enriched


# ─────────────────────────────────────────────
#  Signal detection
# ─────────────────────────────────────────────

def _distance_atr(dist_pts: float, atr: float) -> float:
    return round(dist_pts / atr, 2) if atr > 0 else 0.0


def detect_signals(
    instrument: str,
    current_price: float,
    levels_data: dict,
) -> dict:
    """
    Run the full signal engine for one instrument.

    levels_data: output of levels.compute_levels()
    Returns signal dict consumed by the API endpoint.
    """
    raw_levels = levels_data.get("levels", [])
    atr = levels_data["atr_5min"]
    threshold = levels_data["atr_threshold"]
    atr_status = levels_data["atr_status"]
    ema9 = levels_data["ema_9_5min"]
    ema15 = levels_data["ema_15_5min"]
    trend_gap = levels_data["ema_trend_gap"]
    trend_valid = levels_data["ema_trend_valid"]

    # ── Update state machine for all levels ──────────────────
    stateful_levels: list[dict] = []
    for lvl in raw_levels:
        state = update_level_state(instrument, lvl, current_price, atr)
        stateful_levels.append({**lvl, "state": state})

    # ── Confluence enrichment ────────────────────────────────
    enriched = find_confluences(stateful_levels, atr)

    # ── Find LONG and SHORT signals ──────────────────────────
    # LONG  = support levels BELOW current price (we buy when price drops to them)
    # SHORT = resistance levels ABOVE current price (we sell when price rallies to them)
    above = [l for l in enriched if l["price"] > current_price]
    below = [l for l in enriched if l["price"] < current_price]

    # LONG  candidates: levels below price, nearest (highest price) first
    below.sort(key=lambda l: l["price"], reverse=True)
    # SHORT candidates: levels above price, nearest (lowest price) first
    above.sort(key=lambda l: l["price"])

    def make_signal_entry(lvl: dict, direction: str) -> dict:
        dist_pts = abs(current_price - lvl["price"])
        qualifies = dist_pts >= threshold and lvl["state"] == "ACTIVE"
        is_trend = lvl["name"] in ("EMA9", "EMA15") and lvl["timeframe"] == "5min"
        return {
            "direction": direction,
            "level_name": lvl["name"],
            "timeframe": lvl["timeframe"],
            "price": round(lvl["price"], 4),
            "distance_pts": round(dist_pts, 2),
            "distance_atr": _distance_atr(dist_pts, atr),
            "qualifies": qualifies,
            "state": lvl["state"],
            "confluence": lvl.get("confluence_with", []),
            "color_key": lvl.get("color_key", ""),
            "is_trend_level": is_trend,
            "trend_valid": trend_valid if is_trend else None,
        }

    # LONG signal = nearest qualifying level BELOW price
    long_signal: Optional[dict] = None
    all_long_candidates: list[dict] = []
    for lvl in below:
        entry = make_signal_entry(lvl, "LONG")
        all_long_candidates.append(entry)
        if long_signal is None and entry["qualifies"]:
            long_signal = entry

    # SHORT signal = nearest qualifying level ABOVE price
    short_signal: Optional[dict] = None
    all_short_candidates: list[dict] = []
    for lvl in above:
        entry = make_signal_entry(lvl, "SHORT")
        all_short_candidates.append(entry)
        if short_signal is None and entry["qualifies"]:
            short_signal = entry

    # ── Trend trade signals ──────────────────────────────────
    trend_long: Optional[dict] = None
    trend_short: Optional[dict] = None
    if trend_valid and ema9 > 0 and ema15 > 0:
        # 9 EMA above 15 EMA → bullish; below → bearish
        if ema9 > ema15 and ema15 > current_price:
            trend_long = {
                "direction": "TREND LONG",
                "level_name": "EMA9 (5min)",
                "price": round(ema9, 4),
                "distance_pts": round(abs(current_price - ema9), 2),
                "distance_atr": _distance_atr(abs(current_price - ema9), atr),
                "trend_gap": round(trend_gap, 2),
            }
        elif ema9 < ema15 and ema15 < current_price:
            trend_short = {
                "direction": "TREND SHORT",
                "level_name": "EMA9 (5min)",
                "price": round(ema9, 4),
                "distance_pts": round(abs(current_price - ema9), 2),
                "distance_atr": _distance_atr(abs(current_price - ema9), atr),
                "trend_gap": round(trend_gap, 2),
            }

    # ── Copy format strings ──────────────────────────────────
    def copy_str(entry: Optional[dict]) -> str:
        if entry is None:
            return ""
        tf_chip = f" ({entry['timeframe']})" if entry.get("timeframe") else ""
        conf = ""
        if entry.get("confluence"):
            conf_names = " + ".join(
                c.split(" (")[0] for c in entry["confluence"][:2]
            )
            conf = f" · ✦ {conf_names}"
        atr_m = entry.get("distance_atr", 0)
        # LONG levels are below price (−distance), SHORT levels are above (+distance)
        sign = "-" if entry["direction"] == "LONG" else "+"
        return (
            f"{instrument} {entry['direction']} · "
            f"{entry['level_name']}{tf_chip} · "
            f"{entry['price']:,.2f} · "
            f"{sign}{entry['distance_pts']} pts · "
            f"{atr_m} ATR"
            f"{conf}"
        )

    no_trade_copy = ""
    if atr_status in ("no-trade-low", "no-trade-high"):
        reason = "below minimum SL" if atr_status == "no-trade-low" else "above maximum SL"
        no_trade_copy = f"{instrument} · NO TRADE · ATR {atr:.1f} pts — {reason}"

    return {
        "instrument": instrument,
        "current_price": current_price,
        "atr_5min": round(atr, 2),
        "atr_threshold": round(threshold, 2),
        "atr_status": atr_status,
        "ema_9_5min": round(ema9, 4),
        "ema_15_5min": round(ema15, 4),
        "ema_trend_gap": round(trend_gap, 2),
        "ema_trend_valid": trend_valid,
        "long_signal": long_signal,
        "short_signal": short_signal,
        "trend_long": trend_long,
        "trend_short": trend_short,
        "all_long_candidates": all_long_candidates[:8],   # top 8 for ruler
        "all_short_candidates": all_short_candidates[:8],
        "long_copy": copy_str(long_signal),
        "short_copy": copy_str(short_signal),
        "no_trade_copy": no_trade_copy,
        "ruler_levels": [
            {
                "name": l["name"],
                "timeframe": l["timeframe"],
                "price": round(l["price"], 4),
                "state": l["state"],
                "color_key": l.get("color_key", ""),
                "confluence": l.get("confluence_with", []),
                "distance_pts": round(abs(current_price - l["price"]), 2),
                "distance_atr": _distance_atr(abs(current_price - l["price"]), atr),
                "side": "above" if l["price"] >= current_price else "below",
            }
            for l in enriched
        ],
    }
