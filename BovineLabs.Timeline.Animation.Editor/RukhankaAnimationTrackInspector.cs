using BovineLabs.Timeline.Animation.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Editor
{
    [CustomEditor(typeof(RukhankaAnimationTrack))]
    [CanEditMultipleObjects]
    public class RukhankaAnimationTrackInspector : UnityEditor.Editor
    {
        private SerializedProperty m_ApplyAvatarMask;
        private SerializedProperty m_AvatarMask;
        private SerializedProperty m_EulerAnglesOffset;
        private SerializedProperty m_LayerIndex;
        private SerializedProperty m_PositionOffset;
        private SerializedProperty m_TrackOffset;

        private void OnEnable()
        {
            m_LayerIndex = serializedObject.FindProperty("LayerIndex");
            m_TrackOffset = serializedObject.FindProperty("trackOffset");
            m_PositionOffset = serializedObject.FindProperty("positionOffset");
            m_EulerAnglesOffset = serializedObject.FindProperty("eulerAnglesOffset");
            m_AvatarMask = serializedObject.FindProperty("avatarMask");
            m_ApplyAvatarMask = serializedObject.FindProperty("applyAvatarMask");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_LayerIndex);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Track Offsets", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_TrackOffset);

            // Only draw offset fields if mode is Transform Offsets
            if (m_TrackOffset.enumValueIndex == (int)TrackOffset.ApplyTransformOffsets)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_PositionOffset, new GUIContent("Position"));
                EditorGUILayout.PropertyField(m_EulerAnglesOffset, new GUIContent("Rotation"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_ApplyAvatarMask);
            if (m_ApplyAvatarMask.boolValue || m_ApplyAvatarMask.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_AvatarMask);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}