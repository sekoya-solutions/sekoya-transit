from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path
from statistics import median

from PIL import Image, ImageChops, ImageDraw


ASSETS = {
    Path("issue16-pilot-assets-v3/grass-texture.png"): (
        "ground_grass_01.png",
        (128, 64),
        "ground",
        None,
    ),
    Path("issue16-pilot-assets-v2/town-building.png"): (
        "building_town_shop_01.png",
        (128, 128),
        "sprite",
        (560, 0, 1024, 400),
    ),
    Path("issue16-pilot-assets-v2/industry-building.png"): (
        "building_industry_flour_mill_01.png",
        (224, 240),
        "sprite",
        (80, 0, 940, 1024),
    ),
    Path("issue16-pilot-assets-v2/truck.png"): (
        "vehicle_road_cargo_lorry_01.png",
        (128, 128),
        "sprite",
        (210, 660, 690, 1000),
    ),
}


def remove_flat_background(image: Image.Image) -> Image.Image:
    source = image.convert("RGB")
    width, height = source.size
    pixels = source.load()

    samples = []
    for left, top in ((0, 0), (width - 16, 0), (0, height - 16), (width - 16, height - 16)):
        for y in range(top, top + 16):
            for x in range(left, left + 16):
                samples.append(pixels[x, y])

    background = tuple(int(median(channel)) for channel in zip(*samples))
    variation = max(
        max(abs(color[channel] - background[channel]) for channel in range(3))
        for color in samples
    )
    if variation > 48:
        raise ValueError(f"Background is not sufficiently uniform (corner variation {variation})")

    total = width * height
    connected = bytearray(total)
    queue = deque()

    def add_background(x: int, y: int) -> None:
        index = y * width + x
        if connected[index]:
            return
        color = pixels[x, y]
        if max(abs(color[channel] - background[channel]) for channel in range(3)) <= 96:
            connected[index] = 1
            queue.append(index)

    for x in range(width):
        add_background(x, 0)
        add_background(x, height - 1)
    for y in range(height):
        add_background(0, y)
        add_background(width - 1, y)

    while queue:
        index = queue.popleft()
        x = index % width
        y = index // width
        if x > 0:
            add_background(x - 1, y)
        if x + 1 < width:
            add_background(x + 1, y)
        if y > 0:
            add_background(x, y - 1)
        if y + 1 < height:
            add_background(x, y + 1)

    removed_ratio = sum(connected) / total
    if not 0.05 <= removed_ratio <= 0.9:
        raise ValueError(f"Background mask coverage is suspicious ({removed_ratio:.1%})")

    output = Image.new("RGBA", source.size)
    output_pixels = output.load()
    for y in range(height):
        for x in range(width):
            color = pixels[x, y]
            index = y * width + x
            if not connected[index]:
                output_pixels[x, y] = (*color, 255)
                continue

            distance = max(abs(color[channel] - background[channel]) for channel in range(3))
            alpha = max(0, min(255, round((distance - 8) * 255 / (80 - 8))))
            if alpha == 0:
                output_pixels[x, y] = (*color, 0)
                continue

            foreground = tuple(
                max(
                    0,
                    min(
                        255,
                        round(
                            (color[channel] * 255 - background[channel] * (255 - alpha))
                            / alpha
                        ),
                    ),
                )
                for channel in range(3)
            )
            output_pixels[x, y] = (*foreground, alpha)

    return output


def alpha_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("No foreground remained after background removal")
    return bounds


def fit_sprite(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    width, height = size
    content = image.crop(alpha_bounds(image))
    content.thumbnail((width - 4, height - 4), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.alpha_composite(content, ((width - content.width) // 2, height - content.height))
    return canvas


def make_ground_tile(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    width, height = size
    scale = 4
    scaled_size = (width * scale, height * scale)
    source = image.convert("RGBA")
    if source.width * height > source.height * width:
        crop_width = round(source.height * width / height)
        left = (source.width - crop_width) // 2
        source = source.crop((left, 0, left + crop_width, source.height))
    else:
        crop_height = round(source.width * height / width)
        top = (source.height - crop_height) // 2
        source = source.crop((0, top, source.width, top + crop_height))

    canvas = source.resize(scaled_size, Image.Resampling.LANCZOS)
    mask = Image.new("L", scaled_size, 0)
    draw = ImageDraw.Draw(mask)
    draw.polygon(
        (
            (width * scale // 2, 0),
            (width * scale - 1, height * scale // 2),
            (width * scale // 2, height * scale - 1),
            (0, height * scale // 2),
        ),
        fill=255,
    )
    canvas.putalpha(ImageChops.multiply(canvas.getchannel("A"), mask))
    return canvas.resize(size, Image.Resampling.LANCZOS)


def curate(input_dir: Path, output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    for source_name, (output_name, size, kind, crop_box) in ASSETS.items():
        source_path = input_dir / source_name
        if not source_path.is_file():
            raise FileNotFoundError(f"Missing generated source image: {source_path}")

        with Image.open(source_path) as source:
            cropped = source.crop(crop_box) if crop_box else source.copy()
            if kind == "ground":
                curated = make_ground_tile(cropped, size)
            else:
                curated = fit_sprite(remove_flat_background(cropped), size)
        output_path = output_dir / output_name
        curated.save(output_path, format="PNG", optimize=True)
        print(f"{source_name} -> {output_path} ({size[0]}x{size[1]})")


def main() -> None:
    parser = argparse.ArgumentParser(description="Curate the Issue #16 SDXL pilot sprites.")
    parser.add_argument("--input-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    curate(args.input_dir, args.output_dir)


if __name__ == "__main__":
    main()
