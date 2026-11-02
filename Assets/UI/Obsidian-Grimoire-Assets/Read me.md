# Obsidian Grimoire — Fantasy UI and Icons
NimbeKit • Version 1.0 • 21 October 2026

Open the book of forbidden things. Amethyst accents, sharp silver trim and haunted relics for gothic RPGs, dungeon crawlers and occult adventures.

## Included
- 160 separately named illustrated icon designs. Resized copies are not counted as additional designs.
- Transparent individual PNG icons at 32×32, 64×64 and 128×128 pixels: 480 files total.
- Three uniform icon atlases with JSON frame coordinates: 512×320, 1024×640 and 2048×1280 pixels.
- 107 UI component/state files in both transparent PNG and editable SVG: 214 files. This number includes hover, pressed, disabled and selected variants.
- 24 different assembled UI layout examples at 1920×1080, supplied in PNG and editable SVG compositions.
- Named JSON and CSV icon catalogs, component dimensions and 9-slice margins, palette tokens, a searchable offline preview, and licensed DejaVu fonts.

## Start here
Unzip the download, then open Preview.html in a browser. Search the icons by name, browse the UI parts, and open full-size layout examples. No installation, account or network is required. The preview is a catalog, not a playable game.

For a game, import the individual PNG files you need. Icon art is raster artwork; the SVG files contain scalable UI geometry and text. Full layout SVGs embed the raster icons, so enlarging those icons does not create extra detail. At 32px the illustrations are best for small secondary inventory indicators; use 64px or 128px for detailed primary items.

## UI construction
Use blank component assets for your game UI. Layer text, icons and button logic in your engine. State files use exactly matching dimensions. Keep Normal, Hover, Pressed and Disabled at the same position. Component catalog.json supplies measured pixel dimensions and optional slice margins. Panels are suited to 9-slicing; do not shrink them below the sum of opposing margins. Decorative header crests are separate overlays.

SVG layout text stays editable. Install the supplied DejaVu fonts for matching text metrics when editing. Examples are screen composition references, not engine scenes, scripts, prefabs or a working UI framework. Menus, stats, currency, quest text and values in the examples are illustrative.

## Atlas layout
16 columns × 10 rows, no rotation, no trimming. IDs run left to right, top to bottom. Tile size is 32, 64 or 128 pixels. In the 128px icons the artwork fits within 112×112px, leaving at least 8px padding. Smaller exports preserve proportional padding. The atlas JSON uses pixel coordinates and includes image size and source-size fields. Each engine may need its own importer; no engine-specific atlas compatibility is claimed.

## Engine notes
Unity: import PNG UI artwork as Sprite (2D and UI). Use Full Rect geometry for 9-slicing, enter the component's border margins in the Sprite Editor, and use a sliced UI Image for stretchable panels. Use bilinear filtering for this illustrated artwork; avoid treating it as pixel art.
Godot: use the PNGs in TextureRect controls and NinePatchRect for panels. Copy the catalog's left/right/top/bottom values into patch margins. Keep state textures aligned. Use engine labels for runtime/localized text.
Other engines: the PNGs are standard RGBA files. Import a few sample files first and set size, filtering and alpha behavior to suit the project.

Official references:
https://docs.unity3d.com/Manual/9SliceSprites.html
https://docs.godotengine.org/en/stable/classes/class_ninepatchrect.html

## Usage rights
See License.txt. Commercial use is permitted under those terms. These assets are non-exclusive. Credits are optional for the NimbeKit assets; the bundled fonts retain their own license.

The exports were checked for file counts, dimensions, transparency, duplicate files, renderability and catalog alignment. No third-party pack artwork is included. No future update schedule is promised.
