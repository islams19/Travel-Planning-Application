using NUnit.Framework;
using TravelPlanning.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TravelPlanning.Tests
{
    public sealed class EditableScreenPreviewTests
    {
        [Test]
        public void SavingRestoresVisibilityButKeepsTheAuthorsActualEdits()
        {
            string path = "Assets/InitTestScene-preview-" + System.Guid.NewGuid().ToString("N") + ".unity";
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                // Arrange a clean saved authoring scene, separate from Login.unity.
                var login = new GameObject("Login preview fixture");
                var feature = new GameObject("Feature preview fixture");
                SceneManager.MoveGameObjectToScene(login, scene);
                SceneManager.MoveGameObjectToScene(feature, scene);
                feature.SetActive(false);
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                Assert.That(scene.isDirty, Is.False);
                // Preview visibility itself must not create fake unsaved edits.
                EditableScreenPreview.SetVisible(login, false);
                EditableScreenPreview.SetVisible(feature, true);
                Assert.That(scene.isDirty, Is.False);
                // A real authoring edit remains dirty and survives restoration/saving.
                feature.name = "Author renamed feature";
                EditorSceneManager.MarkSceneDirty(scene);
                EditableScreenPreview.Restore();
                Assert.That(scene.isDirty, Is.True);
                EditableScreenPreview.SetVisible(login, false);
                EditableScreenPreview.SetVisible(feature, true);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                // The sceneSaving callback restored startup states before serialization.
                EditorSceneManager.CloseScene(scene, true);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var roots = scene.GetRootGameObjects();
                Assert.That(System.Array.Find(roots, root => root.name == "Login preview fixture").activeSelf, Is.True);
                Assert.That(System.Array.Find(roots, root => root.name == "Author renamed feature").activeSelf, Is.False);
            }
            finally
            {
                EditableScreenPreview.Restore();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void RestoreKeepsOriginalStateAcrossRepeatedScreenChanges()
        {
            // Arrange two authoring objects with different startup visibility.
            var login = new GameObject("Preview test login");
            var feature = new GameObject("Preview test feature");
            feature.SetActive(false);
            try
            {
                // Act: changing preview repeatedly must retain the first saved state.
                EditableScreenPreview.SetVisible(login, false);
                EditableScreenPreview.SetVisible(feature, true);
                EditableScreenPreview.SetVisible(login, true);
                EditableScreenPreview.SetVisible(login, false);
                EditableScreenPreview.Restore();
                // Assert: startup visibility is restored, not the last preview.
                Assert.That(login.activeSelf, Is.True);
                Assert.That(feature.activeSelf, Is.False);
            }
            finally
            {
                EditableScreenPreview.Restore();
                Object.DestroyImmediate(login);
                Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void RestoreToleratesAnObjectDeletedDuringEditing()
        {
            var screen = new GameObject("Deleted preview test");
            EditableScreenPreview.SetVisible(screen, false);
            Object.DestroyImmediate(screen);
            Assert.DoesNotThrow(EditableScreenPreview.Restore);
        }
    }
}
