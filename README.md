# U3D-ARPG-demo

Unity project repository for `U3D-ARPG-demo`.
This demo includes one Boss fignt, one enemy soldier and a set of combat systems.


## Environment

- Unity version: `2022.3.62f1c1`
- Repository includes the standard Unity project folders:
  - `Assets/`
  - `Packages/`
  - `ProjectSettings/`

## Scenes

Current scenes in `Assets/Scenes/`:

- `Level1`
- `LoginScene`
- `MainMenu`
- `SampleScene`
- `SettingPage`
- `TankTest`

## How to play

Use WASD to move and mouse to attack and defend. 
Multi click on mouse will lead to combo attacks.
Wait to be detailed.

## Packages

This project currently uses Unity packages including:

- `com.unity.ai.navigation`
- `com.unity.feature.characters-animation`
- `com.unity.shadergraph`
- `com.unity.textmeshpro`
- `com.unity.timeline`
- `com.unity.ugui`
- `com.unity.visualscripting`

See `Packages/manifest.json` for the full package list.

## Getting Started

1. Open Unity Hub.
2. Add this repository folder as an existing project.
3. Open it with Unity `2022.3.62f1c1`.
4. Let Unity reimport packages and regenerate local cache folders such as `Library/`.

## Git Notes

- Large generated folders such as `Library/`, `Temp/`, `Logs/`, and `obj/` are ignored.
- Local build output under `tempScenes/` is ignored.
- `.vsconfig` is included to help restore the Visual Studio workload setup for this project.

## TODO

- Document the recommended entry scene.
- Add build and run instructions once the workflow is finalized.
