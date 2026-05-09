# BovineLabs.Timeline.Animation — Full Audit
> Bugs · Missing Features · TODOs · Code Quality

---

## ✅ Completed Fixes

### 1. Scale Corruption on First Frame (No Timeline Targeting)
**Severity: Critical** ✅ Fixed  
**Files:** `TimelineAnimationUnificationSystem.cs`, `TimelineAnimationStateAuthoring.cs`  
**Fix:** Fallback path now reads `PositionOffset`, `RotationOffset`, `RemoveStartOffset`, `ApplyFootIK` from `FallbackBlend`. Authoring default changed to `removeStartOffset = false` when no offsets are authored, preventing the character from dropping to origin.

### 2. BlendTree2D Fallback Does Not Propagate Offsets
**Severity: High** ✅ Fixed  
**File:** `TimelineAnimationBlendTree2DTrackSystem.cs`  
**Fix:** Both `TrackFallbackOverride` and `DefaultBlendGroupFallback` paths now propagate `PositionOffset`, `RotationOffset`, `RemoveStartOffset`, `ApplyFootIK`.

### 3. BlendTree2D BlendGroupEntry Missing Offsets
**Severity: High** ✅ Fixed  
**File:** `TimelineAnimationBlendTree2DTrackSystem.cs`  
**Fix:** `ProcessTrackMotions` now carries track-level `PositionOffset`/`RotationOffset` and conditionally sets `RemoveStartOffset` (only when offsets are non-zero).

### 4. BlendTree2D AvatarMask Never Applied at Runtime
**Severity: High** ✅ Fixed  
**File:** `TimelineAnimationBlendTree2DTrackSystem.cs`  
**Fix:** `ProcessTrackMotions` reads `trackData.ApplyAvatarMask` and conditionally passes `trackData.AvatarMaskHash`.

### 6. FollowPositionOnlySystem Redundant L2W Write
**Severity: Medium** ✅ Fixed  
**File:** `FollowPositionOnlySystem.cs`  
**Fix:** Removed direct `LocalToWorld` write. Only writes `LocalTransform.Position`. Changed to `[UpdateBefore(typeof(LocalToWorldSystem))]` so TransformSystemGroup derives L2W.

### 7. Playback State Orphan Accumulation (BlendTree2D)
**Severity: Medium** ✅ Fixed  
**File:** `TimelineAnimationBlendTree2DTrackSystem.cs`  
**Fix:** Added `CleanupOrphanPlaybackStates` (stack-based) and `CleanupOrphanPlaybackStatesHeap` (heap-based) methods. Called after track processing to remove stale `BlendTreePlaybackStateElement` entries.

### 11. RukhankaAnimationTrackInspector Commented Out
**Severity: Medium** ✅ Fixed  
**File:** `RukhankaAnimationTrackInspector.cs`  
**Fix:** Uncommented and restored the custom inspector with proper TrackOffset conditional rendering.

### 14. DefaultBlendGroupFallback Never Restores at Runtime
**Severity: Medium** ✅ Fixed  
**File:** `TimelineAnimationBlendTree2DTrackSystem.cs`  
**Fix:** Added `ResetStaleFallbackJob` (IJobEntity) that runs after the main job. For entities not processed by `DecomposeAndAppendBlendTreeJob`, resets `FallbackBlend` to `DefaultBlendGroupFallback` values including all offset fields.

### 15. FallbackPlaybackMode.Hold Pops to End Frame
**Severity: Medium** ✅ Fixed  
**File:** `TimelineAnimationUnificationSystem.cs`  
**Fix:** Hold mode now stops accumulating time once past clip end (no wrapping). Uses `math.min(accumulatedTime, 1f)` to freeze at the last evaluated frame instead of jumping to normalized time 1.0.

### 18. EditorPreviewBootstrap No Cleanup on Destroy
**Severity: Low** ✅ Fixed  
**File:** `EditorPreviewBootstrap.cs`  
**Fix:** Added `OnDestroy` that removes registered systems from `TimelineComponentAnimationGroup`. Uses `List<SystemHandle>` to track registrations. Prevents duplicate registrations on domain reload.

### 24. Tests Do Not Cover Offset Fields
**Severity: Medium** ✅ Fixed  
**File:** `AnimationDataTests.cs`  
**Fix:** All 5 struct test fixtures (`BlendGroupEntry`, `SmoothBlendGroupEntry`, `FallbackBlend`, `DefaultBlendGroupFallback`, `TrackFallbackOverride`) now test `PositionOffset`, `RotationOffset`, `RemoveStartOffset`, `ApplyFootIK` in both Default and SetCorrectly tests. **All 54 tests pass.**

---

## ⚠️ Known Remaining Items (Lower Priority / Design Decisions)

### 5. BlendGroupTimer Enable-State Never Reset
**Severity: Low**  
The `IEnableableComponent` on `BlendGroupTimer` is always enabled after baking. The enable bit adds complexity without current functional use. Could be integrated with a future timer optimization (TimerEnableable pattern). **Low priority — no functional bug.**

### 8. Blend-curve weight saturation loss
**Severity: Low**  
`math.saturate(blend.TotalWeight)` discards summed magnitude for overlapping blend-curve clips. This is a design choice — fully fixing it requires weighted normalization instead of saturation.

### 12. AnimationDebugState Not Per-Layer
**Severity: Low**  
Single aggregate debug state. Only matters for multi-layer debugging. Cosmetic, not functional.

### 13. No Missing-Clip Warning in Runtime
**Severity: Low**  
Silent no-op when clip hash is missing from `BlobDatabaseSingleton`. Would benefit from `#if UNITY_EDITOR` logging.

### 16. No Support for Additive Layer Tracks
**Severity: Medium**  
Both track types write `BlendMode = Override` unconditionally. Full additive layer support requires authoring toggle + runtime path. **Feature request, not a bug.**

### 17. Implicit AvatarMask=false Guard
**Severity: Low**  
Works correctly but the guard is implicit. Could add explicit comment or assert.

### 19. BlendTreePlaybackStateBuffer Added Unconditionally
**Severity: Low**  
`TimelineAnimationStateBuilder.ApplyTo` always adds `BlendTreePlaybackStateElement`, even for single-clip entities. Memory waste is minimal (InternalBufferCapacity=4).

### 20-23. Code Quality (Documentation, Magic Numbers, etc.)
**Severity: Low**  
Structural improvements that don't affect functionality. Can be addressed incrementally.
