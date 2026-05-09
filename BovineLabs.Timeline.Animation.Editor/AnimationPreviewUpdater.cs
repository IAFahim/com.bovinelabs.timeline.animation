#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Editor
{
    /// <summary>
    /// Hooks into AnimationMode to force-evaluate animation outputs created by
    /// RukhankaAnimationTrack and BlendTree2DTrack during Timeline editor preview.
    /// Timeline's own evaluation only processes outputs for built-in AnimationTrack.
    /// Our custom AnimationPlayableOutputs on DOTSTrack-derived tracks need manual evaluation.
    /// </summary>
    [InitializeOnLoad]
    internal static class AnimationPreviewUpdater
    {
        private static PlayableDirector s_Director;
        private static double s_LastTime = -1;

        static AnimationPreviewUpdater()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (Application.isPlaying) return;

            var director = Object.FindObjectOfType<PlayableDirector>();
            if (director == null) return;

            // Only act when the director time changes (Timeline scrubbing)
            var time = director.time;
            if (System.Math.Abs(time - s_LastTime) < 0.0001) return;
            s_LastTime = time;

            if (!director.playableGraph.IsValid()) return;

            var graph = director.playableGraph;

            // Evaluate the full graph — this processes ALL outputs including our custom ones
            graph.Evaluate();
        }
    }
}
#endif
