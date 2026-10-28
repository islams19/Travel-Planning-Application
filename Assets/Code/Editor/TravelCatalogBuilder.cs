using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using TravelApp;

[InitializeOnLoad]
public sealed class TravelCatalogBuilder : IPreprocessBuildWithReport
{
    static bool syncing;
    static TravelCatalogBuilder() { EditorApplication.delayCall += Synchronize; }
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) { Synchronize(); }
    [MenuItem("Travel App/Synchronize Catalog")]
    public static void Synchronize()
    {
        if (syncing || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Synchronize; return; }
        syncing = true;
        try
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            const string path = "Assets/Resources/TravelCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<TravelCatalog>(path);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<TravelCatalog>(); AssetDatabase.CreateAsset(catalog, path); }
            var products = AssetDatabase.FindAssets("t:Product", new[] { "Assets/Database/Products" })
                .OrderBy(g => g, StringComparer.Ordinal).Select(g =>
                {
                    var product = AssetDatabase.LoadAssetAtPath<Product>(AssetDatabase.GUIDToAssetPath(g));
                    if (string.IsNullOrWhiteSpace(product.id)) { product.id = g; EditorUtility.SetDirty(product); }
                    return product;
                }).Where(p => p != null).ToList();
            if (products.GroupBy(p => p.id).Any(g => g.Count() > 1))
                throw new BuildFailedException("Duplicate Product IDs. Clear the ID on duplicated products and synchronize again.");
            var users = AssetDatabase.FindAssets("t:User", new[] { "Assets/Database/Users" }).OrderBy(g => g, StringComparer.Ordinal)
                .Select(g => AssetDatabase.LoadAssetAtPath<User>(AssetDatabase.GUIDToAssetPath(g))).Where(u => u != null).ToList();
            if (users.Any(u => string.IsNullOrWhiteSpace(u.username)) || users.GroupBy(u => (u.username ?? "").Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new BuildFailedException("Usernames must be non-empty and unique (ignoring case and spaces). Check User assets in Assets/Database/Users.");
            if (!catalog.products.SequenceEqual(products) || !catalog.users.SequenceEqual(users))
            { catalog.products = products; catalog.users = users; EditorUtility.SetDirty(catalog); }
            foreach (var guid in AssetDatabase.FindAssets("t:ProductDatabase", new[] { "Assets/Database" }))
            {
                var db = AssetDatabase.LoadAssetAtPath<ProductDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (!db.products.SequenceEqual(products)) { db.products = products.ToList(); EditorUtility.SetDirty(db); }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UserDatabase", new[] { "Assets/Database" }))
            {
                var db = AssetDatabase.LoadAssetAtPath<UserDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (!db.users.SequenceEqual(users)) { db.users = users.ToList(); EditorUtility.SetDirty(db); }
            }
            AssetDatabase.SaveAssets();
        }
        finally { syncing = false; }
    }
}
public sealed class TravelCatalogImportWatcher : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p => p.StartsWith("Assets/Database/", StringComparison.Ordinal)))
            EditorApplication.delayCall += TravelCatalogBuilder.Synchronize;
    }
}
