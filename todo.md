# BovineLabs.Timeline.Animation — Editor Timeline Preview

## ✅ Completed: Animation Preview in Timeline Editor Window

### What Changed

**Goal:** When scrubbing the Timeline editor window (edit mode), see the character animate in real-time via Unity's native PlayableGraph. Previously, the tracks bound to `RigDefinitionAuthoring` which doesn't produce animation output in the editor.

### Changes Made

| File | Change |
|------|--------|
| `RukhankaAnimationTrack.cs` | `TrackBindingType` changed from `RigDefinitionAuthoring` → `Animator`. Added `CreateTrackMixer` returning `AnimationMixerPlayable` in edit mode. |
| `RukhankaAnimationClip.cs` | Added `CreatePlayable` returning `AnimationClipPlayable` in edit mode (with `SetApplyFootIK`). |
| `BlendTree2DTrack.cs` | `TrackBindingType` changed from `RigDefinitionAuthoring` → `Animator`. Added `CreateTrackMixer` returning `AnimationMixerPlayable` in edit mode. |
| `BlendTree2DClip.cs` | Added `CreatePlayable` returning empty `AnimationMixerPlayable` in edit mode (blend trees are DOTS-only, no per-clip preview). |
| `RukhankaAnimationTrackExtensions.cs` | `ResolveRigDefinition` now resolves from `Animator` binding → `GetComponent<RigDefinitionAuthoring>()`. |

### How It Works

1. **Edit mode (Timeline scrubbing):** Unity builds a `PlayableGraph` using the `Animator` binding. Each `RukhankaAnimationClip` creates an `AnimationClipPlayable` from its `animationClipHolder`. The track mixer blends them. Moving the playhead evaluates the graph → the Animator drives bones in real-time.

2. **Play mode (runtime):** The DOTS path runs unchanged — `DOTSClip.CreatePlayable` returns `Playable.Create(graph)` (base behavior). The `PlayableDirector` graph is not used for actual animation; Rukhanka's ECS systems drive everything.

3. **Baking:** `ResolveRigDefinition` resolves the `RigDefinitionAuthoring` from the `Animator` binding via `GetComponent<RigDefinitionAuthoring>()`. The bake path is identical to before.

### Known Limitations

- **Track offsets not previewed:** `AnimationOffsetPlayable` is `internal` in Unity's `UnityEngine.Animations` assembly. Track position/rotation offsets won't show in editor preview unless Unity exposes this API or we use the `InternalsVisibleTo` trick.
- **BlendTree2D clips are blank in preview:** Blend tree content is entirely DOTS-driven. The editor preview shows an empty mixer for these clips.
- **`SetRemoveStartOffset` is internal:** Not accessible from custom assemblies. The clip plays with its baked-in start offset in editor preview.
