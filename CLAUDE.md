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

## Architecture (`Assets/Scripts/Combat/`)

- `KitDefinition` (ScriptableObject): identity, stats, light combo, heavy, ability, block/dodge values, optional `modelPrefab`. Kit assets live in `Assets/Kits/` (Knight, Brute, Rogue). New kit = Create > KitFight > Kit, no code.
- `AttackDefinition`: serializable timings (windup/active/recovery), range, arc, damage, knockback, stun, stamina cost, lunge, unblockable, `animTrigger`.
- `CombatController`: input-agnostic state machine (Idle, Attacking, Blocking, Dodging, Stunned, Dead; attack phases Windup/Active/Recovery). Command API: `Light()`, `Heavy()`, `UseAbility()`, `Dodge(dir)`, `SetBlock(bool)`. `ReceiveHit(DamageInfo)` returns a `HitResult`. `ApplyKit(kit)` swaps kits at runtime. Hits are cone scans (OverlapSphere + arc) during the Active phase.
- `PlayerController`: local input only (Input System actions built in code) -> combat commands. Contains no combat rules.
- `SimpleEnemyAI`: test dummy that drives the same `CombatController` commands as the player.
- `CharacterMotor`: CharacterController movement, gravity, dash, knockback.
- `Health` (with `team`; static `All` list), `DamageInfo`, `ThirdPersonCamera`, `CombatHUD` (throwaway IMGUI), `PlaceholderWeapon` (swinging cube, remove once real animations exist).
- Animator triggers used by `CombatController`: `Dodge`, `Hit`, `Die`, plus each attack's `animTrigger`. Triggers missing from the controller are skipped silently.

## Multiplayer rule

Keep input separate from combat logic. Everything that changes combat state goes through `CombatController` commands so they can later be sent to / validated by a server. Do not put combat rules in `PlayerController`.

## Controls (test scene `Assets/Scenes/SampleScene.unity`)

WASD move, LMB light, E heavy, RMB block, Space dodge, Q ability, 1/2/3 swap kit, Esc frees the cursor. Gamepad bindings exist but are untested.

## Working with Unity from Claude

- The Unity MCP tools (`Unity_RunCommand`, `Unity_GetConsoleLogs`, ...) drive the open Editor. After script edits the Editor recompiles and the MCP connection drops for ~30 s ("Unity not detected"); wait and retry.
- `Unity_RunCommand` code needs `internal class CommandScript : IRunCommand`.
- `Unity_Camera_Capture` fails unless a Scene View window is open.
- `Library`, `Temp`, `obj`, `Logs`, `UserSettings`, `*.csproj`, `*.sln` are gitignored. The nested `KitFight/` folder (a stray clone with its own `.git`) is also ignored.
- No Git LFS yet; `Assets/PixPlays` is ~110 MB. Consider LFS before adding more large binaries.
- Installed skill: `unity-animation` (`~/.agents/skills`) for Animator work; takes effect in a new session.

## Status

Done: combat core, 3 kits, test arena with 3 AI enemies, HUD. Verified in Play mode: no compile errors, AI engages, attacks trigger. Not hand-played yet.

Next: wire PixPlays animations/models into kits (Animator), hook VFX to abilities, lock-on, kit select menu, then multiplayer (see the `unity:setup-multiplayer-services` skill).
