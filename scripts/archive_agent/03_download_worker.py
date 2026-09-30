"""
ARCHIVE PIPELINE — TIER 3: DOWNLOAD WORKER
===========================================
Operated by: Bob accounts 21-30

Handles two sources:
  - Teachable: uses yt-dlp with session cookies
  - Local: copies file to archive/raw/

Usage:
    python scripts/archive_agent/03_download_worker.py --session session_001
    python scripts/archive_agent/03_download_worker.py --session session_001 --local "C:/Videos/session.mp4"
    python scripts/archive_agent/03_download_worker.py --session session_001 --url "https://..."
"""

import json
import argparse
import subprocess
import shutil
from pathlib import Path
from datetime import datetime

MANIFEST_PATH = Path("archive/archive_manifest.json")
RAW_DIR = Path("archive/raw")
RAW_DIR.mkdir(parents=True, exist_ok=True)

COOKIES_FILE = Path(".env.teachable_cookies")  # exported from browser


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


def download_from_teachable(session_id: str, url: str) -> Path:
    """Download video from Teachable using yt-dlp."""
    output_path = RAW_DIR / f"{session_id}.mp4"

    if output_path.exists():
        print(f"  Already downloaded: {output_path}")
        return output_path

    cmd = [
        "yt-dlp",
        "--format", "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best",
        "--output", str(output_path),
        "--merge-output-format", "mp4",
        "--no-playlist",
    ]

    # Add cookies if available (needed for Teachable auth)
    if COOKIES_FILE.exists():
        cmd += ["--cookies", str(COOKIES_FILE)]

    cmd.append(url)

    print(f"  Downloading: {url}")
    print(f"  Output: {output_path}")

    result = subprocess.run(cmd, capture_output=True, text=True)

    if result.returncode != 0:
        raise RuntimeError(f"yt-dlp failed:\n{result.stderr}")

    print(f"  ✅ Download complete: {output_path}")
    return output_path


def copy_local_file(session_id: str, local_path: str) -> Path:
    """Copy a local video file into the archive/raw/ directory."""
    src = Path(local_path)
    if not src.exists():
        raise FileNotFoundError(f"Local file not found: {local_path}")

    dest = RAW_DIR / f"{session_id}{src.suffix}"
    if not dest.exists():
        shutil.copy2(src, dest)
        print(f"  ✅ Copied: {src} → {dest}")
    else:
        print(f"  Already exists: {dest}")
    return dest


def get_video_duration(video_path: Path) -> float:
    """Get video duration in seconds using ffprobe."""
    cmd = [
        "ffprobe", "-v", "quiet",
        "-print_format", "json",
        "-show_format",
        str(video_path)
    ]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode == 0:
        data = json.loads(result.stdout)
        return float(data.get("format", {}).get("duration", 0))
    return 0.0


def main():
    parser = argparse.ArgumentParser(description="Tier 3 Download Worker")
    parser.add_argument("--session", required=True, help="Session ID (e.g. session_001)")
    parser.add_argument("--url", help="Override URL for this session")
    parser.add_argument("--local", help="Local file path for this session")
    args = parser.parse_args()

    manifest = load_manifest()

    # Find this session
    session = next((s for s in manifest["sessions"] if s["id"] == args.session), None)
    if not session:
        print(f"Session not found: {args.session}")
        return

    print(f"\n[DOWNLOAD] {args.session}: {session['title'][:60]}")

    # Update status to downloading
    update_session(manifest, args.session, {"status": "downloading"})
    manifest = load_manifest()  # reload after update

    try:
        url = args.url or session.get("url")
        local = args.local or session.get("local_path")

        if local:
            output_path = copy_local_file(args.session, local)
        elif url:
            output_path = download_from_teachable(args.session, url)
        else:
            print(f"  ⚠️  No URL or local path set for {args.session}")
            print(f"  Set the URL in archive/archive_manifest.json or pass --url")
            update_session(manifest, args.session, {"status": "pending"})
            return

        # Get duration
        duration = get_video_duration(output_path)
        duration_str = f"{int(duration//3600)}h {int((duration%3600)//60)}m"

        update_session(manifest, args.session, {
            "status": "transcribing",
            "raw_video_path": str(output_path),
            "duration_seconds": duration,
            "duration_str": duration_str,
        })

        print(f"  Duration: {duration_str}")
        print(f"  Status updated: downloading → transcribing")
        print(f"  ✅ Ready for Tier 3 transcribe worker")

    except Exception as e:
        print(f"  ❌ Download failed: {e}")
        update_session(manifest, args.session, {
            "status": "failed",
            "error": str(e),
        })


if __name__ == "__main__":
    main()
