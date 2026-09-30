"""
ARCHIVE PIPELINE — TIER 3: TRANSCRIBE WORKER
=============================================
Operated by: Bob accounts 31-45

Uses OpenAI Whisper to transcribe video audio.
Outputs JSON with word-level timestamps for clip analysis.

Usage:
    python scripts/archive_agent/04_transcribe_worker.py --session session_001
    python scripts/archive_agent/04_transcribe_worker.py --session session_001 --model large
"""

import json
import argparse
import subprocess
from pathlib import Path
from datetime import datetime

MANIFEST_PATH = Path("archive/archive_manifest.json")
RAW_DIR = Path("archive/raw")
TRANSCRIPTS_DIR = Path("archive/transcripts")
TRANSCRIPTS_DIR.mkdir(parents=True, exist_ok=True)

# Whisper model options: tiny, base, small, medium, large
# Recommendation: "base" for speed, "medium" for quality, "large" for best accuracy
DEFAULT_MODEL = "base"


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


def extract_audio(video_path: Path, audio_path: Path):
    """Extract audio from video using FFmpeg (faster than processing full video)."""
    if audio_path.exists():
        print(f"  Audio already extracted: {audio_path}")
        return
    cmd = [
        "ffmpeg", "-i", str(video_path),
        "-vn",                    # no video
        "-acodec", "pcm_s16le",   # WAV format for Whisper
        "-ar", "16000",           # 16kHz sample rate (Whisper optimal)
        "-ac", "1",               # mono
        "-y",                     # overwrite
        str(audio_path)
    ]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"FFmpeg audio extraction failed:\n{result.stderr}")
    print(f"  ✅ Audio extracted: {audio_path}")


def transcribe_with_whisper(audio_path: Path, model: str) -> dict:
    """Run Whisper transcription. Returns full transcript with word timestamps."""
    import whisper

    print(f"  Loading Whisper model: {model}")
    wmodel = whisper.load_model(model)

    print(f"  Transcribing: {audio_path}")
    result = wmodel.transcribe(
        str(audio_path),
        word_timestamps=True,   # critical for clip boundary detection
        verbose=False,
    )
    print(f"  ✅ Transcription complete — {len(result['segments'])} segments")
    return result


def format_transcript(whisper_result: dict, session_id: str, session_title: str) -> dict:
    """Format Whisper output into our standard transcript JSON."""
    segments = []
    full_text_parts = []

    for seg in whisper_result.get("segments", []):
        words = []
        if "words" in seg:
            for w in seg["words"]:
                words.append({
                    "word": w["word"].strip(),
                    "start": round(w["start"], 3),
                    "end": round(w["end"], 3),
                })

        segment = {
            "id": seg["id"],
            "start": round(seg["start"], 3),
            "end": round(seg["end"], 3),
            "text": seg["text"].strip(),
            "words": words,
        }
        segments.append(segment)
        full_text_parts.append(seg["text"].strip())

    return {
        "session_id": session_id,
        "session_title": session_title,
        "transcribed_at": datetime.utcnow().isoformat(),
        "model": whisper_result.get("_model", "whisper"),
        "language": whisper_result.get("language", "en"),
        "duration_seconds": segments[-1]["end"] if segments else 0,
        "full_text": " ".join(full_text_parts),
        "segments": segments,
    }


def main():
    parser = argparse.ArgumentParser(description="Tier 3 Transcribe Worker")
    parser.add_argument("--session", required=True, help="Session ID (e.g. session_001)")
    parser.add_argument("--model", default=DEFAULT_MODEL,
                        choices=["tiny", "base", "small", "medium", "large"],
                        help="Whisper model size (default: base)")
    args = parser.parse_args()

    manifest = load_manifest()
    session = next((s for s in manifest["sessions"] if s["id"] == args.session), None)
    if not session:
        print(f"Session not found: {args.session}")
        return

    print(f"\n[TRANSCRIBE] {args.session}: {session['title'][:60]}")
    print(f"  Model: {args.model}")

    # Find video file
    video_path = Path(session.get("raw_video_path", RAW_DIR / f"{args.session}.mp4"))
    if not video_path.exists():
        print(f"  ❌ Video not found: {video_path}")
        print(f"  Run download worker first.")
        return

    transcript_path = TRANSCRIPTS_DIR / f"{args.session}.json"

    if transcript_path.exists():
        print(f"  Already transcribed: {transcript_path}")
        update_session(manifest, args.session, {
            "status": "analyzing",
            "transcript_path": str(transcript_path),
        })
        return

    try:
        update_session(manifest, args.session, {"status": "transcribing"})
        manifest = load_manifest()

        # Extract audio first (faster for Whisper)
        audio_path = TRANSCRIPTS_DIR / f"{args.session}_audio.wav"
        extract_audio(video_path, audio_path)

        # Transcribe
        result = transcribe_with_whisper(audio_path, args.model)
        transcript = format_transcript(result, args.session, session["title"])

        # Save transcript
        with open(transcript_path, "w", encoding="utf-8") as f:
            json.dump(transcript, f, indent=2, ensure_ascii=False)

        # Clean up temp audio
        audio_path.unlink(missing_ok=True)

        update_session(manifest, args.session, {
            "status": "analyzing",
            "transcript_path": str(transcript_path),
        })

        word_count = sum(len(s["words"]) for s in transcript["segments"])
        print(f"  Words transcribed: {word_count:,}")
        print(f"  Saved: {transcript_path}")
        print(f"  Status updated: transcribing → analyzing")
        print(f"  ✅ Ready for Tier 3 analyze worker")

    except Exception as e:
        print(f"  ❌ Transcription failed: {e}")
        update_session(manifest, args.session, {
            "status": "failed",
            "error": str(e),
        })


if __name__ == "__main__":
    main()
