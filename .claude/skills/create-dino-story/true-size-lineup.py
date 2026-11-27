"""Slice 447: cut a Gemini line-up row into its characters and redraw them at true relative size.

Gemini draws every character of a line-up at about the same height. This finds each separate figure on the plain
background (connected regions), then rescales each to its own height in Carrot units (Carrot = 1.0) and lines them up on one
baseline. Run it with `python -I` (Pillow only):

  python -I true-size-lineup.py <row.jpg> <out.jpg> --names Carrot,Mouse,Owl --heights 1,0.3,0.6 [--carrot-px 900]

Figures are numbered left to right in the row, so --names lists them in that order. If the number of regions found differs
from the number of names, nothing is written and the found boxes are printed (exit 2): adjust --gap (joins pieces of one
figure) or --bg-tol, or --min-area (drops specks), and run again. Exit 0 prints each figure's source box and final height.
"""
import argparse
import math
import sys
from PIL import Image, ImageChops, ImageFilter

DEFAULT_CARROT_PX = 900
DEFAULT_BG_TOLERANCE = 40
DEFAULT_GAP = 9           # odd; dilation size at the working scale that joins arms, ears and tails to their body
DEFAULT_MIN_AREA = 0.002  # fraction of the picture a region needs to count as a figure
WORK_WIDTH = 600          # regions are found on a copy this wide: fast, and small gaps close
MAX_HEIGHT_UNITS = 20     # tallest figure, in Carrot units: rejects inf and absurd values
MAX_CANVAS_PX = 20000     # tallest figure in pixels (height x --carrot-px), so the canvas stays allocatable
MARGIN_FRACTION = 0.08   # space between figures, as a fraction of Carrot's height


def background_colour(image):
    """The median of the four corners: a line-up sits on a plain background."""
    w, h = image.size
    corners = sorted(image.getpixel(p) for p in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)))
    return corners[len(corners) // 2]


def foreground_mask(image, tolerance, gap):
    """A 0/255 'L' image: 255 where the pixel differs from the background, dilated by `gap` pixels to join a figure's parts."""
    bg = Image.new("RGB", image.size, background_colour(image))
    diff = ImageChops.difference(image, bg).convert("L")
    mask = diff.point(lambda v: 255 if v > tolerance else 0)
    return mask.filter(ImageFilter.MaxFilter(gap)) if gap > 1 else mask


def label_regions(mask):
    """Connected regions (4-neighbour) of non-zero pixels as (left, top, right, bottom, pixel count), right and bottom exclusive."""
    w, h = mask.size
    data = mask.tobytes()
    seen = bytearray(w * h)
    regions = []
    for start in range(w * h):
        if data[start] and not seen[start]:
            regions.append(_flood(data, seen, w, h, start))
    return regions


def _flood(data, seen, w, h, start):
    stack = [start]
    seen[start] = 1
    left, top, right, bottom, count = w, h, 0, 0, 0
    while stack:
        i = stack.pop()
        x, y = i % w, i // w
        count += 1
        left, top, right, bottom = min(left, x), min(top, y), max(right, x + 1), max(bottom, y + 1)
        for j in _neighbours(i, x, y, w, h):
            if data[j] and not seen[j]:
                seen[j] = 1
                stack.append(j)
    return (left, top, right, bottom, count)


def _neighbours(i, x, y, w, h):
    if x > 0:
        yield i - 1
    if x < w - 1:
        yield i + 1
    if y > 0:
        yield i - w
    if y < h - 1:
        yield i + w


def find_figures(image, tolerance=DEFAULT_BG_TOLERANCE, gap=DEFAULT_GAP, min_area=DEFAULT_MIN_AREA):
    """Boxes (left, top, right, bottom) in the full-size image of every figure, left to right."""
    scale = max(1, image.width // WORK_WIDTH)
    small = image.reduce(scale) if scale > 1 else image
    regions = label_regions(foreground_mask(small, tolerance, gap))
    floor = min_area * small.width * small.height
    boxes = [(l * scale, t * scale, min(r * scale, image.width), min(b * scale, image.height))
             for (l, t, r, b, n) in regions if n >= floor]
    return sorted(_tighten(foreground_mask(image, tolerance, 1), b) for b in boxes)


def _tighten(full_mask, box):
    """Shrinks a (dilated) region box to the real figure: the bounding box of the undilated mask inside it."""
    inner = full_mask.crop(box).getbbox()
    if inner is None:
        return box
    return (box[0] + inner[0], box[1] + inner[1], box[0] + inner[2], box[1] + inner[3])


def compose(image, boxes, heights, carrot_px):
    """Each figure scaled so its box is heights[i] * carrot_px tall, bottoms on one baseline, on the original background."""
    figures = []
    for box, units in zip(boxes, heights):
        crop = image.crop(box)
        target_h = max(1, round(units * carrot_px))
        figures.append(crop.resize((max(1, round(crop.width * target_h / crop.height)), target_h), Image.Resampling.LANCZOS))
    margin = round(MARGIN_FRACTION * carrot_px)
    canvas = Image.new("RGB", (sum(f.width for f in figures) + margin * (len(figures) + 1), max(f.height for f in figures) + 2 * margin),
                       background_colour(image))
    x = margin
    for fig in figures:
        canvas.paste(fig, (x, canvas.height - margin - fig.height))
        x += fig.width + margin
    return canvas


def parse_args(argv):
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("row")
    p.add_argument("out")
    p.add_argument("--names", required=True, help="comma-separated, left to right")
    p.add_argument("--heights", required=True, help="comma-separated heights in Carrot units (Carrot = 1), same order")
    p.add_argument("--carrot-px", type=int, default=DEFAULT_CARROT_PX)
    p.add_argument("--bg-tol", type=int, default=DEFAULT_BG_TOLERANCE)
    p.add_argument("--gap", type=int, default=DEFAULT_GAP)
    p.add_argument("--min-area", type=float, default=DEFAULT_MIN_AREA)
    return p.parse_args(argv)


def main(argv):
    args = parse_args(argv)
    names = [n.strip() for n in args.names.split(",")]
    try:
        heights = [float(h) for h in args.heights.split(",")]
    except ValueError:
        print(f"ERROR: --heights must be comma-separated numbers, got '{args.heights}'", file=sys.stderr)
        return 2
    if any(not (math.isfinite(h) and 0 < h <= MAX_HEIGHT_UNITS) for h in heights):
        print(f"ERROR: every --heights value must be greater than 0 and at most {MAX_HEIGHT_UNITS}, got '{args.heights}'", file=sys.stderr)
        return 2
    if args.carrot_px < 1:
        print(f"ERROR: --carrot-px must be at least 1, got {args.carrot_px}", file=sys.stderr)
        return 2
    if max(heights) * args.carrot_px > MAX_CANVAS_PX:
        print(f"ERROR: the tallest figure would be {round(max(heights) * args.carrot_px)} px; at most {MAX_CANVAS_PX}", file=sys.stderr)
        return 2
    if len(names) != len(heights):
        print(f"ERROR: {len(names)} names but {len(heights)} heights", file=sys.stderr)
        return 2
    if args.gap < 1:
        print(f"ERROR: --gap must be at least 1, got {args.gap}", file=sys.stderr)
        return 2
    gap = args.gap
    if gap % 2 == 0:  # ImageFilter.MaxFilter takes only odd sizes
        gap += 1
        print(f"NOTE: --gap {args.gap} is even; using {gap} (the filter needs an odd size)", file=sys.stderr)
    image = Image.open(args.row).convert("RGB")
    boxes = find_figures(image, args.bg_tol, gap, args.min_area)
    if len(boxes) != len(names):
        print(f"ERROR: found {len(boxes)} figures, expected {len(names)}; boxes (left, top, right, bottom): {boxes}", file=sys.stderr)
        return 2
    compose(image, boxes, heights, args.carrot_px).save(args.out, quality=95)
    for name, box, units in zip(names, boxes, heights):
        print(f"{name}: source box {box}, drawn {round(units * args.carrot_px)} px tall ({units} Carrot)")
    print(f"Saved {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
