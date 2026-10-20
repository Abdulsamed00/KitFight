# KitFight

Unity 6.3 LTS (6000.3.26f1), URP, new Input System. 3D action combat game (2-player local duel) where each player picks a class "kit".

- GitHub: https://github.com/Abdulsamed00/KitFight (branch `main`)
- Owner's language is Turkish; reply in Turkish.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Decisions made

- Now a **2-player local duel on one screen** (shared camera, 3D arena, free movement). Online multiplayer comes later.
- Kit = weapon set + animator + combat numbers (`KitData`), chosen in the lobby before the match.
- Gamepad supported; any mix of devices works (devices are claimed in the lobby).
- The owner adds art assets themselves. `Assets/PixPlays` (VFX) is in the project but unused so far; `Assets/Kevin Iglesias` is the character/animation base.

## Architecture (`Assets/Scripts/Kit/`)

- `KitData` (ScriptableObject): weapons (main/off hand), AnimatorController, combo clips, block + block-hit clips, damage/range/arc/hitStart/knockback, attack/move speed. Assets in `Assets/Kits/` are generated.
- `InputSource`: one control scheme (Keyboard1 = WASD/J/K, Keyboard2 = arrows/,/. or numpad, or a Gamepad), polled once per frame. `PlayerInput` binds one source to a player and issues commands.
- `PlayerMotor`: camera-relative movement, gaits, knockback impulse, `locked` flag (stun/dead/not fighting), faces the opponent when idle/attacking/blocking.
- `PlayerCombat`: `Attack()` / `SetBlock(bool)` commands, combo queue (attacks always finish), cone hit scan at `hitStart`, directional block (`blockAngle`), flinch only when not mid-swing, death. Static `All` list. Animator states: `Locomotion`, `Attack0..N`, `Block`, `BlockHit`, `Hit`, `Death`.
- `Health`, `KitSwitcher` (`Select`/`Cycle`), `PlayerTint` (P1 blue, P2 red), `FightCamera` (shared, fixed yaw, zooms with distance).
- `GameManager`: Lobby (join + kit select) -> Countdown -> Fight -> RoundOver -> MatchOver (first to 2 wins); throwaway IMGUI HUD.
- Editor: `Assets/Editor/KitSceneBuilder.cs`, menu **KitFight > Build Test Scene** regenerates controllers, KitData and `Assets/Scenes/KitTest.unity`.

## Multiplayer rule

Keep input separate from game logic. Everything that changes state goes through `PlayerCombat` / `PlayerMotor` / `KitSwitcher` commands so they can later be sent to / validated by a server. Do not put rules in `PlayerInput` / `InputSource`.

## Controls (scene `Assets/Scenes/KitTest.unity`)

Lobby: press attack on a device to join, left/right = pick kit, attack = ready, block = unready/leave. Fight: move, attack (click during a swing queues the next one), hold block (all kits can block/parry; stand still while blocking).
- Keyboard 1: WASD, Shift sprint, J/Space attack, K block.
- Keyboard 2: arrows, R-Shift/Numpad0 sprint, `,`/Numpad1 attack, `.`/Numpad2 block.
- Gamepad: left stick/dpad, RB/L3 sprint, A/X/RT attack, LT/LB/B block.

## Working with Unity from Claude

- The Unity MCP tools (`Unity_RunCommand`, `Unity_GetConsoleLogs`, ...) drive the open Editor. After script edits the Editor recompiles and the MCP connection drops for ~30 s ("Unity not detected"); wait and retry.
- `Unity_RunCommand` code needs `internal class CommandScript : IRunCommand`.
- `Unity_Camera_Capture` fails unless a Scene View window is open.
- `Library`, `Temp`, `obj`, `Logs`, `UserSettings`, `*.csproj`, `*.sln` are gitignored. The nested `KitFight/` folder (a stray clone with its own `.git`) is also ignored.
- No Git LFS yet; `Assets/PixPlays` is ~110 MB. Consider LFS before adding more large binaries.
- Installed skill: `unity-animation` (`~/.agents/skills`) for Animator work; takes effect in a new session.

## Status

Done: 2-player lobby/match flow, hit/block/death, 5 kits, shared camera, gamepad input. Hit, block and kill verified in Play mode through code; gamepad and the IMGUI HUD were not tested by hand (no controller attached in the test session).

Next: hand-tune per-kit `hitStart`/range/damage, dodge, strafe/lock-on animations, hit VFX (PixPlays), proper UI, online multiplayer (see the `unity:setup-multiplayer-services` skill).
