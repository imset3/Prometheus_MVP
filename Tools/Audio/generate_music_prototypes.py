#!/usr/bin/env python3
"""Generate original, loop-oriented Prometheus tutorial music prototypes.

The renderer intentionally uses only NumPy and the Python standard library so
the source material remains reproducible inside the project. FFmpeg converts
temporary 48 kHz WAV masters to Unity-ready Ogg Vorbis assets.
"""

from __future__ import annotations

import math
import wave
from pathlib import Path

import numpy as np


SAMPLE_RATE = 48_000
ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Project/Audio/Music/Tutorial/Prototypes"
RNG = np.random.default_rng(20260808)


def midi(note: float) -> float:
    return 440.0 * (2.0 ** ((note - 69.0) / 12.0))


def pan_gains(pan: float) -> tuple[float, float]:
    angle = (np.clip(pan, -1.0, 1.0) + 1.0) * math.pi / 4.0
    return math.cos(angle), math.sin(angle)


def envelope(count: int, attack: float, release: float, decay: float = 0.0) -> np.ndarray:
    env = np.ones(count, dtype=np.float32)
    attack_count = min(count, max(1, int(attack * SAMPLE_RATE)))
    release_count = min(count, max(1, int(release * SAMPLE_RATE)))
    env[:attack_count] *= np.linspace(0.0, 1.0, attack_count, dtype=np.float32)
    env[-release_count:] *= np.linspace(1.0, 0.0, release_count, dtype=np.float32)
    if decay > 0.0:
        env *= np.exp(-np.linspace(0.0, decay, count, dtype=np.float32))
    return env


class Track:
    def __init__(self, seconds: float):
        self.seconds = seconds
        self.count = int(round(seconds * SAMPLE_RATE))
        self.data = np.zeros((self.count, 2), dtype=np.float32)

    def add(self, start: float, signal: np.ndarray, gain: float = 1.0, pan: float = 0.0) -> None:
        if signal.size == 0:
            return
        left, right = pan_gains(pan)
        start_sample = int(round(start * SAMPLE_RATE)) % self.count
        signal = signal.astype(np.float32, copy=False) * gain
        remaining = signal.size
        source = 0
        destination = start_sample
        while remaining:
            block = min(remaining, self.count - destination)
            self.data[destination:destination + block, 0] += signal[source:source + block] * left
            self.data[destination:destination + block, 1] += signal[source:source + block] * right
            source += block
            remaining -= block
            destination = 0

    def note(
        self,
        start: float,
        duration: float,
        note: float,
        gain: float,
        instrument: str,
        pan: float = 0.0,
    ) -> None:
        count = max(2, int(round(duration * SAMPLE_RATE)))
        t = np.arange(count, dtype=np.float32) / SAMPLE_RATE
        frequency = midi(note)
        phase = 2.0 * math.pi * frequency * t

        if instrument == "bell":
            signal = (
                np.sin(phase)
                + 0.42 * np.sin(2.01 * phase + 0.2)
                + 0.22 * np.sin(3.98 * phase + 0.7)
                + 0.09 * np.sin(6.12 * phase)
            ) * envelope(count, 0.006, min(0.2, duration * 0.25), decay=4.2)
        elif instrument == "pluck":
            signal = sum(np.sin(phase * harmonic) / (harmonic ** 1.25) for harmonic in range(1, 8))
            signal *= envelope(count, 0.004, min(0.12, duration * 0.2), decay=3.6)
        elif instrument == "string":
            signal = sum(np.sin(phase * harmonic) / harmonic for harmonic in range(1, 7))
            signal += 0.25 * np.sin(phase * 1.004)
            signal *= envelope(count, min(0.35, duration * 0.2), min(0.45, duration * 0.25))
        elif instrument == "glass":
            signal = (
                np.sin(phase)
                + 0.28 * np.sin(2.7 * phase)
                + 0.14 * np.sin(4.15 * phase)
            ) * envelope(count, 0.04, min(0.5, duration * 0.3), decay=1.2)
        elif instrument == "bass":
            signal = np.sin(phase) + 0.3 * np.sin(2.0 * phase) + 0.12 * np.sin(3.0 * phase)
            signal *= envelope(count, 0.012, min(0.16, duration * 0.2), decay=0.5)
        elif instrument == "brass":
            signal = sum(np.sin(phase * harmonic) / (harmonic ** 0.8) for harmonic in range(1, 6))
            signal *= envelope(count, min(0.08, duration * 0.15), min(0.18, duration * 0.25))
        else:
            signal = np.sin(phase) * envelope(count, 0.01, 0.05)

        self.add(start, signal, gain, pan)

    def kick(self, start: float, gain: float = 1.0) -> None:
        duration = 0.32
        count = int(duration * SAMPLE_RATE)
        t = np.arange(count, dtype=np.float32) / SAMPLE_RATE
        phase = 2.0 * math.pi * (92.0 * t - 36.0 * t * t)
        signal = np.sin(phase) * np.exp(-t * 14.0)
        self.add(start, signal, gain, 0.0)

    def snare(self, start: float, gain: float = 1.0) -> None:
        duration = 0.22
        count = int(duration * SAMPLE_RATE)
        t = np.arange(count, dtype=np.float32) / SAMPLE_RATE
        noise = RNG.standard_normal(count).astype(np.float32)
        noise[1:] -= noise[:-1] * 0.72
        body = np.sin(2.0 * math.pi * 178.0 * t)
        signal = (noise * 0.72 + body * 0.28) * np.exp(-t * 19.0)
        self.add(start, signal, gain, 0.08)

    def hat(self, start: float, gain: float = 1.0, open_hat: bool = False) -> None:
        duration = 0.16 if open_hat else 0.055
        count = int(duration * SAMPLE_RATE)
        t = np.arange(count, dtype=np.float32) / SAMPLE_RATE
        noise = RNG.standard_normal(count).astype(np.float32)
        noise[1:] -= noise[:-1] * 0.92
        signal = noise * np.exp(-t * (20.0 if open_hat else 70.0))
        self.add(start, signal, gain, 0.3)

    def tick(self, start: float, gain: float = 1.0, pan: float = 0.0) -> None:
        duration = 0.045
        count = int(duration * SAMPLE_RATE)
        t = np.arange(count, dtype=np.float32) / SAMPLE_RATE
        signal = (
            np.sin(2.0 * math.pi * 1700.0 * t)
            + 0.35 * np.sin(2.0 * math.pi * 2450.0 * t)
        ) * np.exp(-t * 95.0)
        self.add(start, signal, gain, pan)

    def room(self, wet: float = 0.12) -> None:
        dry = self.data.copy()
        for delay_seconds, gain, cross in ((0.113, 0.55, False), (0.173, 0.36, True), (0.257, 0.22, False)):
            delay = int(delay_seconds * SAMPLE_RATE)
            echo = np.roll(dry, delay, axis=0)
            if cross:
                echo = echo[:, ::-1]
            self.data += echo * wet * gain

    def master(self, target_rms_db: float = -18.0, peak_ceiling: float = 0.88) -> np.ndarray:
        self.data -= np.mean(self.data, axis=0, keepdims=True)
        self.data = np.tanh(self.data * 1.08)
        # A very short zero crossing at the loop boundary prevents clicks while
        # remaining below the duration of a perceptible musical pause.
        fade_count = int(0.012 * SAMPLE_RATE)
        fade = np.linspace(0.0, 1.0, fade_count, dtype=np.float32)
        self.data[:fade_count] *= fade[:, None]
        self.data[-fade_count:] *= fade[::-1, None]
        rms = float(np.sqrt(np.mean(self.data * self.data)))
        target_rms = 10.0 ** (target_rms_db / 20.0)
        if rms > 0.0:
            self.data *= target_rms / rms
        peak = float(np.max(np.abs(self.data)))
        if peak > peak_ceiling:
            self.data *= peak_ceiling / peak
        return self.data


def add_chord(track: Track, start: float, duration: float, notes: list[int], gain: float, instrument: str) -> None:
    spread = np.linspace(-0.65, 0.65, len(notes))
    for note, pan in zip(notes, spread):
        track.note(start, duration, note, gain / max(1.0, len(notes) * 0.68), instrument, float(pan))


def render_adamas() -> Track:
    bpm = 84.0
    bar = 120.0 / bpm  # two dotted-quarter pulses in 6/8
    bars = 40
    track = Track(bar * bars)
    chords = [
        ([50, 57, 61, 64], 38),  # Dmaj9
        ([49, 57, 61, 64], 37),  # A/C#
        ([47, 54, 57, 62], 35),  # Bm7
        ([43, 50, 54, 59], 31),  # Gmaj7
        ([40, 47, 52, 55], 28),  # Em7
        ([42, 50, 54, 57], 30),  # D/F#
        ([43, 50, 54, 57], 31),
        ([45, 52, 57, 61], 33),
    ]
    motif = [74, 76, 81, 78, 76, 74]
    arpeggio_steps = [0, 2, 1, 3, 1, 2]

    for bar_index in range(bars):
        chord, bass_note = chords[bar_index % len(chords)]
        start = bar_index * bar
        add_chord(track, start, bar * 0.97, chord, 0.15, "string")
        track.note(start, bar * 0.84, bass_note, 0.13, "bass", -0.12)
        for step, chord_index in enumerate(arpeggio_steps):
            note = chord[chord_index] + 12
            track.note(start + step * bar / 6.0, bar / 5.8, note, 0.105, "pluck", -0.45 + step * 0.18)
            if step in (0, 3):
                track.tick(start + step * bar / 6.0, 0.025, 0.55 if step else -0.55)

        if bar_index % 8 in (0, 1):
            for step, note in enumerate(motif):
                track.note(start + step * bar / 6.0, bar / 4.2, note, 0.12, "bell", 0.18)
        elif bar_index % 8 == 6:
            for step, note in enumerate([71, 74, 76]):
                track.note(start + step * bar / 3.0, bar / 2.8, note, 0.08, "glass", 0.35)

    track.room(0.18)
    return track


def render_title_theme() -> Track:
    """A spacious, hopeful title loop with restrained mechanical motion."""
    bpm = 72.0
    bar = 240.0 / bpm
    bars = 20
    track = Track(bar * bars)
    progression = [
        ([50, 57, 61, 66], 38),  # Dmaj add6
        ([47, 54, 57, 62], 35),  # Bm7
        ([43, 50, 54, 59], 31),  # Gmaj7
        ([45, 52, 57, 61], 33),  # A add9
        ([40, 47, 52, 59], 28),  # Em7
        ([43, 50, 54, 57], 31),  # G6
        ([45, 52, 57, 61], 33),  # A add9
        ([50, 57, 61, 64], 38),  # Dmaj9
    ]
    title_motif = [74, 78, 81, 83, 81, 78, 76, 74]

    for bar_index in range(bars):
        chord, bass_note = progression[bar_index % len(progression)]
        start = bar_index * bar
        add_chord(track, start, bar * 0.995, chord, 0.125, "string")
        track.note(start, bar * 0.9, bass_note, 0.10, "bass", -0.16)
        track.note(start + bar * 0.5, bar * 0.44, bass_note + 7, 0.055, "bass", 0.1)

        # Quiet clockwork pulse: present enough to give the static menu life,
        # but deliberately softer than gameplay percussion.
        for step, chord_index in enumerate((0, 2, 1, 3, 1, 2, 0, 3)):
            offset = start + step * bar / 8.0
            track.note(offset, bar / 7.2, chord[chord_index] + 12, 0.052,
                       "pluck", -0.48 + step * 0.14)
            if step in (0, 4):
                track.tick(offset, 0.014, -0.5 if step == 0 else 0.5)

        if bar_index % 8 in (0, 1):
            for step, note in enumerate(title_motif):
                track.note(start + step * bar / 8.0, bar / 4.6, note, 0.082,
                           "glass", 0.18)
        elif bar_index % 8 == 6:
            for step, note in enumerate((69, 74, 78, 81)):
                track.note(start + step * bar / 4.0, bar / 3.6, note, 0.065,
                           "bell", 0.28)

    track.room(0.22)
    return track


def render_outer_combat() -> tuple[Track, Track, Track]:
    bpm = 124.0
    beat = 60.0 / bpm
    bar = beat * 4.0
    bars = 32
    base = Track(bar * bars)
    intensity = Track(bar * bars)
    progression = [
        ([47, 54, 59, 62], 35),  # Bm
        ([43, 50, 55, 59], 31),  # G
        ([38, 45, 50, 54], 26),  # D
        ([45, 52, 57, 61], 33),  # A
    ]
    motif = [59, 61, 66, 62]

    for bar_index in range(bars):
        chord, bass_note = progression[bar_index % 4]
        start = bar_index * bar
        add_chord(base, start, bar * 0.98, chord, 0.10, "string")
        for eighth in range(8):
            offset = start + eighth * beat / 2.0
            root = chord[0] + (12 if eighth in (3, 7) else 0)
            base.note(offset, beat * 0.42, root, 0.095, "pluck", -0.25 if eighth % 2 == 0 else 0.25)
            base.hat(offset, 0.028 if eighth % 2 else 0.045)
        for quarter in range(4):
            offset = start + quarter * beat
            base.note(offset, beat * 0.72, bass_note, 0.17, "bass", -0.1)
            if quarter in (0, 2):
                base.kick(offset, 0.20 if quarter == 0 else 0.15)
            else:
                base.snare(offset, 0.10)

        if bar_index % 4 == 0:
            for step, note in enumerate(motif):
                base.note(start + step * beat, beat * 0.78, note + 12, 0.09, "bell", 0.25)

        for eighth in range(8):
            offset = start + eighth * beat / 2.0
            intensity.hat(offset, 0.06 if eighth % 2 == 0 else 0.04, open_hat=eighth == 7)
            if eighth in (0, 3, 4, 6):
                intensity.note(offset, beat * 0.38, chord[(eighth // 2) % len(chord)] + 24, 0.055, "pluck", 0.55)
        intensity.kick(start + beat * 3.5, 0.12)
        if bar_index % 4 in (1, 3):
            intensity.note(start, bar * 0.92, motif[bar_index % 4] + 12, 0.11, "brass", 0.15)

    base.room(0.07)
    intensity.room(0.06)
    preview = Track(base.seconds)
    preview.data[:] = base.data
    for bar_index in range(bars):
        amount = 0.0 if bar_index < 8 else min(1.0, (bar_index - 7) / 8.0)
        start = int(bar_index * bar * SAMPLE_RATE)
        end = int((bar_index + 1) * bar * SAMPLE_RATE)
        preview.data[start:end] += intensity.data[start:end] * amount
    return base, intensity, preview


def render_helte() -> tuple[Track, Track, Track, Track]:
    bpm = 128.0
    beat = 60.0 / bpm
    bar = beat * 4.0
    bars = 32
    base = Track(bar * bars)
    phase_two = Track(bar * bars)
    final = Track(bar * bars)
    progression = [
        ([45, 52, 57, 60], 33),  # Am
        ([41, 48, 53, 57], 29),  # F
        ([43, 50, 55, 59], 31),  # G
        ([40, 47, 52, 56], 28),  # E tension
    ]
    helte_motif = [69, 68, 72, 71, 69, 76]

    for bar_index in range(bars):
        chord, bass_note = progression[bar_index % 4]
        start = bar_index * bar
        add_chord(base, start, bar * 0.97, chord, 0.11, "string")
        for eighth in range(8):
            offset = start + eighth * beat / 2.0
            note = chord[0] + (12 if eighth in (2, 5) else 0)
            base.note(offset, beat * 0.39, note, 0.105, "pluck", -0.35 if eighth % 2 == 0 else 0.1)
            base.hat(offset, 0.025)
        for quarter in range(4):
            offset = start + quarter * beat
            base.note(offset, beat * 0.68, bass_note, 0.18, "bass", -0.1)
            base.kick(offset, 0.18 if quarter in (0, 2) else 0.10)
            if quarter in (1, 3):
                base.snare(offset, 0.085)
        if bar_index % 8 in (0, 1):
            for step, note in enumerate(helte_motif):
                base.note(start + step * beat / 2.0, beat * 0.46, note, 0.095, "glass", 0.28)

        # Phase two: summoned blades and a sharper counter-line.
        for sixteenth in range(16):
            offset = start + sixteenth * beat / 4.0
            phase_two.hat(offset, 0.025 if sixteenth % 2 else 0.04)
            if sixteenth in (0, 3, 6, 8, 11, 14):
                note = chord[(sixteenth // 3) % len(chord)] + 24
                phase_two.note(offset, beat * 0.22, note, 0.045, "bell", 0.52)
        if bar_index % 4 == 2:
            for step, note in enumerate([76, 74, 72, 71]):
                phase_two.note(start + step * beat, beat * 0.72, note, 0.075, "brass", 0.22)

        # Final rush: double-time pulse without changing the BPM.
        for sixteenth in range(16):
            offset = start + sixteenth * beat / 4.0
            final.hat(offset, 0.045 if sixteenth % 2 == 0 else 0.027)
            if sixteenth in (0, 4, 7, 8, 12, 15):
                final.kick(offset, 0.09)
        final.snare(start + beat, 0.075)
        final.snare(start + beat * 3.0, 0.09)
        add_chord(final, start, bar * 0.82, [note + 12 for note in chord], 0.075, "brass")
        if bar_index % 2 == 1:
            final.note(start, bar * 0.88, helte_motif[bar_index % len(helte_motif)] - 12, 0.09, "brass", -0.3)

    base.room(0.07)
    phase_two.room(0.055)
    final.room(0.045)
    preview = Track(base.seconds)
    for bar_index in range(bars):
        start = int(bar_index * bar * SAMPLE_RATE)
        end = int((bar_index + 1) * bar * SAMPLE_RATE)
        preview.data[start:end] = base.data[start:end]
        if bar_index >= 8:
            preview.data[start:end] += phase_two.data[start:end]
        if bar_index >= 20:
            preview.data[start:end] += final.data[start:end]
    return base, phase_two, final, preview


def write_wav(name: str, track: Track, target_rms_db: float = -18.0) -> Path:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    audio = track.master(target_rms_db=target_rms_db)
    pcm = np.clip(audio * 32767.0, -32768, 32767).astype("<i2")
    destination = OUTPUT / f"{name}.wav"
    with wave.open(str(destination), "wb") as wav_file:
        wav_file.setnchannels(2)
        wav_file.setsampwidth(2)
        wav_file.setframerate(SAMPLE_RATE)
        wav_file.writeframes(pcm.tobytes())
    print(f"wrote {destination.relative_to(ROOT)} ({track.seconds:.2f}s)")
    return destination


def main() -> None:
    title = render_title_theme()
    write_wav("MUS_TITLE_Prometheus_Prototype_Loop", title, -20.0)
    del title

    adamas = render_adamas()
    write_wav("MUS_TUTO_Adamas_Prototype_Loop", adamas)
    del adamas

    combat_base, combat_intensity, combat_preview = render_outer_combat()
    write_wav("MUS_TUTO_OuterCombat_Base_Prototype_Loop", combat_base, -19.0)
    write_wav("MUS_TUTO_OuterCombat_Intensity_Prototype_Loop", combat_intensity, -25.0)
    write_wav("MUS_TUTO_OuterCombat_Prototype_Preview", combat_preview)
    del combat_base, combat_intensity, combat_preview

    helte_base, helte_phase_two, helte_final, helte_preview = render_helte()
    write_wav("MUS_BOSS_Helte_Base_Prototype_Loop", helte_base, -19.0)
    write_wav("MUS_BOSS_Helte_Phase2_Prototype_Loop", helte_phase_two, -26.0)
    write_wav("MUS_BOSS_Helte_Final_Prototype_Loop", helte_final, -24.0)
    write_wav("MUS_BOSS_Helte_Prototype_Preview", helte_preview)


if __name__ == "__main__":
    main()
