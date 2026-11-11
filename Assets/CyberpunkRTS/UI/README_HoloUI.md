# Chamfered Holo UI kit (Neon Swarm, option A)

Sprites: `Assets/CyberpunkRTS/UI/Resources/HoloUI/` - load with `HoloUI.Get("Name")` or `Resources.Load<Sprite>("HoloUI/Name")`.
All are 2x (PPU 200): on a Canvas Scaler with Reference Pixels Per Unit 100 and reference 1920x1080 they show at 1x. Image Type = Sliced (Tiled for Card_LockedStripes).
`glow margin` = transparent/glow pixels (1x) outside the visible edge; make the RectTransform that much bigger, or accept the inset.

Palette: cyan #00E5D4, pink #FF2E97, yellow #FFD400, green #8CFF1A, text #EDEFF7, muted #9AA3B8, dark text on bright fills #04121A. Suggested font: Oxanium (Google Fonts, OFL).

| Sprite | Size (2x px) | 9-slice border L,B,R,T | Glow margin | Use |
|---|---|---|---|---|
| Bar_BG | 16x16 | 4,4,4,4 | - | HP / power / progress bar background |
| Bar_Fill_White | 16x16 | 2,2,2,2 | - | Slider fill / HP / power / research bars (tint) |
| Btn_Danger | 128x104 | 41,41,41,41 | 6 | Danger normal (Quit, Delete, Sign out, Leave room, Repair) |
| Btn_Danger_Hover | 128x104 | 41,41,41,41 | 6 | Danger highlighted |
| Btn_Disabled | 128x104 | 41,41,41,41 | 6 | Button disabled |
| Btn_Gold | 128x104 | 41,41,41,41 | 6 | Upgrade / primary call to action (Start Game) |
| Btn_Gold_Hover | 128x104 | 41,41,41,41 | 6 | Gold highlighted |
| Btn_Hover | 128x104 | 41,41,41,41 | 6 | Button highlighted |
| Btn_Normal | 128x104 | 41,41,41,41 | 6 | Button normal |
| Btn_Pressed | 128x104 | 41,41,41,41 | 6 | Button pressed / selected tab (use dark text) |
| Btn_Small | 80x72 | 29,29,29,29 | 4 | Small toggles (NO TEAM, NORMAL, ON/OFF, COPY, ADD) |
| Btn_Small_Hover | 80x72 | 29,29,29,29 | 4 | Small toggle highlighted |
| Btn_Small_On | 80x72 | 29,29,29,29 | 4 | Small toggle ON / pressed |
| Card_Building | 128x144 | 41,41,41,41 | 6 | Build card while in production (put Card_ProgressFill above it) |
| Card_Empty | 128x144 | 41,41,41,41 | 6 | Empty queue slot |
| Card_Hover | 128x144 | 41,41,41,41 | 6 | Build card hover |
| Card_Locked | 128x144 | 41,41,41,41 | 6 | Build card locked (add Card_LockedStripes on top) |
| Card_LockedStripes | 24x24 | 0,0,0,0 | - | Image Type = Tiled over a locked card |
| Card_Normal | 128x144 | 41,41,41,41 | 6 | Build card normal |
| Card_ProgressFill | 32x32 | 2,2,2,8 | - | Production progress: Image Sliced, anchored to card bottom, height = progress; tint cyan |
| Card_Selected | 128x144 | 43,43,43,43 | 6 | Build card selected / rally / hotkey active |
| Chip_Cyan | 112x96 | 41,41,41,41 | 6 | Timer chip, small HUD readouts |
| Chip_Green | 112x96 | 41,41,41,41 | 6 | Power / credits chips |
| Chip_Pink | 112x96 | 41,41,41,41 | 6 | Alerts / enemy counters |
| Deco_CornerBracket | 64x64 | 0,0,0,0 | - | Top-left L bracket, tintable; rotate 90/180/270 for other corners |
| Deco_Divider | 8x2 | 2,0,2,0 | - | 1px divider / title rule, tint |
| Deco_HeaderTick | 12x44 | 0,0,0,0 | - | Small solid bar left of a panel title (6x22 display), tint pink/cyan |
| Fill_White | 96x96 | 32,32,32,32 | 2 | Tintable solid chamfer (Image.color) for custom fills / progress overlays |
| Input_Field | 96x88 | 8,8,8,8 | - | InputField background |
| Input_Field_Focus | 96x88 | 8,8,8,8 | - | InputField background (selected / focused) |
| Outline_White | 96x96 | 41,41,41,41 | 6 | Tintable outline only (Image.color), for any extra accent colour |
| Panel_BuildBar | 200x200 | 65,65,65,65 | 8 | Bottom build / production bar background |
| Panel_Frame | 192x192 | 61,61,61,61 | 8 | Generic window / selection panel / player list / chat box |
| Panel_Frame_Pink | 192x192 | 61,61,61,61 | 8 | Secondary frame (lobby outer frame, warnings) |
| Panel_Inset | 128x128 | 29,29,29,29 | 2 | Dark inset area inside a panel (list backgrounds, stat boxes, slots) |
| Panel_Minimap | 192x192 | 65,65,65,65 | 10 | Minimap frame (also lobby map preview) |
| Panel_Modal | 256x256 | 89,89,89,89 | 12 | Menus, sign-in, settings, load game, friends, account, lobby windows |
| Slider_Handle | 40x40 | 0,0,0,0 | - | Slider handle, Simple, 20x20 |
| Slider_Track | 16x8 | 2,2,2,2 | - | Slider background (height 4-6) |
| Toast_Cyan | 192x96 | 48,47,48,47 | - | Notification toast; keep height 48 (stretches horizontally only) |
| Toast_Green | 192x96 | 48,47,48,47 | - | Notification toast; keep height 48 (stretches horizontally only) |
| Toast_Pink | 192x96 | 48,47,48,47 | - | Notification toast; keep height 48 (stretches horizontally only) |
| Tooltip | 128x112 | 45,45,45,45 | 6 | Tooltips / info popups / hover cards |
