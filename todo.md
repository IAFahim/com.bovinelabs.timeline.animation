This is a highly advanced integration you are building. You have successfully
bridged the data flow (authoring -> baking -> BovineLabs Timeline components ->
Rukhanka AnimationToProcessComponent), but playing animation smoothly in the
Unity Editor (Edit Mode / Scrubbing) introduces a whole new set of
synchronization and execution-order challenges.

Because Unity’s default PlayableGraph operates on GameObjects via the C++
engine, and Rukhanka operates purely on ECS data, they can easily fall out of
sync or fight each other. Furthermore, the ECS Editor World does not
continuously tick like the Play mode world does.

Here is a comprehensive, step-by-step TODO.md to perfectly execute Edit Mode
timeline scrubbing.

TODO: Rukhanka + BovineLabs Timeline Edit Mode Integration

Phase 1: Resolve PlayableGraph vs. ECS Conflicts (The "Dummy Playable" Problem)

Currently, in RukhankaAnimationTrack.cs and BlendTree2DTrack.cs, you are
creating an AnimationPlayableOutput targeting the GameObject's Animator to force
the Timeline window to preview the track. This causes Unity's built-in animation
engine to fight with Rukhanka's ECS deformation.

- [ ] Remove AnimationPlayableOutput Binding to the Animator. In
  RukhankaAnimationTrack.CreateTrackMixer() and
  BlendTree2DTrack.CreateTrackMixer(), do not bind the output to the Animator
  component. Fix: Create a generic ScriptPlayableOutput or simply leave the
  AnimationMixerPlayable unconnected to the GameObject's Animator. The Timeline
  UI will still draw the track and clips, but Unity won't try to move the
  underlying GameObject bones, allowing ECS to fully own the deformation.

- [ ] Simplify RukhankaAnimationClip.CreatePlayable(). Right now, you are
  returning an AnimationPlayableAsset.CreatePlayable(). While this works, it
  feeds Unity's internal animation data into the graph. Change this to return an
  empty Playable.Create(graph) if you strictly want ECS to drive the preview.
  (Only keep AnimationPlayableAsset if you absolutely rely on it for Timeline
  window root-motion previews).

Phase 2: Force ECS Editor World Ticking

In Edit Mode, the ECS World only ticks when the Scene is dirtied or repainted.
When you drag the Timeline playhead, the ECS systems will not automatically run,
meaning your meshes will freeze until you click the Scene view.

- [ ] Update AnimationPreviewUpdater.cs to explicitly tick the ECS Editor World.
  After you call director.playableGraph.Evaluate();, you must force the ECS
  world to update so that BovineLabs translates the new time, and Rukhanka
  processes the poses.
  // Inside AnimationPreviewUpdater.OnEditorUpdate()
  director.playableGraph.Evaluate();

  // Force ECS Editor World to tick
  foreach (var world in Unity.Entities.World.All)
  {
  if ((world.Flags & Unity.Entities.WorldFlags.Editor) == Unity.Entities.WorldFlags.Editor)
  {
  world.Update();
  break;
  }
  }

Phase 3: Fix EditorRukhankaBarrier System Registration

Your EditorRukhankaBarrier currently manually invokes AnimationProcessSystem and
AnimationApplicationSystem. However, Rukhanka relies on a strict order and
several other systems to function properly (e.g., Blob Databases and Culling).

- [ ] Inject BlobDatabaseUpdateSystem into the Editor World. When you tweak a
  clip on the timeline in Edit Mode, the Baker creates a
  NewBlobAssetDatabaseRecord. If BlobDatabaseUpdateSystem doesn't run in the
  Editor world, AnimDB.TryGetValue will fail and the animation will be missing
  during scrubbing. Fix: Ensure BlobDatabaseUpdateSystem is created and updated
  in your EditorPreviewBootstrap.

- [ ] Ensure DeformationSystemGroup runs in the Editor World. Rukhanka's
  MeshDeformationSystem updates in RukhankaDeformationSystemGroup, which already
  has WorldSystemFilterFlags.Editor. However, because you are manually ticking
  AnimationApplicationSystem via Reflection inside EditorRukhankaBarrier (which
  runs in TimelineComponentAnimationGroup), you might be causing a race
  condition. Fix: Instead of manually invoking UpdateSystemMethod.Invoke, let
  the standard ECS group sorting handle it. Ensure your
  TimelineComponentAnimationGroup updates before RukhankaAnimationSystemGroup,
  and let Rukhanka's native systems run naturally in the Editor World.

Phase 4: Fix Timeline Time Synchronization

EditorTimelineSystem.cs grabs the time from the TimelineWindow and pushes it to
a Timer component. This works, but has edge cases.

- [ ] Ensure the PlayableDirector is Baked Properly.
  EditorTimelineSystem.EnableSelected() uses
  EntityManager.Debug.GetEntitiesForAuthoringObject(director, entities). If the
  PlayableDirector is on a GameObject in the main scene (not in a SubScene), it
  will not have an Entity in the Editor World. Fix: You must either ensure the
  Director is inside a SubScene, OR modify EditorTimelineSystem to bypass the
  GetEntitiesForAuthoringObject lookup and inject the scrubbed time directly
  into a singleton that your ClipLocalTimeSystem reads.

- [ ] Validate DeltaTime during Scrubbing. In
  TimelineAnimationUnificationSystem.cs, you correctly identified that
  IsScrubbing should snap weights directly (s.CurrentWeight = s.TargetWeight).
  Ensure that DeltaTime is completely ignored during IsScrubbing for all
  time-accumulation logic, otherwise scrubbing backwards will cause negative
  time glitches in looped animations.

Phase 5: Handle Orphaned States on Deselection

When you deselect the Timeline window or stop scrubbing, the Timeline playhead
stops, and clips might become inactive.

- [ ] Clear AnimationToProcessComponent when the track goes inactive. If you
  drag the playhead completely off a clip into an empty space on the Timeline,
  BovineLabs.Timeline will disable ClipActive. If no clips are active,
  TimelineAnimationUnificationSystem might leave the last frame's
  AnimationToProcessComponent inside the buffer. Fix: In
  TimelineAnimationUnificationSystem, ensure that if smoothEntries and
  blendEntries are completely empty, you explicitly call atps.Clear(); so the
  character returns to its bind pose or default fallback idle.

Phase 6: Ensure GPU / Compute Buffer Lifecycle in Edit Mode

Rukhanka's Mesh Deformation uses Compute Shaders and GraphicsBuffers heavily. In
Edit mode, recompiling scripts can destroy GPU buffers while the ECS world is
still alive, leading to ComputeBuffer is invalid errors.

- [ ] Add a cleanup failsafe for script reloads. Ensure that when Unity triggers
  an Assembly Reload, Rukhanka's deformation buffers are safely disposed.
  (Rukhanka usually handles this, but forcing manual ECS ticks in
  AnimationPreviewUpdater during a reload can crash the GPU). Fix: Wrap your
  AnimationPreviewUpdater.OnEditorUpdate tick logic with a check: if
  (EditorApplication.isCompiling) return;

Summary of Execution Flow to Verify:

To verify you have achieved perfect edit-mode scrubbing, trace a single frame of
your Editor:

1.  EditorApplication.update fires.
2.  AnimationPreviewUpdater calls playableGraph.Evaluate() -> advances Unity
    time.
3.  AnimationPreviewUpdater forces EditorWorld.Update().
4.  EditorTimelineSystem reads TimelineWindow.time and writes to the Timer ECS
    component.
5.  BovineLabs TimerUpdateSystem and ClipLocalTimeSystem calculate the
    LocalTime.
6.  Your custom TimelineSingleAnimationTrackSystem writes to BlendGroupEntry
    buffers.
7.  Your TimelineAnimationUnificationSystem converts these to Rukhanka's
    AnimationToProcessComponent.
8.  Rukhanka's AnimationProcessSystem computes the local bone poses.
9.  Rukhanka's AnimationApplicationSystem resolves world space matrices.
10. Rukhanka's MeshDeformationSystem computes the skinning via Compute Shader.
11. The Scene View repaints, showing the accurately deformed mesh.
