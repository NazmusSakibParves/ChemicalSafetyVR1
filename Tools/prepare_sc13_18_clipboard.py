"""Clipboard pictures of the safety data sheets of Scenarios 13-18 (the left-X-button clipboard, like every other scenario): WHITE paper on the board.

    python -X utf8 -I Tools/prepare_sc13_18_clipboard.py

Takes the designer's wooden clipboard with the black binder clip (Assets/Art/Presentable/2D/Designer/SDS in Clipboard.png), cuts the paper
sheet out of it and fits each SDS page on the board under the clip:
  * Scenarios 17 / 18: the client's original paper pictures (Assets/Art/Presentable/2D/ScCheck/SdsPaper/<SC>_sds{n}.png), as they are;
  * Scenarios 13-16: their sheets (Resources/ScCheck/sds1..3.png) are dark navy, so they are turned into black ink on white paper here
    (the pages themselves are unchanged, only the colours are inverted).
Output (loaded by ScCheckRunner.ClipSprite):
    Assets/Resources/ScCheck/clip_sds{n}.png            (13-16)
    Assets/Resources/ScCheck/SC17|SC18/clip_sds{n}.png
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, "Assets", "Resources", "ScCheck")
PAPERDIR = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "ScCheck", "SdsPaper")
BOARD = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Designer", "SDS in Clipboard.png")
PAPER = np.array([243.0, 242.0, 237.0])
INK = np.array([24.0, 24.0, 28.0])

board = Image.open(BOARD).convert("RGBA")
arr = np.asarray(board).astype(np.int32)
alpha = arr[..., 3]
mn = arr[..., :3].min(axis=2)
mx = arr[..., :3].max(axis=2)
paper = (alpha > 200) & (mn > 205) & ((mx - mn) < 40)
rows = np.where(paper.sum(axis=1) > board.width * 0.25)[0]
cols = np.where(paper.sum(axis=0) > board.height * 0.25)[0]
top, bottom, left, right = int(rows.min()), int(rows.max()), int(cols.min()), int(cols.max())
pw, ph = right - left + 1, bottom - top + 1
print("board", board.size, "paper box", (left, top, right, bottom), "size", pw, ph)

# the binder clip (dark) that overlaps the top edge of the paper, to be put back over the new sheet
dark = (mx < 95) & (alpha > 200)
clip_mask = np.zeros_like(dark)
cx0, cx1 = board.width // 2 - 130, board.width // 2 + 130
clip_mask[0:top + 16, cx0:cx1] = dark[0:top + 16, cx0:cx1]
clip_img = Image.fromarray((clip_mask * 255).astype(np.uint8), "L")


def invert_to_paper(path):
    """Dark navy sheet -> black ink on white paper (luminance inversion, coloured fills turn into light grey)."""
    src = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    v = (0.299 * src[..., 0] + 0.587 * src[..., 1] + 0.114 * src[..., 2]) / 255.0
    t = np.clip((0.95 - v) / 0.80, 0.0, 1.0)
    out = PAPER[None, None, :] * t[..., None] + INK[None, None, :] * (1.0 - t[..., None])
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB")


def fit(sheet, crop_top, pad):
    """sheet: RGB image of a white-paper page."""
    if crop_top:
        sheet = sheet.crop((0, int(sheet.height * crop_top), sheet.width, sheet.height))  # a gold clip is printed on the top of 17
    bg = tuple(int(c) for c in PAPER)
    if pad:  # 17 / 18 start right at the top edge: a blank band so the binder clip does not cover the title
        padded = Image.new("RGB", (sheet.width, sheet.height + int(sheet.height * 0.07)), bg)
        padded.paste(sheet, (0, int(sheet.height * 0.07)))
        sheet = padded
    h = int(round(sheet.height * pw / sheet.width))
    sheet = sheet.resize((pw, h), Image.LANCZOS)
    if h < ph:  # the page is a little shorter than the paper: the blank paper continues
        pad_img = Image.new("RGB", (pw, ph), bg)
        pad_img.paste(sheet, (0, 0))
        sheet = pad_img
    else:
        sheet = sheet.crop((0, 0, pw, ph))
    out = board.copy()
    out.paste(sheet.convert("RGBA"), (left, top))
    out.paste(board, (0, 0), clip_img)  # the clip stays in front of the sheet
    return out


jobs = []
for n in (1, 2, 3):
    jobs.append((invert_to_paper(os.path.join(BASE, "sds%d.png" % n)), os.path.join(BASE, "clip_sds%d.png" % n), 0.0, False))
    for sc in ("SC17", "SC18"):
        orig = Image.open(os.path.join(PAPERDIR, "%s_sds%d.png" % (sc, n))).convert("RGB")
        jobs.append((orig, os.path.join(BASE, sc, "clip_sds%d.png" % n), 0.045 if sc == "SC17" else 0.0, True))
for sheet, dst, crop, pad in jobs:
    fit(sheet, crop, pad).save(dst, optimize=True)
    print("wrote", os.path.relpath(dst, ROOT))

check = Image.new("RGBA", (3 * 420, 470), (70, 110, 150, 255))
for k in range(3):
    im = Image.open(jobs[k][1]).copy()
    im.thumbnail((410, 460))
    check.alpha_composite(im, (k * 420 + 5, 5))
check.convert("RGB").save(os.path.join(ROOT, "Temp", "clip_check.png"))
