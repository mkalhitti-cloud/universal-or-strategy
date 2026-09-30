"""
ARCHIVE PIPELINE — TIER 3: METADATA WORKER
===========================================
Operated by: Bob accounts 76-90

Generates YouTube-optimized metadata for each clip:
  - Compelling title (CTR optimized)
  - Description with chapters and timestamps
  - Tags (SEO optimized for trading niche)
  - Recommended playlist assignment
  - Thumbnail prompt (for Higgsfield MCP)
  - Post time recommendation

Uses Claude API if available, falls back to template-based generation.

Usage:
    python scripts/archive_agent/07_metadata_worker.py --session session_001
"""

import json
import argparse
import os
import re
from pathlib import Path
from datetime import datetime

MANIFEST_PATH = Path("archive/archive_manifest.json")
TRANSCRIPTS_DIR = Path("archive/transcripts")
METADATA_DIR = Path("archive/metadata")
METADATA_DIR.mkdir(parents=True, exist_ok=True)

# Trading niche SEO tags — high search volume
BASE_TAGS = [
    "futures trading", "trading education", "prop firm",
    "apex trader funding", "ninjatrader", "tradovate",
    "es futures", "nq futures", "day trading",
    "trading psychology", "risk management", "funded trader",
    "trading strategy", "technical analysis", "ema trading",
]

PILLAR_TAGS = {
    "prop_firm": ["apex trader funding", "topstep", "ftmo", "prop firm challenge",
                  "funded trading", "pass prop firm", "prop firm rules"],
    "ninjatrader": ["ninjatrader tutorial", "ninjatrader setup", "atm strategy",
                    "ninjatrader 8", "ninjatrader apex"],
    "tradovate": ["tradovate tutorial", "tradovate setup", "tradovate review"],
    "psychology": ["trading psychology", "trading mindset", "trading discipline",
                   "trading rules", "trading fear", "fomo trading"],
    "strategy": ["ffma trade", "far from moving average", "trend trade",
                 "base trade", "orb strategy", "opening range breakout",
                 "rma trade", "reversal swing trade"],
    "futures": ["futures trading", "es futures", "nq futures", "mes mnq",
                "futures day trading", "emini futures"],
    "peter_tuchman": ["peter tuchman", "nyse floor trader", "wall street",
                      "einstein of wall street"],
}

PLAYLIST_MAP = {
    1: "Peter Tuchman Q&A Series",
    2: "Trading Psychology Mastery",
    3: "Prop Firm Mastery",
    4: "NinjaTrader Complete Guide",
    5: "The Trading Strategy System",
    6: "Trading Fundamentals",
}

# Post time recommendations by day
POST_TIMES = {
    0: "07:00",  # Monday — pre-market
    1: "18:00",  # Tuesday — post-market
    2: "07:00",  # Wednesday — pre-market
    3: "18:00",  # Thursday — post-market
    4: "09:00",  # Friday — mid-morning
    5: "10:00",  # Saturday — weekend
    6: "18:00",  # Sunday — pre-week
}


def load_manifest() -> dict:
    with open(MANIFEST_PATH, encoding="utf-8") as f:
        return json.load(f)


def save_manifest(manifest: dict):
    manifest["updated_at"] = datetime.utcnow().isoformat()
    with open(MANIFEST_PATH, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)


def update_session(manifest: dict, session_id: str, updates: dict):
    for s in manifest["sessions"]:
        if s["id"] == session_id:
            s.update(updates)
            s["updated_at"] = datetime.utcnow().isoformat()
            break
    save_manifest(manifest)


def detect_pillar(title: str, context: str) -> str:
    """Detect which content pillar this clip belongs to."""
    text = (title + " " + context).lower()
    if "peter" in text or "tuchman" in text:
        return "peter_tuchman"
    if "apex" in text or "topstep" in text or "ftmo" in text or "prop firm" in text:
        return "prop_firm"
    if "ninjatrader" in text or "atm" in text:
        return "ninjatrader"
    if "tradovate" in text:
        return "tradovate"
    if any(k in text for k in ["psychology", "discipline", "fear", "fomo",
                                "losing", "rules", "mindset"]):
        return "psychology"
    if any(k in text for k in ["ffma", "rma", "trend trade", "base trade",
                                "orb", "reversal"]):
        return "strategy"
    if any(k in text for k in ["futures", "/es", "/nq", "emini", "mes", "mnq"]):
        return "futures"
    return "general"


def generate_tags(pillar: str, clip_type: str) -> list[str]:
    tags = BASE_TAGS.copy()
    if pillar in PILLAR_TAGS:
        tags = PILLAR_TAGS[pillar] + tags
    if clip_type == "short":
        tags += ["trading shorts", "trading tips", "daytrading shorts"]
    return list(dict.fromkeys(tags))[:15]  # deduplicate, cap at 15


def generate_thumbnail_prompt(title: str, pillar: str, clip_type: str) -> str:
    """Generate a Higgsfield MCP prompt for thumbnail creation."""
    base = "High-contrast YouTube thumbnail, bold text overlay, "
    if pillar == "peter_tuchman":
        return base + "NYSE trading floor background, dramatic lighting, text: " + title[:40]
    if pillar == "prop_firm":
        return base + "trader at monitor with green charts, funded trader badge, text: " + title[:40]
    if pillar == "psychology":
        return base + "trader looking focused and determined, dark dramatic background, text: " + title[:40]
    if pillar == "strategy":
        return base + "trading chart with arrows and indicators highlighted, text: " + title[:40]
    if pillar == "ninjatrader":
        return base + "NinjaTrader platform screenshot with annotations, text: " + title[:40]
    return base + "trading charts and financial data background, text: " + title[:40]


def generate_metadata_with_claude(clip: dict, session: dict,
                                   transcript_preview: str) -> dict:
    """Use Claude to generate compelling YouTube metadata."""
    api_key = os.environ.get("ANTHROPIC_API_KEY")
    if not api_key:
        return generate_metadata_template(clip, session)

    try:
        import anthropic
        client = anthropic.Anthropic(api_key=api_key)

        clip_type = clip["type"]
        context = clip.get("context", "")
        trigger = clip.get("trigger", "")
        duration = clip.get("duration", 0)

        prompt = f"""Generate YouTube metadata for a trading education clip.

Session: {session['title']}
Clip type: {clip_type} ({duration:.0f} seconds)
Trigger moment: {trigger}
Context: {context}
Transcript preview: {transcript_preview[:500]}

Generate:
1. title: Compelling YouTube title (50-60 chars, high CTR for trading audience)
2. description: YouTube description (150-200 words, include what viewers learn)
3. tags: 15 tags (comma separated, trading niche SEO)
4. hook: First 2 sentences for the video description
5. chapters: If medium/full, list of timestamps and topics

Rules for title:
- For shorts: Start with the hook moment, create curiosity
- For medium: Clear benefit + "Complete Guide" or "Explained" or "Step-by-Step"  
- For full: Include date and main topics covered
- Never clickbait — must deliver what the title promises
- Finance audience = direct, professional, specific

Return as JSON with keys: title, description, tags, hook, chapters
"""
        message = client.messages.create(
            model="claude-sonnet-4-5",
            max_tokens=1000,
            messages=[{"role": "user", "content": prompt}]
        )

        response_text = message.content[0].text
        json_match = re.search(r'\{.*\}', response_text, re.DOTALL)
        if json_match:
            return json.loads(json_match.group())
    except Exception as e:
        print(f"  Claude metadata failed: {e} — using template")

    return generate_metadata_template(clip, session)


def generate_metadata_template(clip: dict, session: dict) -> dict:
    """Template-based metadata generation (no API required)."""
    clip_type = clip["type"]
    context = clip.get("context", "")
    trigger = clip.get("trigger", "")
    session_title = session["title"]
    pillar = detect_pillar(session_title, context)

    # Build title based on type
    if clip_type == "short":
        if trigger in ["blew", "blow", "blow account", "blown"]:
            title = "I Blew My Apex Account — Here's What I Learned"
        elif trigger in ["fomo"]:
            title = "FOMO Is Killing Your Trading (Fix This Now)"
        elif trigger in ["full time job"]:
            title = "Can You Trade Futures With a Full Time Job?"
        elif trigger in ["$18", "18 dollars"]:
            title = "Opening an Apex Account for $18 (Live)"
        else:
            title = f"Trading Insight: {context[:45]}..."
    elif clip_type == "medium":
        if pillar == "prop_firm":
            title = f"Apex Trader Funding Explained — {context[:35]}"
        elif pillar == "peter_tuchman":
            title = f"Peter Tuchman on {context[:40]} | NYSE Floor Trader"
        elif pillar == "strategy":
            title = f"{trigger.upper()} Trade Setup — Complete Walkthrough"
        elif pillar == "psychology":
            title = f"Trading Psychology: {context[:45]}"
        else:
            title = f"{session_title[:55]}"
    else:  # full
        title = f"Full Mentorship Session: {session_title[:45]}"

    description = f"""In this video: {context}

Topics covered:
- {trigger.title() if trigger else 'Key trading concepts'}
- Risk management and discipline
- Real trade examples and analysis

This is from our weekly mentorship program — real students, real trades, real results.

🔔 Subscribe for weekly trading education
📈 Check our Prop Firm Mastery playlist for more

#trading #futures #propfirm #ninjatrader #tradovate
"""

    return {
        "title": title,
        "description": description,
        "tags": generate_tags(pillar, clip_type),
        "hook": context[:120] if context else session_title[:120],
        "chapters": [],
    }


def main():
    parser = argparse.ArgumentParser(description="Tier 3 Metadata Worker")
    parser.add_argument("--session", required=True, help="Session ID")
    args = parser.parse_args()

    manifest = load_manifest()
    session = next((s for s in manifest["sessions"] if s["id"] == args.session), None)
    if not session:
        print(f"Session not found: {args.session}")
        return

    print(f"\n[METADATA] {args.session}: {session['title'][:60]}")

    clips_path = Path(session.get("analysis_path",
                                   TRANSCRIPTS_DIR / f"{args.session}_clips.json"))
    if not clips_path.exists():
        print(f"  ❌ Clips manifest not found. Run analyze worker first.")
        return

    with open(clips_path, encoding="utf-8") as f:
        clip_manifest = json.load(f)

    # Load transcript preview for Claude
    transcript_path = Path(session.get("transcript_path",
                                        TRANSCRIPTS_DIR / f"{args.session}.json"))
    transcript_preview = ""
    if transcript_path.exists():
        with open(transcript_path, encoding="utf-8") as f:
            t = json.load(f)
            transcript_preview = t.get("full_text", "")[:1000]

    clips = [c for c in clip_manifest["clips"] if c["status"] == "extracted"]
    print(f"  Processing metadata for {len(clips)} extracted clips")

    metadata_output = {
        "session_id": args.session,
        "session_title": session["title"],
        "priority": session["priority"],
        "playlist": PLAYLIST_MAP.get(session["priority"], "Trading Fundamentals"),
        "generated_at": datetime.utcnow().isoformat(),
        "clips_metadata": []
    }

    for clip in clips:
        pillar = detect_pillar(session["title"], clip.get("context", ""))
        print(f"  Generating: {clip['clip_id']} ({clip['type']}, pillar: {pillar})")

        meta = generate_metadata_with_claude(clip, session, transcript_preview)

        clip_meta = {
            "clip_id": clip["clip_id"],
            "type": clip["type"],
            "output_path": clip["output_path"],
            "pillar": pillar,
            "playlist": PLAYLIST_MAP.get(session["priority"], "Trading Fundamentals"),
            "title": meta.get("title", ""),
            "description": meta.get("description", ""),
            "tags": meta.get("tags", []),
            "hook": meta.get("hook", ""),
            "chapters": meta.get("chapters", []),
            "thumbnail_prompt": generate_thumbnail_prompt(
                meta.get("title", ""), pillar, clip["type"]
            ),
            "recommended_post_time": POST_TIMES.get(datetime.utcnow().weekday(), "09:00"),
            "upload_status": "ready",
        }
        metadata_output["clips_metadata"].append(clip_meta)

        # Update clip in manifest
        clip["youtube_title"] = meta.get("title", "")
        clip["youtube_description"] = meta.get("description", "")
        clip["youtube_tags"] = meta.get("tags", [])
        clip["status"] = "metadata_complete"

    # Save metadata
    metadata_path = METADATA_DIR / f"{args.session}_metadata.json"
    with open(metadata_path, "w", encoding="utf-8") as f:
        json.dump(metadata_output, f, indent=2, ensure_ascii=False)

    # Update clip manifest
    with open(clips_path, "w", encoding="utf-8") as f:
        json.dump(clip_manifest, f, indent=2)

    # Update session as complete
    update_session(manifest, args.session, {
        "status": "complete",
        "metadata_path": str(metadata_path),
        "clips": clip_manifest["clips"],
    })

    print(f"\n  Clips with metadata: {len(metadata_output['clips_metadata'])}")
    print(f"  Playlist: {metadata_output['playlist']}")
    print(f"  Saved: {metadata_path}")
    print(f"  Status: metadata → ✅ COMPLETE")


if __name__ == "__main__":
    main()
