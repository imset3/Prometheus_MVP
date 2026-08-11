#!/usr/bin/env python3
"""Deterministically render original tutorial SFX prototypes.

The sounds are synthesized from oscillators and seeded noise. No samples or
third-party recordings are used.
"""

from __future__ import annotations

import math
import wave
from pathlib import Path

import numpy as np


SAMPLE_RATE = 48_000
ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Project/Audio/Sfx/Tutorial/Prototypes"
RNG = np.random.default_rng(20260808)


def timeline(seconds: float) -> np.ndarray:
    return np.arange(max(1, round(seconds * SAMPLE_RATE)), dtype=np.float64) / SAMPLE_RATE


def envelope(length: int, attack: float = 0.01, release: float = 0.15) -> np.ndarray:
    attack_samples = max(1, round(attack * SAMPLE_RATE))
    release_samples = max(1, round(release * SAMPLE_RATE))
    result = np.ones(length, dtype=np.float64)
    result[: min(length, attack_samples)] = np.linspace(0.0, 1.0, min(length, attack_samples))
    if release_samples < length:
        result[-release_samples:] *= np.linspace(1.0, 0.0, release_samples)
    else:
        result *= np.linspace(1.0, 0.0, length)
    return result


def oscillator(seconds: float, start_hz: float, end_hz: float | None = None,
               phase: float = 0.0) -> np.ndarray:
    t = timeline(seconds)
    end = start_hz if end_hz is None else end_hz
    frequency = np.linspace(start_hz, end, len(t))
    angle = phase + 2.0 * np.pi * np.cumsum(frequency) / SAMPLE_RATE
    return np.sin(angle)


def noise(seconds: float, smooth: int = 1) -> np.ndarray:
    values = RNG.uniform(-1.0, 1.0, len(timeline(seconds)))
    if smooth <= 1:
        return values
    kernel = np.ones(smooth, dtype=np.float64) / smooth
    return np.convolve(values, kernel, mode="same")


def delay(signal: np.ndarray, seconds: float) -> np.ndarray:
    return np.pad(signal, (round(seconds * SAMPLE_RATE), 0))


def mix(seconds: float, *signals: tuple[np.ndarray, float]) -> np.ndarray:
    result = np.zeros(len(timeline(seconds)), dtype=np.float64)
    for signal, gain in signals:
        count = min(len(result), len(signal))
        result[:count] += signal[:count] * gain
    return result


def whoosh(seconds: float, low: float, high: float, airy_gain: float = 0.18) -> np.ndarray:
    t = timeline(seconds)
    sweep = oscillator(seconds, low, high)
    airy = noise(seconds, 5)
    shape = np.sin(np.linspace(0.0, np.pi, len(t))) ** 1.6
    # Keep the motion readable through the tonal sweep. Earlier prototypes used
    # 0.8 noise gain, which made repeated attacks sound like constant wind.
    return (sweep * 0.72 + airy * airy_gain) * shape


def blade_slash(seconds: float, low: float, high: float) -> np.ndarray:
    t = timeline(seconds)
    sweep = whoosh(seconds, low, high, 0.12)
    edge = oscillator(seconds, high * 1.7, max(180.0, low * 1.25)) * np.exp(-t * 13.0)
    tick = noise(seconds, 2) * np.exp(-t * 48.0)
    return sweep * 0.72 + edge * 0.34 + tick * 0.12


def impact(seconds: float, body_hz: float, metallic: float = 0.0) -> np.ndarray:
    t = timeline(seconds)
    body = oscillator(seconds, body_hz * 1.8, body_hz) * np.exp(-t * 16.0)
    crack = noise(seconds, 2) * np.exp(-t * 35.0)
    ring = oscillator(seconds, body_hz * 5.7) * np.exp(-t * 8.0) * metallic
    return body * 0.8 + crack * 0.55 + ring


def pulse(seconds: float, frequencies: list[float], spacing: float = 0.11) -> np.ndarray:
    result = np.zeros(len(timeline(seconds)), dtype=np.float64)
    for index, frequency in enumerate(frequencies):
        tone = oscillator(0.16, frequency, frequency * 1.04) * envelope(len(timeline(0.16)), 0.005, 0.12)
        start = round(index * spacing * SAMPLE_RATE)
        end = min(len(result), start + len(tone))
        result[start:end] += tone[: end - start]
    return result


def stereo(signal: np.ndarray, width: float = 0.08) -> np.ndarray:
    shift = max(1, round(width * 0.001 * SAMPLE_RATE))
    right = np.roll(signal, shift)
    right[:shift] = 0.0
    return np.column_stack((signal, right))


def write(name: str, signal: np.ndarray, peak_db: float = -3.0, width: float = 0.08) -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    data = stereo(signal, width)
    peak = float(np.max(np.abs(data)))
    if peak > 0.0:
        data *= (10.0 ** (peak_db / 20.0)) / peak
    pcm = np.clip(data, -1.0, 1.0)
    pcm = (pcm * 32767.0).astype("<i2")
    with wave.open(str(OUTPUT / name), "wb") as handle:
        handle.setnchannels(2)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(pcm.tobytes())


def combat_assets() -> None:
    write("SFX_Player_Melee_Swing_A.wav", blade_slash(0.28, 180, 1100), -5.0)
    write("SFX_Player_Melee_Swing_B.wav", blade_slash(0.25, 230, 1350), -5.0)
    write("SFX_Player_Ranged_Fire.wav", mix(0.36,
        (oscillator(0.28, 240, 1250) * envelope(len(timeline(0.28)), 0.006, 0.18), 0.75),
        (noise(0.12, 3) * envelope(len(timeline(0.12)), 0.002, 0.09), 0.28)), -3.5)
    write("SFX_Impact_Light_A.wav", impact(0.24, 105), -3.0)
    write("SFX_Impact_Light_B.wav", impact(0.22, 125), -3.0)
    write("SFX_Impact_Heavy.wav", impact(0.42, 62, 0.32), -2.5)
    write("SFX_Player_Hit.wav", mix(0.44,
        (impact(0.34, 72), 0.8),
        (oscillator(0.4, 310, 115) * envelope(len(timeline(0.4)), 0.01, 0.32), 0.28)), -2.5)
    write("SFX_Player_Death.wav", mix(1.15,
        (impact(0.5, 48, 0.15), 0.8),
        (oscillator(1.1, 260, 48) * envelope(len(timeline(1.1)), 0.015, 0.75), 0.42)), -2.5)
    write("SFX_Enemy_Death.wav", mix(0.7,
        (impact(0.38, 82, 0.2), 0.75),
        (oscillator(0.66, 430, 90) * envelope(len(timeline(0.66)), 0.01, 0.5), 0.34)), -3.0)
    write("SFX_Enemy_Melee_Telegraph.wav", pulse(0.38, [430, 540]), -6.0)
    write("SFX_Enemy_Melee_Attack.wav", mix(0.32,
        (whoosh(0.3, 760, 120), 0.85), (impact(0.18, 92), 0.25)), -3.5)
    write("SFX_Enemy_Ranged_Telegraph.wav", pulse(0.58, [640, 760, 920], 0.14), -6.0)
    write("SFX_Enemy_Ranged_Fire.wav", mix(0.35,
        (oscillator(0.32, 980, 170) * envelope(len(timeline(0.32)), 0.003, 0.25), 0.72),
        (noise(0.11, 3) * envelope(len(timeline(0.11)), 0.002, 0.08), 0.3)), -3.0)
    write("SFX_Skill_FocusedVolley_Start.wav", mix(0.62,
        (pulse(0.58, [520, 780, 1040], 0.11), 0.7),
        (oscillator(0.58, 140, 620) * envelope(len(timeline(0.58)), 0.02, 0.2), 0.32)), -5.0)
    write("SFX_Skill_FocusedVolley_Shot.wav", mix(0.3,
        (oscillator(0.26, 1250, 260) * envelope(len(timeline(0.26)), 0.002, 0.18), 0.8),
        (impact(0.15, 145, 0.12), 0.28)), -4.0)
    write("SFX_Skill_FourSlash_Start.wav", mix(0.55,
        (pulse(0.5, [330, 495, 740], 0.1), 0.55),
        (oscillator(0.52, 105, 440) * envelope(len(timeline(0.52)), 0.015, 0.2), 0.4)), -4.5)
    write("SFX_Skill_FourSlash_Hit.wav", mix(0.42,
        (blade_slash(0.34, 210, 1550), 0.68),
        (delay(impact(0.25, 68, 0.42), 0.08), 0.62)), -2.8)


def boss_assets() -> None:
    write("SFX_Helte_Intro_Warning.wav", mix(1.1,
        (pulse(1.0, [180, 270, 405], 0.25), 0.65),
        (oscillator(1.05, 65, 92) * envelope(len(timeline(1.05)), 0.08, 0.45), 0.4)), -4.0, 0.25)
    write("SFX_Helte_Phase2.wav", mix(1.25,
        (oscillator(1.2, 95, 420) * envelope(len(timeline(1.2)), 0.04, 0.4), 0.55),
        (noise(1.0, 18) * envelope(len(timeline(1.0)), 0.18, 0.35), 0.45),
        (delay(impact(0.42, 55, 0.4), 0.72), 0.75)), -2.5, 0.35)
    write("SFX_Helte_FinalRush.wav", mix(1.35,
        (oscillator(1.3, 74, 680) * envelope(len(timeline(1.3)), 0.025, 0.38), 0.58),
        (pulse(1.3, [220, 330, 495, 740], 0.2), 0.5),
        (delay(impact(0.45, 48, 0.45), 0.82), 0.8)), -2.0, 0.45)
    write("SFX_Helte_Basic_Windup.wav", pulse(0.32, [320, 480], 0.1), -5.0)
    write("SFX_Helte_Slash.wav", mix(0.38,
        (blade_slash(0.34, 145, 1500), 0.9), (impact(0.2, 88, 0.25), 0.28)), -3.0)
    write("SFX_Helte_Blink_Out.wav", whoosh(0.42, 260, 2100), -3.5, 0.5)
    write("SFX_Helte_Blink_In.wav", mix(0.4,
        (whoosh(0.36, 1900, 240), 0.8), (impact(0.25, 90, 0.2), 0.38)), -3.0, 0.45)
    write("SFX_Helte_Dash_Telegraph.wav", pulse(0.42, [520, 690, 920], 0.1), -5.0)
    write("SFX_Helte_Dash.wav", whoosh(0.48, 1800, 95, 0.22), -3.8, 0.35)
    write("SFX_Helte_Cross_Telegraph.wav", pulse(0.34, [720, 510, 880], 0.08), -4.5)
    write("SFX_Helte_Cross_Slash.wav", mix(0.55,
        (whoosh(0.4, 1700, 120), 0.75),
        (delay(whoosh(0.4, 1450, 105), 0.08), 0.75),
        (delay(impact(0.28, 70, 0.35), 0.12), 0.42)), -2.0, 0.6)
    write("SFX_Helte_Sword_Focus.wav", mix(0.72,
        (pulse(0.68, [640, 810, 1020], 0.14), 0.65),
        (oscillator(0.7, 110, 460) * envelope(len(timeline(0.7)), 0.08, 0.25), 0.35)), -4.0, 0.35)
    write("SFX_Helte_Sword_Fire.wav", mix(0.34,
        (whoosh(0.3, 1200, 230), 0.58),
        (oscillator(0.27, 880, 210) * envelope(len(timeline(0.27)), 0.003, 0.2), 0.65)), -3.0)
    write("SFX_Helte_Counter_Telegraph.wav", pulse(0.48, [290, 580, 290], 0.13), -5.0)
    write("SFX_Helte_Counter.wav", mix(0.45,
        (impact(0.4, 58, 0.75), 0.9),
        (oscillator(0.4, 1200, 430) * envelope(len(timeline(0.4)), 0.002, 0.3), 0.35)), -2.0)
    write("SFX_Helte_Mercy.wav", pulse(0.85, [520, 660, 820], 0.2), -7.0, 0.25)
    write("SFX_Helte_Victory.wav", mix(1.1,
        (pulse(1.0, [740, 620, 440], 0.22), 0.5),
        (oscillator(1.05, 180, 72) * envelope(len(timeline(1.05)), 0.01, 0.8), 0.38)), -4.0, 0.25)


def flow_assets() -> None:
    write("SFX_UI_Dialogue_Advance.wav", pulse(0.18, [780]), -10.0)
    write("SFX_UI_Objective_Update.wav", pulse(0.42, [540, 720, 960], 0.09), -8.0)
    write("SFX_UI_Tutorial_Complete.wav", pulse(0.9, [440, 660, 880, 1100], 0.14), -7.0, 0.25)
    write("SFX_UI_Panel_Open.wav", whoosh(0.22, 260, 980), -9.0)
    write("SFX_UI_Panel_Close.wav", whoosh(0.2, 880, 220), -9.0)
    write("SFX_World_Item_Pickup.wav", pulse(0.7, [620, 930, 1240], 0.13), -6.0, 0.3)
    write("SFX_World_Relay_Activate.wav", mix(0.9,
        (oscillator(0.85, 82, 360) * envelope(len(timeline(0.85)), 0.04, 0.28), 0.55),
        (pulse(0.85, [420, 630, 840], 0.16), 0.45)), -4.0, 0.35)
    write("SFX_World_Gate_Open.wav", mix(0.82,
        (noise(0.78, 30) * envelope(len(timeline(0.78)), 0.04, 0.28), 0.7),
        (oscillator(0.75, 120, 58) * envelope(len(timeline(0.75)), 0.02, 0.42), 0.4)), -5.0)
    write("SFX_World_Encounter_Start.wav", pulse(0.7, [220, 330, 495], 0.14), -5.0)
    write("SFX_World_Encounter_Clear.wav", pulse(0.82, [420, 630, 840, 1050], 0.12), -6.0)
    write("SFX_Player_Jump.wav", whoosh(0.24, 180, 720), -7.0)
    write("SFX_Player_Land.wav", impact(0.24, 74), -6.0)
    write("SFX_Player_Dash.wav", whoosh(0.38, 1500, 110, 0.16), -5.5, 0.28)


def main() -> None:
    combat_assets()
    boss_assets()
    flow_assets()
    print(f"Rendered {len(list(OUTPUT.glob('*.wav')))} tutorial SFX files to {OUTPUT}")


if __name__ == "__main__":
    main()
