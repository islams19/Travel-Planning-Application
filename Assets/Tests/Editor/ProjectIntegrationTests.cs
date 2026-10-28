using System.Linq;
using NUnit.Framework;
using TravelApp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ProjectIntegrationTests
{
    [Test] public void BuildCatalogIncludesEveryAuthoredProductAndUser()
    {
        var catalog = Resources.Load<TravelCatalog>("TravelCatalog");
        Assert.IsNotNull(catalog, "Use Travel App > Synchronize Catalog first.");
        Assert.AreEqual(AssetDatabase.FindAssets("t:Product", new[] {"Assets/Database/Products"}).Length, catalog.products.Count);
        Assert.AreEqual(AssetDatabase.FindAssets("t:User", new[] {"Assets/Database/Users"}).Length, catalog.users.Count);
        Assert.IsTrue(catalog.products.All(p => p != null && !string.IsNullOrWhiteSpace(p.id)));
        Assert.AreEqual(catalog.products.Count, catalog.products.Select(p => p.id).Distinct().Count());
    }
    [Test] public void AllBuildScenesAndProductPrefabRetainScriptReferences()
    {
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), entry.path + " / " + transform.name);
                foreach (var manager in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LoginManager>(true))) Check(manager, "usernameInput", "passwordInput");
                foreach (var manager in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BookingManager>(true))) Check(manager, "resultsContainer", "productResultPrefab", "searchInput", "typeDropdown", "minPriceInput", "maxPriceInput", "minimumRatingDropdown", "startDateInput", "endDateInput", "sortDropdown");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ProductCard.prefab");
        Assert.IsNotNull(prefab.GetComponent<ProductCardUI>());
    }
    static void Check(Object target, params string[] fields)
    {
        var serialized = new SerializedObject(target);
        foreach (var field in fields) Assert.IsNotNull(serialized.FindProperty(field).objectReferenceValue, target.name + "." + field);
    }
}
