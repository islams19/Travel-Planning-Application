using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Temporary authoring visibility; never save or play with preview visibility.</summary>
    [InitializeOnLoad]
    public static class EditableScreenPreview
    {
        private static readonly Dictionary<GameObject, bool> originalStates = new Dictionary<GameObject, bool>();
        static EditableScreenPreview()
        {
            EditorSceneManager.sceneSaving += BeforeSave;
            EditorSceneManager.sceneOpening += BeforeOpen;
            EditorApplication.playModeStateChanged += BeforePlay;
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.quitting += Restore;
        }

        public static void SetVisible(GameObject target, bool visible)
        {
            if (!target)
                return;
            if (!originalStates.ContainsKey(target))
                originalStates.Add(target, target.activeSelf);
            target.SetActive(visible);
        }

        public static void Restore()
        {
            foreach (var entry in originalStates)
                if (entry.Key)
                    entry.Key.SetActive(entry.Value);
            originalStates.Clear();
            SceneView.RepaintAll();
        }

        private static void BeforeSave(Scene scene, string path) => Restore();
        private static void BeforeOpen(string path, OpenSceneMode mode) => Restore();
        private static void BeforePlay(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                Restore();
        }
    }
}
