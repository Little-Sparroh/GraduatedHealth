# GraduatedHealth

A BepInEx client mod for Mycopunk that adds graduation notches to health bars so HP is easier to read at a glance.

## Features

- **Player health bar**: vertical notches every **5 max HP**
- **Boss / abomination bars**: notches every **500 max HP** (configurable)
- **Floating enemy bars**: same enemy interval (optional)

- Notches track **max health** (capacity markers), not current fill
- Rebuilds when max HP or bar size changes
- Fully configurable intervals, color, thickness, and toggles

## Dependencies

* Mycopunk
* [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.2403+ (Mycopunk pack)

## Building

```bash
dotnet build --configuration Release
```

## Installing

Place `GraduatedHealth.dll` in `<Mycopunk>/BepInEx/plugins/`.

## Configuration

Config file: `BepInEx/config/sparroh.graduatedhealth.cfg`

| Key | Default | Description |
|---|---|---|
| `EnablePlayerNotches` | true | Player HUD notches |
| `EnableEnemyBossNotches` | true | Boss / abomination bar notches |
| `EnableFloatingEnemyNotches` | true | World-space floating bar notches |
| `PlayerHealthPerNotch` | 5 | Player interval (max HP); set to 10 etc. as preferred |
| `EnemyHealthPerNotch` | 500 | Enemy/boss/floating interval (max HP) |

| `NotchThickness` | 2 | Tick width in UI pixels |
| `NotchHeightFraction` | 1 | Tick height as fraction of bar height |
| `NotchColor` | `000000AA` | Hex RRGGBB or RRGGBBAA |

## Notes

- Player bar discovery uses reflection + HUD hierarchy search (Player / PlayerLook fields, then named health UI).
- Enemy boss max HP uses the same shell/core rules as the vanilla boss bar fill.
- Client-side only; safe for multiplayer.

## License

MIT
