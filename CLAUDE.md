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
- `PlayerMotor`: camera-relative movement, gaits, knockback impulse, `locked` flag (stun/dead/not fighting), faces the opponent while attacking/blocking or locked on; otherwise faces its move direction and keeps that facing when it stops. Moves at 70% speed while attacking. Drives `MoveX`/`MoveZ` (local move dir * gait) for the 8-direction `Locomotion` blend tree (Kevin Walk/Run/Sprint direction clips), so strafing while facing the opponent animates correctly.
- `PlayerCombat`: `Attack()` / `SetBlock(bool)` commands, combo queue (attacks always finish), cone hit scan at `hitStart`, directional block (`blockAngle`), flinch only when not mid-swing, death. Static `All` list. Animator layer 1 `UpperBody` (Kevin `Human Body Upper Mask`) holds `Empty` + `Attack0..N`: attacks always play there; while moving the base layer stays in `Locomotion` (legs run), when standing still the base layer plays the attack full-body too. Base layer states: `Locomotion`, `Attack0..N`, `Block`, `BlockHit`, `BlockImpact`, `Hit`, `HitHeavy`, `Stunned`, `KnockedDown`, `GettingUp`, `Victory`, `Death`, `Dodge`, `DodgeBack`, `DodgeRight`, `DodgeLeft`. Rules: hits >= 14 dmg use `HitHeavy`; the last hit of a combo (kit damage >= 12, i.e. not Dagger) is a heavy stagger (`HitHeavy` + 1.6x knockback); only the special attack knocks down (invulnerable until up); blocked damage fills a guard meter, 45 = guard break (`Stunned`); blocked hits >= 20 dmg play `BlockImpact`; the round winner plays `Victory` (`Celebrate()`). Hit feel: hit-stop (animator freeze), camera shake, spark particles (`HitFeedback`), white flash (`PlayerTint.Flash`), lunge on swing, slow-mo on the killing blow. `Dodge(moveInput)`: dash impulse in the input direction (or away from the opponent), i-frames, cooldown; cannot start mid-attack.
- `Health`, `KitSwitcher` (`Select`/`Cycle`), `PlayerTint` (P1 blue, P2 red), `FightCamera` (shared; yaw follows the P1->P2 axis so P1 is always left and P2 right; zooms with distance; `Shake()`).
- Stamina + special: `PlayerCombat.SpecialAttack()` costs 60 of 100 stamina (regen 14/s). `SpecialCharge` state for 0.9 s (golden pulsing `PlayerTint.SetGlow`; a clean hit in this window cancels it and the stamina is lost), then `SpecialAttack` (Mixamo `Mixamo@SpecialCharge.fbx` = Great Sword Power Up, `Mixamo@SpecialAttack.fbx` = Standing Melee Attack Downward at 2.2x): 18 + kit damage, kit range + 0.8, 170° arc, knockdown, a blocked special breaks the guard. `specialHitTime`/`specialRecoverTime` are matched to `SpecialAttackSpeed` in `KitSceneBuilder`.
- Lock-on: `PlayerMotor.ToggleLockOn()` (lock key). While locked the fighter always faces the opponent and strafes/backpedals with the 8-direction blend tree; sprint only applies when moving toward the opponent. The HUD shows a `KİLİTLİ` badge on the panel and a gold frame on the target's world tag. Kept across rounds.
- Dash attack: kits with `KitData.dashAttack` (Dagger) can press attack during a dodge to cancel it into the first swing, keeping the dash momentum. Dagger also moves at 1.35x.
- `GameManager`: Lobby (join + kit select) -> Countdown -> Fight -> RoundOver -> MatchOver (first to 2 wins). Exposes read-only state (`CurrentPhase`, `Combat(i)`, `Wins(i)`, ...) for the HUD.
- HUD (`Assets/Scripts/UI/HudView.cs`, `Assets/UI/`): UI Toolkit (`Layouts/Hud.uxml`, `Styles/Theme.uss` palette variables, `Styles/Hud.uss`), `Settings/KitFightPanel.asset` (scale with screen, 1920x1080). Read-only, polls state in `LateUpdate`: health bar with a damage trail, stamina bar (cost mark, glows when the special is ready), guard bar, win pips, P1/P2 world tags, lobby kit cards with stat bars, round/countdown banners.
- Editor: `Assets/Editor/KitSceneBuilder.cs`, menu **KitFight > Build Test Scene** regenerates controllers, KitData and `Assets/Scenes/KitTest.unity`.

## Arena (`Assets/Scripts/Arena/`, `Assets/Editor/ArenaBuilder.cs`)

- Low-poly assets made in Blender by the asset-agent: FBX in `Assets/Models/Arena/` (`SM_ArenaWall_01`, `SM_Pillar_01`, `SM_Crate_01`, `SM_Barrel_01`, `SM_Vase_01` + `_Broken` versions with one child per piece, `SM_SpikeTrap_Base_01`, `SM_SpikeTrap_Spikes_01`); source `ArtSource/Arena.blend`. Flat-colour URP Lit materials embedded in the FBX. Use Box/Capsule colliders, never MeshCollider (meshes are overlapping closed blocks).
- `ArenaBuilder.Build(spawnPositions)` (called from `KitSceneBuilder`) places wall/pillar visuals along the ±22 m edge (the invisible `Walls` colliders stay the real boundary), 13 breakables and 4 spike traps at (±8,0,±8). Falls back to primitives if an FBX is missing.
- `Breakable`: the attack cone in `PlayerCombat.HitScan` also calls `Break()`; pieces get rigidbodies, ignore player collisions and fade out. No damage, no hit-stop. Props with `healAmount > 0` (the vases and the two mid-field props, 25 HP) drop a `HealthPickup` (`SM_HealthPotion_01`, bobbing, green light): the first hurt fighter that walks over it is healed. `GameManager.ResetFighters()` resets props and clears potions every round.
- Decor (`ArenaBuilder.BuildDecor`, placed only when the FBX exists): `SM_Brazier_01` (with a fire light), `SM_WeaponRack_01`, `SM_Banner_01`, `SM_Rock_01/02`, `SM_Statue_01`; source `ArtSource/ArenaProps.blend`.
- `SpikeTrap`: Idle (3-5 s) -> Warning (0.6 s, tips peek out) -> Strike (one unblockable hit per player, 10 dmg, via `ReceiveHit(..., unblockable: true)`) -> Hold -> Retract. Runs only while `GameManager.IsFighting`. Dodge/knockdown i-frames still apply.
- The camera hides wall/pillar renderers that block the view of a fighter.
- Main-checkout writes from a worktree session are blocked by a hook; make changes in the worktree, commit, and the owner merges into `main` (the Unity Editor has the main checkout open).

## Multiplayer rule

Keep input separate from game logic. Everything that changes state goes through `PlayerCombat` / `PlayerMotor` / `KitSwitcher` commands so they can later be sent to / validated by a server. Do not put rules in `PlayerInput` / `InputSource`.

## Controls (scene `Assets/Scenes/KitTest.unity`)

Lobby: press attack on a device to join, left/right = pick kit, attack = ready, block = unready/leave. Fight: move, attack (click during a swing queues the next one), hold block (all kits can block/parry; stand still while blocking), dodge (dash + i-frames; clip chosen by direction relative to the opponent: back/right/left(mirrored) = Mixamo `Assets/Mixamo/Mixamo@Dodge{Back,Right}.fbx`, forward = Kevin Iglesias `Dodge01`). Mixamo FBX in `Assets/Mixamo/` are auto-imported as Humanoid with root height/rotation baked into the pose (`MixamoImportSettings`); downloaded from mixamo.com as FBX Binary, Without Skin, 30 fps.
- Keyboard 1: WASD, Shift sprint, J/Space attack, K block, L dodge, I special, Q lock-on.
- Keyboard 2: arrows, R-Shift/Numpad0 sprint, `,`/Numpad1 attack, `.`/Numpad2 block, `/`/Numpad3 dodge, M/Numpad4 special, N/Numpad5 lock-on.
- Gamepad: left stick/dpad, RB/L3 sprint, A/RT attack, LT/LB/B block, Y dodge, X special, R3 lock-on.

## Art rules (asset-agent, ui-agent)

- Low-poly, flat colour, one material per prop, 1 unit = 1 m, pivot at the base centre, FBX (Apply Transforms) in `Assets/Models/Arena/`, name `SM_Name_01`.
- Palette (sRGB). UI uses these as USS variables in `Assets/UI/Styles/Theme.uss`:
  - ink `#0E0B10`, panel `#120E14` @ 82%, gold `#D6AC5C`, gold dim `#786034`, text `#F4ECDC`, muted `#A09688`
  - P1 blue `#5A9BFF`, P2 red `#FF665A`, health `#D83A34`, stamina `#F2C14E`, guard `#FF963C`, heal green `#4CFF7A`
  - world: stone `#8C8780`, dark stone `#66615C`, wood `#8C5C33`, dark wood `#613D21`, clay `#BF734D`, iron `#464448`, banner red `#96201E`

Animation speeds: attack speed lives in `KitSpec.attackSpeed`; reaction/dodge state speeds are set in `KitSceneBuilder` and must stay matched to the timers in `PlayerCombat` (stun, knockdown, guard break, dodge).

## Working with Unity from Claude

- The Unity MCP tools (`Unity_RunCommand`, `Unity_GetConsoleLogs`, ...) drive the open Editor. After script edits the Editor recompiles and the MCP connection drops for ~30 s ("Unity not detected"); wait and retry.
- `Unity_RunCommand` code needs `internal class CommandScript : IRunCommand`.
- `Unity_Camera_Capture` fails unless a Scene View window is open.
- When the Editor is unfocused the Play loop barely ticks; advance it with `EditorApplication.isPaused = true` + `EditorApplication.Step()` in a loop (with `Time.captureDeltaTime = 1/30`).
- Play-mode tests via `Unity_RunCommand` are flaky (a RunCommand compile right after `EnterPlaymode` can abort Play). Reliable way: a temporary `[RuntimeInitializeOnLoadMethod]` self-test that writes a result file in `Temp/`, then read the file.
- `Library`, `Temp`, `obj`, `Logs`, `UserSettings`, `*.csproj`, `*.sln` are gitignored. The nested `KitFight/` folder (a stray clone with its own `.git`) is also ignored.
- No Git LFS yet; `Assets/PixPlays` is ~110 MB. Consider LFS before adding more large binaries.
- Installed skill: `unity-animation` (`~/.agents/skills`) for Animator work; takes effect in a new session.

## Status

Done: 2-player lobby/match flow, hit/block/death, 5 kits, shared camera, gamepad input, arena with breakables and spike traps (trap damage, unblockable, break/reset verified in Play mode; the trap's timed cycle in a real match was not). Hit, block and kill verified in Play mode through code; gamepad and the IMGUI HUD were not tested by hand (no controller attached in the test session).

Also done: stamina + charged special (hit, cancel-on-hit, guard crush, stamina gate verified in Play mode through code), dagger dash attack (verified), health potions (drop verified; the walk-over heal needs `GameManager.IsFighting` and was not exercised by the code test), UI Toolkit HUD (lobby + fight layouts checked from Game view screenshots), Blender decor props. Keyboard input could not be simulated from the MCP (Input System ignores queued events while the Editor is unfocused), so a full match was not played by hand.

Next: hand-tune per-kit `hitStart`/range/damage, special balance, strafe/lock-on animations, hit VFX (PixPlays), pause/settings menus, online multiplayer (see the `unity:setup-multiplayer-services` skill).
