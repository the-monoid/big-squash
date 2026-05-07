# Visual Rework — Phase 0 (manual asset acquisition)

This is the user-facing setup work for the "New World meets Valheim" overhaul.
Plan: `C:\Users\AJW\.claude\plans\plan-out-a-stategy-virtual-nebula.md`.

After Phase 1 (Cinemachine + VFX Graph packages, already pushed) lands in Unity,
do these one-time downloads in parallel with my Phase 2+ code work. Total ~45 min.

---

## 1. Mixamo character (5 min)

> Mixamo is Adobe's free rigged character + animation library. Sign-in is free.

1. Open <https://www.mixamo.com> in your browser. Sign in with an Adobe ID
   (free if you don't have one).
2. **Characters** tab → search **"X Bot"** → click X Bot → click **Download**.
3. Settings:
   - Format: **FBX for Unity (.fbx)**
   - Pose: **T-pose**
   - Skin: **With Skin**
   - Frames per Second: 30
4. Save the file as **`Player_VikingHero.fbx`**.
5. Drop into `Assets/_Project/Art/Models/Characters/Player/`.

The `SteadingFbxPostprocessor` will auto-detect the `Player_*` prefix and
configure it as a **Humanoid avatar** with the painterly material assigned.

---

## 2. Mixamo animations (~20 min)

With X Bot still loaded in Mixamo, search and download these animations. Each
takes ~30s.

**Settings for every animation download:**
- Format: **FBX for Unity (.fbx)**
- Skin: **Without Skin** (the character mesh is already in step 1's file)
- Frames per Second: 30
- Keyframe Reduction: none
- In Place: **Yes** (we apply movement in code, not via root motion)

| Mixamo search | Save as | Notes |
|---|---|---|
| `Idle` | `Player_Idle.fbx` | base loop |
| `Walking` | `Player_Walk.fbx` | forward walk loop |
| `Running` | `Player_Run.fbx` | forward run loop |
| `Jump` | `Player_Jump.fbx` | jump arc |
| `Standing Death Forward 01` | `Player_Death.fbx` | one-shot, no loop |
| `Standing React Small Gut` | `Player_HitReact.fbx` | additive flinch |
| `Sword And Shield Slash` | `Player_SwordSlash.fbx` | combo step 1 |
| `Sword And Shield Slash Combo` | `Player_SwordCombo.fbx` | combo step 2 |
| `Shield Bash` | `Player_ShieldBash.fbx` | base for both Shield Rush + Power Bash |

Drop all 9 into `Assets/_Project/Art/Models/Characters/Player/`.

---

## 3. Synty POLYGON Starter Pack (5 min)

Free Unity-ready stylized props/buildings for environment dressing.

1. Open <https://assetstore.unity.com/packages/3d/props/polygon-starter-pack-low-poly-3d-art-by-synty-156819>
   (or search "POLYGON Starter Pack" by Synty Studios in the Unity Asset Store
   — it's free).
2. Click **Add to My Assets**.
3. In Unity: **Window → Package Manager → My Assets** → Synty's pack → **Download → Import**.
4. Default import path: `Assets/PolygonStarter/`. Keep that path.

---

## 4. Mixamo Draugr (10 min)

Same flow as steps 1–2 but for the enemy.

1. On Mixamo, **Characters** → search **"Mutant"** → click → **Download**.
   Format FBX for Unity, T-pose, **With Skin**.
2. Save as **`Enemy_Draugr.fbx`**.
3. Animations (each Without Skin, In Place):

| Mixamo search | Save as |
|---|---|
| `Mutant Idle` | `Enemy_Idle.fbx` |
| `Mutant Walking` | `Enemy_Walk.fbx` |
| `Mutant Run` | `Enemy_Run.fbx` |
| `Mutant Punch` | `Enemy_Attack.fbx` |
| `Mutant Hit Reaction` | `Enemy_HitReact.fbx` |
| `Mutant Dying` | `Enemy_Death.fbx` |

Drop all 7 into `Assets/_Project/Art/Models/Characters/Enemies/`.

---

## 5. After everything is in (~5 min Unity work)

1. Open Unity. Wait for the asset import to finish (the postprocessor configures
   each FBX automatically).
2. **Window → Package Manager** → confirm **Cinemachine** and **Visual Effect
   Graph** are listed under "In Project" (they should appear after my push lands).
3. Open `Assets/_Project/Art/Models/Characters/Player/Player_VikingHero.fbx`
   in the Inspector → **Rig** tab → confirm:
   - Animation Type: **Humanoid**
   - Avatar Definition: **Create From This Model**
   - Optimize Game Objects: unchecked (we need access to the bones)
4. Click **Apply** if anything was changed.

Then ping me. Phase 2 is ~3 hours of code I'll layer on top of your imports:
the Animator state machine, the Animator Bridge, and the Cinemachine third-
person rig. Phase 3 is the Shield Rush + Charged Power Bash on top of that.

---

## Troubleshooting

- **"Player_VikingHero.fbx imports as Generic, not Humanoid"** — The
  postprocessor only triggers on imports under `Assets/_Project/Art/Models/`.
  If you put the file elsewhere by accident, move it and let Unity re-import.
- **"Animations don't play on the X Bot character"** — Each animation FBX
  needs its **Rig** set to **Copy from another avatar** and pointed at
  `Player_VikingHero.fbx`'s avatar. Phase 2 of my code will set this
  automatically, but if you want to verify before then: select
  `Player_Idle.fbx` → Rig → Copy from `Player_VikingHero.fbx → Avatar`.
- **"Cinemachine missing types in the Console"** — Mirror's Edgegap module
  pulled in `com.unity.nuget.newtonsoft-json` already, which Cinemachine 3.1
  also wants. If you see version conflicts, delete your `Library/PackageCache`
  and let Unity re-resolve.
