using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ubco.ovilab.ViconUnityStream.Editor
{
    [CustomEditor(typeof(SubjectDataManager), true)]
    [CanEditMultipleObjects]
    public class SubjectDataManagerEditor: UnityEditor.Editor
    {
        private List<CustomSubjectScript> subjectScripts = new();
        private SubjectDataManager subjectDataManager;

        private SerializedProperty scriptProperty, totalFramesProperty, currentFrameProperty,
            playProperty, pathToDataFileProperty, jsonFilesToLoadProperty, enableWriteDataProperty,
            fileNameBaseProperty, baseURIProperty, streamTypeProperty;

        private bool showDuplicateWarning = false;
        private Texture2D playIcon, pauseIcon, prevIcon, nextIcon, ffwdIcon, frwdIcon;

        private void OnEnable()
        {
            subjectScripts.AddRange(FindObjectsByType<CustomSubjectScript>(FindObjectsSortMode.None));

            subjectDataManager = target as SubjectDataManager;

            showDuplicateWarning = subjectDataManager.JsonFilesToLoad.Count != subjectDataManager.JsonFilesToLoad.Distinct().Count();

            scriptProperty = serializedObject.FindProperty("m_Script");
            baseURIProperty = serializedObject.FindProperty("baseURI");
            streamTypeProperty = serializedObject.FindProperty("streamType");
            pathToDataFileProperty = serializedObject.FindProperty("pathToDataFile");
            currentFrameProperty = serializedObject.FindProperty("currentFrame");
            totalFramesProperty = serializedObject.FindProperty("totalFrames");
            jsonFilesToLoadProperty = serializedObject.FindProperty("jsonFilesToLoad");
            enableWriteDataProperty = serializedObject.FindProperty("enableWriteData");
            fileNameBaseProperty = serializedObject.FindProperty("fileNameBase");
            playProperty = serializedObject.FindProperty("play");

            string packagePath = "Packages/ubc.ok.ovilab.vicon-nexus-unity-stream/Assets/Scripts/ViconNexusUnityStream/Editor/Resources";
            playIcon = (Texture2D)AssetDatabase.LoadAssetAtPath($"{packagePath}/play.png", typeof(Texture2D));
            pauseIcon = (Texture2D)AssetDatabase.LoadAssetAtPath($"{packagePath}/pause.png", typeof(Texture2D));
            prevIcon = (Texture2D)AssetDatabase.LoadAssetAtPath($"{packagePath}/prev.png", typeof(Texture2D));
            nextIcon = (Texture2D)AssetDatabase.LoadAssetAtPath($"{packagePath}/next.png", typeof(Texture2D));
            ffwdIcon = (Texture2D)AssetDatabase.LoadAssetAtPath($"{packagePath}/ffwd.png", typeof(Texture2D));
            frwdIcon = (Texture2D)AssetDatabase.LoadAssetAtPath($"{packagePath}/frwd.png", typeof(Texture2D));
        }

        private void OnDisable()
        {
            subjectScripts = null;
        }

        public override void OnInspectorGUI()
        {
            GUI.enabled = false;
            EditorGUILayout.PropertyField(scriptProperty);
            GUI.enabled = true;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Subjects in scene", EditorStyles.boldLabel);
            foreach (CustomSubjectScript subjectScript in subjectScripts)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{subjectScript.transform.name} ({subjectScript.SubejectName})");
                if (GUILayout.Button("Goto subject"))
                {
                    Selection.activeObject = subjectScript;
                    InternalEditorUtility.SetIsInspectorExpanded(subjectScript, true);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            CustomSubjectConfig.instance.Enabled = EditorGUILayout.Toggle("Subjects enabled", CustomSubjectConfig.instance.Enabled);
            if (EditorGUI.EndChangeCheck())
            {
                UpdateContent();
            }
            EditorGUILayout.PropertyField(baseURIProperty);
            using(var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(streamTypeProperty);
                if (check.changed && Application.isPlaying)
                {
                    serializedObject.ApplyModifiedProperties();
                    subjectDataManager.LoadRecordedJson();
                }
            }

            EditorGUILayout.Space();

            StreamType currentStreamType = (StreamType)streamTypeProperty.enumValueIndex;
            switch (currentStreamType)
            {
                case StreamType.LiveStream:
                {
                    LiveStreamControls();
                    break;
                }
                case StreamType.Recorded:
                {
                    RecordedDataControls();
                    break;
                }
                case StreamType.Default:
                {
                    enableWriteDataProperty.boolValue = false;
                    break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void RecordedDataControls()
        {
            enableWriteDataProperty.boolValue = false;
            EditorGUILayout.LabelField("Controls for playing recorded data", EditorStyles.boldLabel);
            int totalFrames = totalFramesProperty.intValue;
            EditorGUI.BeginChangeCheck();
            if (showDuplicateWarning)
            {
                EditorGUILayout.HelpBox("There are duplicate entries in Json Files To Load. Only one of them will be loaded", MessageType.Warning);
            }
            EditorGUILayout.PropertyField(jsonFilesToLoadProperty);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                showDuplicateWarning = subjectDataManager.JsonFilesToLoad.Count != subjectDataManager.JsonFilesToLoad.Distinct().Count();
                totalFrames = totalFramesProperty.intValue = subjectDataManager.LoadRecordedJson();
            }
            EditorGUILayout.IntSlider(currentFrameProperty, 0, totalFrames, $"Current Frame (out of {totalFrames})");
            GUI.color = Color.white;
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent(frwdIcon, "Rewind 30 frames"), GUILayout.Width(50), GUILayout.Height(50)))
            {
                currentFrameProperty.intValue = Mathf.Max(0, currentFrameProperty.intValue - 30);
            }
            if (GUILayout.Button(new GUIContent(prevIcon, "Rewind 1 frame"), GUILayout.Width(50), GUILayout.Height(50)))
            {
                currentFrameProperty.intValue = Mathf.Max(0, currentFrameProperty.intValue - 1);
            }
            if (GUILayout.Button(new GUIContent(playProperty.boolValue ? pauseIcon : playIcon, playProperty.boolValue ? "Pause" : "Play"), GUILayout.Width(50), GUILayout.Height(50)))
            {
                playProperty.boolValue = !playProperty.boolValue;
            }
            if (GUILayout.Button(new GUIContent(nextIcon, "Advance 1 frame"), GUILayout.Width(50), GUILayout.Height(50)))
            {
                currentFrameProperty.intValue = Mathf.Min(totalFrames, currentFrameProperty.intValue + 1);
            }
            if (GUILayout.Button(new GUIContent(ffwdIcon, "Advance 30 frames"), GUILayout.Width(50), GUILayout.Height(50)))
            {
                currentFrameProperty.intValue = Mathf.Max(0, currentFrameProperty.intValue + 30);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void LiveStreamControls()
        {
            EditorGUILayout.LabelField("Controls for recording LiveStream data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableWriteDataProperty);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(pathToDataFileProperty);
            if (GUILayout.Button("Select", GUILayout.MaxWidth(100)))
            {
                pathToDataFileProperty.stringValue = Path.GetRelativePath(Application.dataPath, EditorUtility.OpenFolderPanel("Location to save live streamed data", "", ""));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(fileNameBaseProperty);
        }

        private void UpdateContent()
        {
            CustomSubjectConfig.instance.Save();
            foreach (CustomSubjectScript script in subjectScripts)
            {
                script.enabled = CustomSubjectConfig.instance.Enabled;

                EditorUtility.SetDirty(script);
            }
            Debug.Log($"Updated {subjectScripts.Count} subject script(s):"+
                      $"\n    Enabled:            {CustomSubjectConfig.instance.Enabled};"+
                      $"\n    StreamType: {subjectDataManager.StreamType.ToString()};"+
                      $"\n    Writing data:       {subjectDataManager.EnableWriteData};"+
                      $"\n    Scripts updated: \n         " +
                      string.Join(",\n         ", subjectScripts));
        }
    }
}
