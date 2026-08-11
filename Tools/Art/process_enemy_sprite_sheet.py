#!/usr/bin/env python3
"""Slice chroma-keyed enemy sheets into baseline-aligned Unity sprites."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def visible_bbox(image: Image.Image, threshold: int = 8) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A")
    mask = alpha.point(lambda value: 255 if value > threshold else 0)
    bbox = mask.getbbox()
    if bbox is None:
        raise ValueError("frame contains no visible pixels")
    return bbox


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--prefix", required=True)
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--rows", type=int, default=2)
    parser.add_argument("--canvas", type=int, default=512)
    parser.add_argument("--padding", type=int, default=18)
    args = parser.parse_args()

    source_path = Path(args.input)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    with Image.open(source_path) as loaded:
        source = loaded.convert("RGBA")

    usable_width = source.width - source.width % args.columns
    usable_height = source.height - source.height % args.rows
    source = source.crop((0, 0, usable_width, usable_height))
    cell_width = usable_width // args.columns
    cell_height = usable_height // args.rows

    frames: list[Image.Image] = []
    boxes: list[tuple[int, int, int, int]] = []
    for row in range(args.rows):
        for column in range(args.columns):
            frame = source.crop(
                (
                    column * cell_width,
                    row * cell_height,
                    (column + 1) * cell_width,
                    (row + 1) * cell_height,
                )
            )
            frames.append(frame)
            boxes.append(visible_bbox(frame))

    max_width = max(right - left for left, top, right, bottom in boxes)
    max_height = max(bottom - top for left, top, right, bottom in boxes)
    available = args.canvas - args.padding * 2
    scale = min(available / max_width, available / max_height)

    for index, (frame, bbox) in enumerate(zip(frames, boxes)):
        cropped = frame.crop(bbox)
        target_width = max(1, round(cropped.width * scale))
        target_height = max(1, round(cropped.height * scale))
        resized = cropped.resize((target_width, target_height), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (args.canvas, args.canvas), (0, 0, 0, 0))
        x = (args.canvas - target_width) // 2
        y = args.canvas - args.padding - target_height
        canvas.alpha_composite(resized, (x, y))
        output_path = output_dir / f"{args.prefix}_{index:02d}.png"
        canvas.save(output_path, optimize=True)
        print(output_path)

    print(
        f"Processed {len(frames)} frames from {source.width}x{source.height}; "
        f"cell={cell_width}x{cell_height}, common_scale={scale:.4f}"
    )


if __name__ == "__main__":
    main()
