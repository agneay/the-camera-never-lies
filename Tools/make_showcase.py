"""Builds store-page screenshots and key art from the clean trailer stills.

Usage:  python make_showcase.py <clean_stills_dir> <output_dir>
Stills come from:  TheCameraNeverLies.exe -trailer -clean -shots <dir>
"""
import os
import sys

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

FONTS = r"C:\Windows\Fonts"
CREAM = (238, 230, 214)
RED = (220, 62, 52)
GREY = (190, 182, 168)

SHOTS = [  # (still, screenshot name)
    ("02_eye_symbol", "01_the_door_that_isnt_there"),
    ("04_portraits", "02_every_photograph_is_evidence"),
    ("05_corridor_viewfinder", "03_something_at_the_end_of_the_corridor"),
    ("06_behind_you", "04_some_photographs_change"),
    ("07_darkroom_print", "05_the_darkroom_print"),
    ("10_ghost_viewfinder", "06_through_the_lens_it_cannot_move"),
    ("12_ghost_flash", "07_the_flash_drives_it_back"),
    ("14_family_lens", "08_the_missing_person"),
    ("16_jumpscare", "09_dont_let_it_find_you"),
    ("17_it_found_you", "10_it_found_you"),
]


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def text_shadow(draw, xy, text, fnt, fill, anchor="la", blur_layer=None, offset=4):
    x, y = xy
    draw.text((x + offset, y + offset), text, font=fnt, fill=(0, 0, 0), anchor=anchor)
    draw.text((x, y), text, font=fnt, fill=fill, anchor=anchor)


def vignette_gradient(size, bottom=0.92, left=0.0, top=0.25):
    """Dark gradient overlay so titles stay legible."""
    w, h = size
    g = Image.new("L", (w, h), 0)
    px = g.load()
    for y in range(h):
        v = y / h
        b = max(0.0, (v - 0.45) / 0.55) ** 1.4 * bottom + max(0.0, (0.18 - v) / 0.18) * top
        for x in range(0, w, 4):
            l = max(0.0, (0.55 - x / w) / 0.55) ** 1.5 * left
            a = int(min(1.0, b + l) * 255)
            for dx in range(4):
                if x + dx < w:
                    px[x + dx, y] = a
    black = Image.new("RGB", (w, h), (0, 0, 0))
    return black, g


def darken(img, amount):
    return ImageEnhance.Brightness(img).enhance(amount)


def cover_crop(img, w, h, cx=0.5, cy=0.5):
    """Scale to cover w×h, then crop around (cx, cy)."""
    s = max(w / img.width, h / img.height)
    r = img.resize((int(img.width * s + 0.5), int(img.height * s + 0.5)), Image.LANCZOS)
    x = int(min(max(cx * r.width - w / 2, 0), r.width - w))
    y = int(min(max(cy * r.height - h / 2, 0), r.height - h))
    return r.crop((x, y, x + w, y + h))


def title_block(img, scale, x, y, anchor="la", tagline=True):
    d = ImageDraw.Draw(img)
    t1 = font("georgiab.ttf", int(96 * scale))
    t2 = font("georgiai.ttf", int(54 * scale))
    t3 = font("segoeui.ttf", int(30 * scale))
    text_shadow(d, (x, y), "THE CAMERA", t1, CREAM, anchor, offset=max(2, int(5 * scale)))
    text_shadow(d, (x, y + int(100 * scale)), "NEVER LIES", t1, CREAM, anchor, offset=max(2, int(5 * scale)))
    text_shadow(d, (x + int(6 * scale), y + int(212 * scale)), "— does it?", t2, RED, anchor, offset=max(2, int(4 * scale)))
    if tagline:
        text_shadow(d, (x + int(6 * scale), y + int(300 * scale)),
                    "A first-person photo-horror demo  ·  2 chapters  ·  Made in Unity", t3, GREY, anchor, offset=2)


def key_art(stills, out):
    base = Image.open(os.path.join(stills, "10_ghost_viewfinder.png")).convert("RGB")

    # 1920×1080 hero / YouTube thumbnail / itch banner
    hero = darken(base, 1.15)
    black, mask = vignette_gradient(hero.size, bottom=0.95, left=0.85, top=0.0)
    hero = Image.composite(black, hero, mask)
    title_block(hero, 1.0, 110, 560)
    hero.save(os.path.join(out, "hero_1920x1080.png"))

    # 1200×627 LinkedIn / Open Graph
    li = cover_crop(base, 1200, 627, 0.52, 0.47)
    li = darken(li, 1.15)
    black, mask = vignette_gradient(li.size, bottom=0.95, left=1.0, top=0.0)
    li = Image.composite(black, li, mask)
    black, mask = vignette_gradient(li.size, bottom=0.0, left=0.75, top=0.0)  # second pass: deeper left side
    li = Image.composite(black, li, mask)
    title_block(li, 0.56, 60, 345)
    li.save(os.path.join(out, "linkedin_1200x627.png"))

    # 630×500 itch.io cover — cropped inside the HUD so no readouts poke in at the edges
    cov = base.crop((500, 110, 1380, 810)).resize((630, 500), Image.LANCZOS)
    cov = darken(cov, 1.2)
    black, mask = vignette_gradient(cov.size, bottom=1.0, left=0.0, top=0.0)
    cov = Image.composite(black, cov, mask)
    d = ImageDraw.Draw(cov)
    t1 = font("georgiab.ttf", 50)
    t2 = font("georgiai.ttf", 30)
    text_shadow(d, (315, 352), "THE CAMERA", t1, CREAM, "mm", offset=3)
    text_shadow(d, (315, 405), "NEVER LIES", t1, CREAM, "mm", offset=3)
    text_shadow(d, (315, 456), "— does it?", t2, RED, "mm", offset=3)
    cov.save(os.path.join(out, "itch_cover_630x500.png"))

    # 1080×1080 square (Instagram / LinkedIn feed)
    sq = base.crop((490, 30, 1430, 970)).resize((1080, 1080), Image.LANCZOS)
    sq = darken(sq, 1.15)
    black, mask = vignette_gradient(sq.size, bottom=1.0, left=0.0, top=0.0)
    sq = Image.composite(black, sq, mask)
    d = ImageDraw.Draw(sq)
    text_shadow(d, (540, 770), "THE CAMERA", font("georgiab.ttf", 92), CREAM, "mm", offset=4)
    text_shadow(d, (540, 865), "NEVER LIES", font("georgiab.ttf", 92), CREAM, "mm", offset=4)
    text_shadow(d, (540, 950), "— does it?", font("georgiai.ttf", 52), RED, "mm", offset=3)
    text_shadow(d, (540, 1025), "Photo-horror demo  ·  Made in Unity", font("segoeui.ttf", 30), GREY, "mm", offset=2)
    sq.save(os.path.join(out, "square_1080x1080.png"))


def collage(stills, out):
    picks = [("02_eye_symbol", "The camera sees what the world forgets"),
             ("06_behind_you", "Some photographs change"),
             ("10_ghost_viewfinder", "Through the lens, it cannot move"),
             ("16_jumpscare", "Don't let it find you")]
    W, H, pad, top = 1920, 1080, 18, 150
    canvas = Image.new("RGB", (W, H), (10, 8, 8))
    d = ImageDraw.Draw(canvas)
    text_shadow(d, (W // 2, 62), "THE CAMERA NEVER LIES", font("georgiab.ttf", 64), CREAM, "mm", offset=3)
    text_shadow(d, (W // 2, 118), "— does it?", font("georgiai.ttf", 36), RED, "mm", offset=2)
    cw, ch = (W - pad * 3) // 2, (H - top - pad * 2) // 2
    cap = font("segoeuib.ttf", 30)
    for i, (name, caption) in enumerate(picks):
        im = cover_crop(Image.open(os.path.join(stills, name + ".png")).convert("RGB"), cw, ch, 0.5, 0.48)
        black, mask = vignette_gradient(im.size, bottom=0.9, left=0.0, top=0.0)
        im = Image.composite(black, im, mask)
        dd = ImageDraw.Draw(im)
        text_shadow(dd, (28, ch - 34), caption, cap, CREAM, "ls", offset=2)
        x = pad + (i % 2) * (cw + pad)
        y = top + (i // 2) * (ch + pad)
        canvas.paste(im, (x, y))
    canvas.save(os.path.join(out, "feature_collage_1920x1080.png"))


def main():
    stills, out = sys.argv[1], sys.argv[2]
    shots_dir = os.path.join(out, "Screenshots")
    art_dir = os.path.join(out, "KeyArt")
    os.makedirs(shots_dir, exist_ok=True)
    os.makedirs(art_dir, exist_ok=True)
    for src, name in SHOTS:
        Image.open(os.path.join(stills, src + ".png")).convert("RGB").save(os.path.join(shots_dir, name + ".png"), optimize=True)
    key_art(stills, art_dir)
    collage(stills, art_dir)
    print("done:", shots_dir, art_dir)


if __name__ == "__main__":
    main()
