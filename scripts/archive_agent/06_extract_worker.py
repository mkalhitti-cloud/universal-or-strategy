"""
ARCHIVE PIPELINE — TIER 3: EXTRACT WORKER
==========================================
Operated by: Bob accounts 61-75

Reads clip manifest and uses FFmpeg to extract each clip.
Handles: shorts, medium clips, full sessions.

Usage:
    python scripts/archive_agent/06_extract_worker.py --session session_001
    python scripts/archive_agent/06_extract_worker.py --session session_001 --type short
    python scripts/archive_agent/06_extract_worker.py --session session_001 --type medium
"""

import json
import argparse
import subprocess
from pathlib import Path
from datetime import datetime

MANIFEST_PATH = Path("archive/archive_manifest.json")
TRANSCRIPTS_DIR = Path("archive/transcripts")
CLIPS_DIR = Path("archive/clips")


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


def extract_clip(video_path: Path, output_path: Path,
                 start: float, end: float, clip_type: str) -> bool:
    """
    Extract a clip from video using FFmpeg.
    Uses fast seek for efficiency.
    """
    output_path.parent.mkdir(parents=True, exist_ok=True)

    if output_path.exists():
        print(f"    Already extracted: {output_path.name}")
        return True

    duration = end - start

    # FFmpeg command — fast seek + re-encode for clean cuts
    cmd = [
        "ffmpeg",
        "-ss", str(start),          # seek to start (fast seek)
        "-i", str(video_path),
        "-t", str(duration),         # duration
        "-c:v", "libx264",           # re-encode video for clean cut
        "-c:a", "aac",               # re-encode audio
        "-crf", "23",                # quality (23 = good balance)
        "-preset", "fast",           # encoding speed
        "-movflags", "+faststart",   # web optimized
    ]

    # Shorts: crop to 9:16 vertical if needed
    if clip_type == "short":
        cmd += [
            "-vf", "scale=1080:1920:force_original_aspect_ratio=decrease,"
                   "pad=1080:1920:(ow-iw)/2:(oh-ih)/2",
        ]

    cmd += ["-y", str(output_path)]

    result = subprocess.run(cmd, capture_output=True, text=True)

    if result.returncode != 0:
        print(f"    ❌ FFmpeg failed: {result.stderr[-300:]}")
        return False

    size_mb = output_path.stat().st_size / 1024 / 1024
    print(f"    ✅ {output_path.name} ({duration:.0f}s, {size_mb:.1f}MB)")
    return True


def main():
    parser = argparse.ArgumentParser(description="Tier 3 Extract Worker")
    parser.add_argument("--session", required=True, help="Session ID")
    parser.add_argument("--type", choices=["short", "medium", "full", "all"],
                        default="all", help="Clip type to extract")
    args = parser.parse_args()

    manifest = load_manifest()
    session = next((s for s in manifest["sessions"] if s["id"] == args.session), None)
    if not session:
        print(f"Session not found: {args.session}")
        return

    print(f"\n[EXTRACT] {args.session}: {session['title'][:60]}")

    # Find video
    video_path = Path(session.get("raw_video_path",
                                   f"archive/raw/{args.session}.mp4"))
    if not video_path.exists():
        print(f"  ❌ Video not found: {video_path}")
        return

    # Load clip manifest
    clips_path = Path(session.get("analysis_path",
                                   TRANSCRIPTS_DIR / f"{args.session}_clips.json"))
    if not clips_path.exists():
        print(f"  ❌ Clips manifest not found. Run analyze worker first.")
        return

    with open(clips_path, encoding="utf-8") as f:
        clip_manifest = json.load(f)

    clips = clip_manifest.get("clips", [])
    if args.type != "all":
        clips = [c for c in clips if c["type"] == args.type]

    print(f"  Extracting {len(clips)} clips (type: {args.type})")
    print(f"  Source: {video_path}")

    success_count = 0
    fail_count = 0

    for clip in clips:
        output_path = Path(clip["output_path"])
        print(f"\n  [{clip['type'].upper()}] {clip['clip_id']}")
        print(f"    {clip['start']}s → {clip['end']}s "
              f"({clip['duration']/60:.1f} min)")
        if clip.get("trigger"):
            print(f"    Trigger: {clip['trigger']}")

        ok = extract_clip(
            video_path=video_path,
            output_path=output_path,
            start=clip["start"],
            end=clip["end"],
            clip_type=clip["type"],
        )

        if ok:
            clip["status"] = "extracted"
            clip["extracted_at"] = datetime.utcnow().isoformat()
            clip["file_size_mb"] = round(output_path.stat().st_size / 1024 / 1024, 1)
            success_count += 1
        else:
            clip["status"] = "failed"
            fail_count += 1

    # Update clip manifest
    with open(clips_path, "w", encoding="utf-8") as f:
        json.dump(clip_manifest, f, indent=2)

    # Update session status
    all_extracted = all(c["status"] == "extracted"
                        for c in clip_manifest["clips"])
    new_status = "metadata" if all_extracted else "extracting"

    update_session(manifest, args.session, {
        "status": new_status,
        "clips": clip_manifest["clips"],
    })

    print(f"\n  Summary: {success_count} extracted, {fail_count} failed")
    print(f"  Status: extracting → {new_status}")
    print(f"  ✅ Ready for Tier 3 metadata worker")


if __name__ == "__main__":
    main()
