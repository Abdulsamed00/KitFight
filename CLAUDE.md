# KitFight

Unity 6.3 LTS (6000.3.26f1), URP, new Input System. Third-person 3D action combat game where each player picks a class "kit". Multiplayer comes last; combat is built and tuned single-player first.

- GitHub: https://github.com/Abdulsamed00/KitFight (branch `main`)
- Owner's language is Turkish; reply in Turkish.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Decisions made

- 3D, third-person action camera (over-the-shoulder orbit), class-based kits.
- Kit = class/character (own weapon, abilities, stats), chosen before the match.
- Roadmap: combat core with assets -> animations/VFX -> lock-on -> kit select menu -> multiplayer (last).
- The owner adds art assets themselves. `Assets/PixPlays` (Elemental AOE/Auras/Beams/Blast/Projectiles/Shields VFX, prefabs, FBX, anims) is already in the project.

## Architecture (`Assets/Scripts/Kit/`)

The old combat prototype (CombatController, AI, HUD, Knight/Brute/Rogue) was deleted. Current base is the Kevin Iglesias "Human Melee Animations" asset (`Assets/Kevin Iglesias`), male model `HumanM_Model.fbx`.

- `KitData` (ScriptableObject): name, weapon prefab, AnimatorController, combo clips, attack speed, move speed multiplier. Assets in `Assets/Kits/` (Sword, Dagger, Greatsword, Warhammer, Polearm) are generated.
- `PlayerMotor`: camera-relative CharacterController movement (Walk/Run/Sprint gaits), sets animator `Speed` (0 idle, 1 walk, 2 run, 3 sprint). No input code.
- `PlayerCombat`: command API `Attack()`; combo queue; plays animator states `Attack0..N` via CrossFade, returns to `Locomotion`. No input code.
- `KitSwitcher`: `Select(i)` swaps weapon (parented to bone `B-handProp.R`), controller and speed values.
- `PlayerInput`: local input only (Input System, polled) -> motor/combat/kit commands.
- `ThirdPersonCamera`, `KitHUD` (throwaway IMGUI).
- Editor: `Assets/Editor/KitSceneBuilder.cs`, menu **KitFight > Build Test Scene** regenerates controllers, KitData and `Assets/Scenes/KitTest.unity` (run it after changing the kit table).

## Multiplayer rule

Keep input separate from game logic. Everything that changes state goes through `PlayerCombat` / `PlayerMotor` / `KitSwitcher` commands so they can later be sent to / validated by a server. Do not put rules in `PlayerInput`.

## Controls (test scene `Assets/Scenes/KitTest.unity`)

WASD move (run), Shift sprint, Ctrl walk, LMB attack (hold clicks to chain combo), 1-5 swap kit (Sword, Dagger, Greatsword, Warhammer, Polearm), Esc frees the cursor.

## Working with Unity from Claude

- The Unity MCP tools (`Unity_RunCommand`, `Unity_GetConsoleLogs`, ...) drive the open Editor. After script edits the Editor recompiles and the MCP connection drops for ~30 s ("Unity not detected"); wait and retry.
- `Unity_RunCommand` code needs `internal class CommandScript : IRunCommand`.
- `Unity_Camera_Capture` fails unless a Scene View window is open.
- `Library`, `Temp`, `obj`, `Logs`, `UserSettings`, `*.csproj`, `*.sln` are gitignored. The nested `KitFight/` folder (a stray clone with its own `.git`) is also ignored.
- No Git LFS yet; `Assets/PixPlays` is ~110 MB. Consider LFS before adding more large binaries.
- Installed skill: `unity-animation` (`~/.agents/skills`) for Animator work; takes effect in a new session.

## Status

Done: Kevin Iglesias kit test scene (empty arena, 5 weapon kits, run/sprint/walk, combo attack, 1-5 switch). Compiles clean; kit switching, attack and movement verified in Play mode via code, not hand-played yet.

Next: tune animation/weapon offsets by hand-play, then shield/block, dodge, hit detection + dummy targets, lock-on, kit select menu, multiplayer (see the `unity:setup-multiplayer-services` skill).
