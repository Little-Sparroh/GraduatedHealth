# GraduatedHealth

A BepInEx client mod for Mycopunk that adds graduation notches to health bars so HP is easier to read at a glance.

## Features

- **Player health bar**: vertical notches every **5 max HP**
- **Boss / abomination bars**: notches every **500 max HP** (configurable)
- **Floating enemy bars**: same enemy interval (optional)
- Notches track **max health** (capacity markers), not current fill
- Rebuilds when max HP or bar size changes
- Fully configurable intervals, color, thickness, height, and toggles

## Dependencies

- Mycopunk
- [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.2403+ (Mycopunk pack)

## Building

```bash
dotnet build --configuration Release
```

## Installing

Place `GraduatedHealth.dll` in `<Mycopunk>/BepInEx/plugins/`.

## Configuration

Config file: `BepInEx/config/sparroh.graduatedhealth.cfg`

| Section   | Key                     | Default    | Description                                               |
|-----------|-------------------------|------------|-----------------------------------------------------------|
| General   | Enable Player Notches   | true       | Add graduation notches to the local player health bar     |
| General   | Enable Boss Notches     | true       | Add graduation notches to boss / abomination health bars  |
| General   | Enable Enemy Notches    | true       | Add graduation notches to floating world-space enemy bars |
| Intervals | Player Health Per Notch | 5          | Player health bar: one notch every N max HP               |
| Intervals | Enemy Health Per Notch  | 500        | Enemy bars (boss + floating): one notch every N max HP    |
| Display   | Notch Thickness         | 2          | Width of each notch line in UI pixels                     |
| Display   | Notch Height Fraction   | 0.33       | Notch height as a fraction of the bar height (0–1)        |
| Display   | Notch Color             | `000000AA` | Notch color as hex RRGGBB or RRGGBBAA                     |

## Notes

- Player bar discovery uses reflection + HUD hierarchy search (Player / PlayerLook fields, then named health UI).
- Enemy boss max HP uses the same shell/core rules as the vanilla boss bar fill.
- Client-side only; safe for multiplayer.

## License

MIT
