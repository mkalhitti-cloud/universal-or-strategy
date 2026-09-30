"""
ARCHIVE PIPELINE — TIER 3: ANALYZE WORKER
==========================================
Operated by: Bob accounts 46-60

Reads transcript JSON and uses Claude (via Anthropic API) to:
  1. Identify SHORT clip boundaries (<60s) — hook moments, key insights
  2. Identify MEDIUM clip boundaries (8-15 min) — complete topic segments
  3. Identify FULL session markers — minimal processing needed
  4. Generate clip titles and types

Outputs: archive/transcripts/{session_id}_clips.json

Usage:
    python scripts/archive_agent/05_analyze_worker.py --session session_001
"""

import json
import argparse
import os
from pathlib import Path
from datetime import datetime

MANIFEST_PATH = Path("archive/archive_manifest.json")
TRANSCRIPTS_DIR = Path("archive/transcripts")

# Psychology / hook keywords that signal Short-worthy moments
SHORT_SIGNAL_KEYWORDS = [
    "blew", "blown", "blow up", "blow account",
    "lost everything", "worst trade", "biggest mistake",
    "never do this", "one rule", "most important rule",
    "changed my trading", "stopped losing", "finally profitable",
    "don't do this", "rookie mistake", "beginner mistake",
    "fomo", "fear", "scared", "emotional",
    "can't believe", "incredible", "amazing trade",
    "full time job", "quit my job", "fired", "$18", "18 dollars",
    "passed", "failed the challenge", "funded",
]

# Topic boundary keywords that signal Medium clip start
TOPIC_KEYWORDS = [
    "let's talk about", "today we're going to", "next topic",
    "moving on to", "let me show you", "here's how",
    "the rule is", "when you see", "the setup is",
    "far from moving average", "ffma", "trend trade",
    "opening range", "orb", "base trade",
    "apex", "topstep", "ftmo", "prop firm",
    "ninjatrader", "tradovate", "thinkorswim",
    "psychology", "discipline", "rules",
    "risk management", "stop loss", "atr",
    "peter", "tuchman",
]


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


def find_short_candidates(segments: list[dict]) -> list[dict]:
    """
    Scan transcript segments for Short-worthy moments.
    Returns list of {start, end, trigger_word, text_context}
    """
    candidates = []
    for i, seg in enumerate(segments):
        text_lower = seg["text"].lower()
        for keyword in SHORT_SIGNAL_KEYWORDS:
            if keyword in text_lower:
                # Build a ~45 second window around this moment
                start = max(0, seg["start"] - 5)
                end = min(seg["start"] + 40, seg["end"] + 30)
                # Avoid duplicates too close together
                if not any(abs(c["start"] - start) < 30 for c in candidates):
                    candidates.append({
                        "start": round(start, 1),
                        "end": round(end, 1),
                        "trigger": keyword,
                        "context": seg["text"][:120],
                        "type": "short",
                    })
                break
    return candidates


def find_medium_clip_candidates(segments: list[dict]) -> list[dict]:
    """
    Scan for topic boundaries — places where a new subject begins.
    Groups segments into 8-15 minute topic blocks.
    """
    topic_starts = []
    for seg in segments:
        text_lower = seg["text"].lower()
        for keyword in TOPIC_KEYWORDS:
            if keyword in text_lower:
                if not any(abs(t["start"] - seg["start"]) < 300 for t in topic_starts):
                    topic_starts.append({
                        "start": seg["start"],
                        "text": seg["text"][:120],
                        "keyword": keyword,
                    })
                break

    # Build medium clips from topic starts
    clips = []
    for i, topic in enumerate(topic_starts):
        start = topic["start"]
        # End = next topic start or +12 minutes
        if i + 1 < len(topic_starts):
            end = min(topic_starts[i + 1]["start"], start + 900)  # cap at 15 min
        else:
            end = start + 720  # default 12 minutes

        duration = end - start
        if duration >= 480:  # at least 8 minutes
            clips.append({
                "start": round(start, 1),
                "end": round(end, 1),
                "trigger": topic["keyword"],
                "context": topic["text"],
                "type": "medium",
                "duration_minutes": round(duration / 60, 1),
            })

    return clips


def analyze_with_claude(transcript: dict, session: dict) -> dict:
    """
    Use Claude API to intelligently analyze transcript for clips.
    Falls back to keyword analysis if no API key set.
    """
    api_key = os.environ.get("ANTHROPIC_API_KEY")

    if not api_key:
        print("  No ANTHROPIC_API_KEY — using keyword analysis fallback")
        return keyword_analysis(transcript, session)

    try:
        import anthropic
        client = anthropic.Anthropic(api_key=api_key)

        # Send first 8000 chars of transcript to Claude for analysis
        full_text_preview = transcript["full_text"][:8000]

        prompt = f"""You are analyzing a trading education session transcript to identify the best clips.

Session: {session['title']}
Priority: P{session['priority']} (1=highest value)
Duration: {transcript.get('duration_seconds', 0)/60:.0f} minutes

Transcript preview:
{full_text_preview}

Identify:
1. SHORT clips (<60 seconds) — emotionally compelling moments, key insights, shocking facts, psychology moments
2. MEDIUM clips (8-15 minutes) — complete topic segments with clear start/end
3. Whether this session has a FULL session worth uploading

For each clip provide:
- start_time_hint: approximate time in minutes (not exact)
- title: compelling YouTube title
- type: short|medium|full
- hook: first sentence to grab attention
- why: why this will perform well

Return as JSON array of clip objects. Max 5 shorts, max 4 medium clips.
"""
        message = client.messages.create(
            model="claude-sonnet-4-5",
            max_tokens=2000,
            messages=[{"role": "user", "content": prompt}]
        )

        response_text = message.content[0].text
        # Extract JSON from response
        import re
        json_match = re.search(r'\[.*\]', response_text, re.DOTALL)
        if json_match:
            claude_clips = json.loads(json_match.group())
            return {
                "method": "claude",
                "claude_suggestions": claude_clips,
            }
    except Exception as e:
        print(f"  Claude analysis failed: {e} — falling back to keyword analysis")

    return keyword_analysis(transcript, session)


def keyword_analysis(transcript: dict, session: dict) -> dict:
    """Fallback: pure keyword-based clip detection."""
    segments = transcript.get("segments", [])
    shorts = find_short_candidates(segments)
    mediums = find_medium_clip_candidates(segments)
    return {
        "method": "keyword",
        "shorts": shorts,
        "mediums": mediums,
    }


def generate_clip_manifest(session_id: str, session: dict,
                            transcript: dict, analysis: dict) -> dict:
    """Build final clip manifest for the extract worker."""
    clips = []
    clip_num = 1

    # Add shorts
    for s in analysis.get("shorts", []):
        clips.append({
            "clip_id": f"{session_id}_short_{clip_num:02d}",
            "type": "short",
            "start": s["start"],
            "end": s["end"],
            "duration": round(s["end"] - s["start"], 1),
            "trigger": s.get("trigger", ""),
            "context": s.get("context", ""),
            "output_path": f"archive/clips/shorts/{session_id}_short_{clip_num:02d}.mp4",
            "youtube_title": None,  # filled by metadata worker
            "youtube_description": None,
            "youtube_tags": [],
            "status": "pending_extract",
        })
        clip_num += 1

    # Add medium clips
    for m in analysis.get("mediums", []):
        clips.append({
            "clip_id": f"{session_id}_medium_{clip_num:02d}",
            "type": "medium",
            "start": m["start"],
            "end": m["end"],
            "duration": round(m["end"] - m["start"], 1),
            "trigger": m.get("trigger", ""),
            "context": m.get("context", ""),
            "output_path": f"archive/clips/medium/{session_id}_medium_{clip_num:02d}.mp4",
            "youtube_title": None,
            "youtube_description": None,
            "youtube_tags": [],
            "status": "pending_extract",
        })
        clip_num += 1

    # Add full session
    duration = transcript.get("duration_seconds", 0)
    if duration > 1800:  # only sessions > 30 min get full upload
        clips.append({
            "clip_id": f"{session_id}_full",
            "type": "full",
            "start": 0,
            "end": duration,
            "duration": duration,
            "output_path": f"archive/clips/full/{session_id}_full.mp4",
            "youtube_title": None,
            "youtube_description": None,
            "youtube_tags": [],
            "status": "pending_extract",
        })

    return {
        "session_id": session_id,
        "session_title": session["title"],
        "priority": session["priority"],
        "analyzed_at": datetime.utcnow().isoformat(),
        "analysis_method": analysis.get("method", "keyword"),
        "total_clips": len(clips),
        "shorts_count": sum(1 for c in clips if c["type"] == "short"),
        "medium_count": sum(1 for c in clips if c["type"] == "medium"),
        "full_count": sum(1 for c in clips if c["type"] == "full"),
        "clips": clips,
    }


def main():
    parser = argparse.ArgumentParser(description="Tier 3 Analyze Worker")
    parser.add_argument("--session", required=True, help="Session ID (e.g. session_001)")
    args = parser.parse_args()

    manifest = load_manifest()
    session = next((s for s in manifest["sessions"] if s["id"] == args.session), None)
    if not session:
        print(f"Session not found: {args.session}")
        return

    print(f"\n[ANALYZE] {args.session}: {session['title'][:60]}")

    transcript_path = Path(session.get("transcript_path",
                                        TRANSCRIPTS_DIR / f"{args.session}.json"))
    if not transcript_path.exists():
        print(f"  ❌ Transcript not found: {transcript_path}")
        print(f"  Run transcribe worker first.")
        return

    clips_path = TRANSCRIPTS_DIR / f"{args.session}_clips.json"

    if clips_path.exists():
        print(f"  Already analyzed: {clips_path}")
        update_session(manifest, args.session, {
            "status": "extracting",
            "analysis_path": str(clips_path),
        })
        return

    try:
        with open(transcript_path, encoding="utf-8") as f:
            transcript = json.load(f)

        print(f"  Transcript: {len(transcript.get('segments', []))} segments")
        print(f"  Duration: {transcript.get('duration_seconds', 0)/60:.1f} min")

        # Analyze
        analysis = analyze_with_claude(transcript, session)

        # Build clip manifest
        clip_manifest = generate_clip_manifest(
            args.session, session, transcript, analysis
        )

        # Save
        with open(clips_path, "w", encoding="utf-8") as f:
            json.dump(clip_manifest, f, indent=2, ensure_ascii=False)

        update_session(manifest, args.session, {
            "status": "extracting",
            "analysis_path": str(clips_path),
            "clips": clip_manifest["clips"],
        })

        print(f"  Shorts found:  {clip_manifest['shorts_count']}")
        print(f"  Medium clips:  {clip_manifest['medium_count']}")
        print(f"  Full sessions: {clip_manifest['full_count']}")
        print(f"  Saved: {clips_path}")
        print(f"  Status: analyzing → extracting")
        print(f"  ✅ Ready for Tier 3 extract worker")

    except Exception as e:
        print(f"  ❌ Analysis failed: {e}")
        update_session(manifest, args.session, {
            "status": "failed",
            "error": str(e),
        })


if __name__ == "__main__":
    main()
