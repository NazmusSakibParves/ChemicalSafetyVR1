"""Scenario 19 tank-roof / wall / ground textures that match the client's workflow stills (bright orange rust roof,
pale grey weathered concrete wall, dusty grey ground). Tileable 1024 px PNG, written to Assets/Art/Presentable/3D/Sc19/Tex.
Run: python -I Tools/generate_sc19_textures.py
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc19", "Tex")
N = 1024
rng = np.random.default_rng(1913)


def noise(scale, seed):
    """Tileable value noise: white noise low-passed in the Fourier domain."""
    r = np.random.default_rng(seed)
    f = np.fft.fft2(r.standard_normal((N, N)))
    ky = np.fft.fftfreq(N)[:, None]
    kx = np.fft.fftfreq(N)[None, :]
    k = np.sqrt(kx * kx + ky * ky) + 1e-6
    f = f * np.exp(-(k * scale) ** 2) / (k ** 0.35)
    a = np.real(np.fft.ifft2(f))
    a -= a.min()
    return a / a.max()


def save(name, arr):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name), optimize=True)
    print(name)


# --- roof: bright orange rust, mottled, darker brown pits, lighter dusty patches (workflow stills)
big, mid, fine = noise(60, 1), noise(18, 2), noise(5, 3)
t = 0.55 * big + 0.30 * mid + 0.15 * fine
orange = np.array([0.82, 0.43, 0.12])
dark = np.array([0.52, 0.24, 0.07])
light = np.array([0.92, 0.62, 0.30])
roof = dark[None, None] * (1 - t[..., None]) + orange[None, None] * t[..., None]
roof = roof * (0.9 + 0.2 * fine[..., None])
patch = np.clip((noise(40, 4) - 0.62) * 4, 0, 1)[..., None]
roof = roof * (1 - 0.45 * patch) + light[None, None] * 0.45 * patch
save("Sc19_RoofRust.png", roof)

# --- wall: pale warm-grey concrete, soft vertical stains and rust drips
w = 0.6 * noise(80, 5) + 0.4 * noise(10, 6)
stain = noise(200, 7)[..., None]
base = np.array([0.74, 0.72, 0.68])
wall = base[None, None] * (0.82 + 0.3 * w[..., None])
drip = np.clip((noise(8, 8) - 0.55) * 3, 0, 1)
col = np.linspace(0, 1, N)[None, :]
streaks = np.clip(noise(150, 9) * 1.6 - 0.45, 0, 1) * (0.5 + 0.5 * np.cos(col * 40))
wall = wall * (1 - 0.18 * stain) + np.array([0.66, 0.38, 0.18])[None, None] * (0.35 * streaks * drip)[..., None]
save("Sc19_WallConcrete.png", wall)

# --- ground: dusty grey concrete slab
g = 0.5 * noise(100, 10) + 0.3 * noise(20, 11) + 0.2 * noise(4, 12)
ground = np.array([0.55, 0.55, 0.54])[None, None] * (0.8 + 0.4 * g[..., None])
save("Sc19_GroundConcrete.png", ground)

# --- galvanized mesh railing / hatch cover: a darker rusty plate
c = 0.6 * noise(30, 13) + 0.4 * noise(6, 14)
cover = np.array([0.62, 0.34, 0.12])[None, None] * (0.7 + 0.5 * c[..., None])
save("Sc19_CoverRust.png", cover)
