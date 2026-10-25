#!/usr/bin/env python3
"""Render Listening and Speaking recordings with distinct neural voices (build time only).

Reads Tools/content2026/audio_manifest.json ({key: [{voice, text, pause}]}) produced by
build_toefl_2026_bank.py. Each segment is spoken by its Kokoro-82M (Apache-2.0) voice:
af_/am_ voices are North American English, bf_/bm_ voices British English. Segments are
joined with natural pauses, encoded as mono AAC with macOS afconvert, and packed into
TOEFL Simulation/Resources/ExamAudio.pack (+ ExamAudio-2.pack, …: volumes under 45 MB) and ExamAudio.json.

    PYTORCH_ENABLE_MPS_FALLBACK=1 python Tools/generate_exam_audio.py --cache tmp/exam-audio-cache --device mps   # render
    python Tools/generate_exam_audio.py --cache tmp/exam-audio-cache --pack-only            # pack
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "Tools" / "content2026" / "audio_manifest.json"
RESOURCES = ROOT / "TOEFL Simulation" / "Resources"
SAMPLE_RATE = 24_000
BITRATE = 32_000
SPEED = 0.9  # natural exam pace, about 150–170 words per minute


def clip_name(key: str, segments: list[dict]) -> str:
    digest = hashlib.sha1((json.dumps(segments, sort_keys=True) + f"|{SPEED}").encode()).hexdigest()[:12]
    return f"{key}-{digest}.m4a"


def render(manifest: dict, cache: Path, shard: int, shards: int, device: str | None) -> None:
    import numpy as np
    import soundfile as sf
    import torch
    from kokoro import KPipeline

    torch.set_num_threads(3)
    pipelines = {"a": KPipeline(lang_code="a", repo_id="hexgrad/Kokoro-82M", device=device),
                 "b": KPipeline(lang_code="b", repo_id="hexgrad/Kokoro-82M", device=device)}
    cache.mkdir(parents=True, exist_ok=True)
    keys = sorted(manifest)[shard::shards]
    pending = [k for k in keys if not (cache / clip_name(k, manifest[k])).exists()]
    print(f"shard {shard}: {len(keys) - len(pending)} cached, {len(pending)} to render", flush=True)

    def trim(audio, threshold=0.006):
        loud = np.flatnonzero(np.abs(audio) > threshold)
        if loud.size == 0:
            return audio
        pad = int(0.04 * SAMPLE_RATE)
        return audio[max(0, loud[0] - pad): min(len(audio), loud[-1] + pad)]

    with tempfile.TemporaryDirectory() as scratch:
        wav = Path(scratch) / f"clip-{shard}.wav"
        for count, key in enumerate(pending, 1):
            pieces = [np.zeros(int(0.15 * SAMPLE_RATE), dtype=np.float32)]
            for segment in manifest[key]:
                voice = segment["voice"]
                pipeline = pipelines["b" if voice.startswith("b") else "a"]
                chunks = [audio.numpy() for _, _, audio in pipeline(segment["text"], voice=voice, speed=segment.get("speed", SPEED))]
                if chunks:
                    pieces.append(trim(np.concatenate(chunks)).astype(np.float32))
                pieces.append(np.zeros(int(segment.get("pause", 0.35) * SAMPLE_RATE), dtype=np.float32))
            sf.write(wav, np.concatenate(pieces), SAMPLE_RATE)
            subprocess.run(["afconvert", "-f", "m4af", "-d", "aac", "-b", str(BITRATE), "-c", "1",
                            str(wav), str(cache / clip_name(key, manifest[key]))], check=True)
            if count % 25 == 0:
                print(f"shard {shard}: rendered {count}/{len(pending)}", flush=True)


VOLUME_BYTES = 45 * 1_048_576  # keeps every pack file under GitHub's 50 MB warning size


def pack(manifest: dict, cache: Path) -> None:
    """Write ExamAudio.pack (+ ExamAudio-2.pack, …) and an index of [offset, length, volume]."""
    clips: dict[str, list[int]] = {}
    volumes = [bytearray()]
    missing = []
    for key in sorted(manifest):
        path = cache / clip_name(key, manifest[key])
        if not path.exists():
            missing.append(key)
            continue
        data = path.read_bytes()
        if volumes[-1] and len(volumes[-1]) + len(data) > VOLUME_BYTES:
            volumes.append(bytearray())
        blob = volumes[-1]
        clips[key] = [len(blob), len(data)] + ([len(volumes) - 1] if len(volumes) > 1 else [])
        blob.extend(data)
    for old in RESOURCES.glob("ExamAudio-*.pack"):
        old.unlink()
    for number, blob in enumerate(volumes, 1):
        name = "ExamAudio.pack" if number == 1 else f"ExamAudio-{number}.pack"
        (RESOURCES / name).write_bytes(bytes(blob))
    index = {"engine": "Kokoro-82M", "license": "Apache-2.0", "format": "m4a", "clips": clips}
    (RESOURCES / "ExamAudio.json").write_text(json.dumps(index, separators=(",", ":"), sort_keys=True) + "\n", encoding="utf-8")
    total = sum(len(blob) for blob in volumes)
    print(f"packed {len(clips)} recordings in {len(volumes)} volume(s), {total / 1_048_576:.1f} MB; missing {len(missing)}", flush=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--shard", type=int, default=0)
    parser.add_argument("--shards", type=int, default=1)
    parser.add_argument("--pack-only", action="store_true")
    parser.add_argument("--device", help="e.g. mps for Apple GPU (set PYTORCH_ENABLE_MPS_FALLBACK=1)")
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if args.pack_only:
        pack(manifest, args.cache)
    else:
        render(manifest, args.cache, args.shard, args.shards, args.device)


if __name__ == "__main__":
    main()
