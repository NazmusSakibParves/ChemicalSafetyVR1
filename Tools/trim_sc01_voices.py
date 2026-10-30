"""Trim the silence and noise before and after the Scenario 1 trial voices.

Reads C:\\Users\\msi\\Downloads\\Scene-1-Voices and writes
Assets/Art/Audio/VO/Sc01Trial/. The current clips in Assets/Art/Audio/VO/
are never opened for writing.

A quiet consonant at the start or end is kept: the cut sits 300 ms before
the first speech window and 400 ms after the last one, and a cut is
rejected when the removed audio is not near silence.
"""
import shutil
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np

SRC = Path(r"C:\Users\msi\Downloads\Scene-1-Voices")
DST = Path(__file__).resolve().parents[1] / "Assets" / "Art" / "Audio" / "VO" / "Sc01Trial"
STEMS = [
    "VO_AssalamuAlaikum_bn",
    "VO_ContinueHint_bn",
    "VO_Correct_bn",
    "VO_Wrong_bn",
    "VO_SC01_Instruction_bn",
    "VO_PickLabel_bn",
    "VO_WrongLabel_bn",
    "VO_Carrier_bn",
    "VO_Sc01Complete_bn",
]
LEAD_S = 0.300
TAIL_S = 0.400
MIN_S = 0.4


def ffmpeg_bin():
    found = shutil.which("ffmpeg")
    if found:
        return found
    winget = Path.home() / "AppData" / "Local" / "Microsoft" / "WinGet"
    for path in winget.glob("Packages/**/ffmpeg.exe"):
        return str(path)
    link = winget / "Links" / "ffmpeg.exe"
    if link.exists():
        return str(link)
    sys.exit("ffmpeg was not found on PATH")


def find_src(stem):
    for ext in (".mp3", ".m4a", ".wav", ".flac"):
        path = SRC / (stem + ext)
        if path.exists():
            return path
    sys.exit("missing source for " + stem)


def decode(ffmpeg, src, dest):
    subprocess.run(
        [ffmpeg, "-y", "-i", str(src), "-ac", "1", "-ar", "44100", "-sample_fmt", "s16", str(dest)],
        check=True,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
    )


def read_wav(path):
    with wave.open(str(path), "rb") as handle:
        if handle.getsampwidth() != 2:
            sys.exit("expected 16-bit wav: " + str(path))
        frames = handle.readframes(handle.getnframes())
        rate = handle.getframerate()
        channels = handle.getnchannels()
    samples = np.frombuffer(frames, dtype=np.int16).astype(np.float32) / 32768.0
    if channels > 1:
        samples = samples.reshape(-1, channels).mean(axis=1)
    return rate, samples


def write_wav(path, rate, samples):
    pcm = np.clip(samples, -1.0, 1.0)
    pcm = (pcm * 32767.0).astype(np.int16)
    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(rate)
        handle.writeframes(pcm.tobytes())


def window_rms(samples, frame, hop):
    count = 1 + max(0, (len(samples) - frame) // hop)
    rms = np.empty(count, dtype=np.float64)
    for index in range(count):
        start = index * hop
        piece = samples[start:start + frame]
        rms[index] = np.sqrt(np.mean(piece * piece) + 1e-12)
    return rms


def region_has_speech(rms, hop, frame, thr, start, end):
    for index, level in enumerate(rms):
        centre = index * hop + frame // 2
        if start <= centre < end and level > thr:
            return True
    return False


def fade_edges(samples, rate):
    fade = min(int(0.015 * rate), max(0, len(samples) // 8))
    if fade < 2:
        return samples
    out = samples.copy()
    ramp = np.linspace(0.0, 1.0, fade, dtype=np.float32)
    out[:fade] *= ramp
    out[-fade:] *= ramp[::-1]
    return out


def trim(samples, rate):
    frame = int(0.020 * rate)
    hop = frame // 2
    if len(samples) < frame * 2:
        return samples, "too short, kept full"
    rms = window_rms(samples, frame, hop)
    floor = float(np.percentile(rms, 10))
    thr = max(floor * 2.5, 1e-4)
    speech = np.flatnonzero(rms > thr)
    if speech.size == 0:
        return samples, "no speech found, kept full"
    first = int(speech[0] * hop)
    last = int(speech[-1] * hop + frame)
    start = max(0, first - int(LEAD_S * rate))
    end = min(len(samples), last + int(TAIL_S * rate))
    if (end - start) / rate < MIN_S:
        return samples, "under 0.4s after the cut, kept full"
    if region_has_speech(rms, hop, frame, thr, 0, start) or region_has_speech(rms, hop, frame, thr, end, len(samples)):
        return samples, "the cut would remove speech, kept full"
    return fade_edges(samples[start:end], rate), "trimmed"


def main():
    ffmpeg = ffmpeg_bin()
    DST.mkdir(parents=True, exist_ok=True)
    scratch = DST / "_decode.wav"
    for stem in STEMS:
        src = find_src(stem)
        decode(ffmpeg, src, scratch)
        rate, samples = read_wav(scratch)
        kept, note = trim(samples, rate)
        dest = DST / (stem + ".wav")
        write_wav(dest, rate, kept)
        print(
            "%s  %.2fs -> %.2fs  %s"
            % (stem, len(samples) / rate, len(kept) / rate, note)
        )
    scratch.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
