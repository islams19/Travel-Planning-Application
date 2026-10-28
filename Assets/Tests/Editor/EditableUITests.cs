using System.Linq;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EditableUITests
{
    [Test] public void EverySceneHasOneAuthoredShellAndVisibleInitialPanel()
    {
        string[] names = { "LoginScreen", "HomeScreen", "BookingScreen", "AccountScreen" };
        foreach (var name in names)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var shells = roots.SelectMany(r => r.GetComponentsInChildren<TravelScreenUI>(true)).ToArray();
                Assert.AreEqual(1,shells.Length,name); var shell = shells[0];
                Assert.AreEqual(1, roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Count());
                Assert.AreEqual(1, roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Count());
                Assert.AreEqual(9,shell.pages.Length); Assert.AreEqual(1,shell.pages.Count(p => p.activeSelf));
                Assert.IsTrue(shell.pages[(int)shell.initialPage].activeSelf);
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(shell));
                Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(shell.GetComponent<RectTransform>()); Canvas.ForceUpdateCanvases();
                Assert.Greater(shell.heading.rectTransform.rect.width, 700, "Heading uses full content width.");
                if (shell.initialPage == TravelPage.Catalog)
                {
                    var filters = shell.catalog.transform.Find("EditableFilters").GetComponent<RectTransform>();
                    Assert.LessOrEqual(filters.rect.height, 330, "Filters must leave space for results.");
                }
                Assert.IsNotNull(shell.auth.loginUsername); Assert.IsNotNull(shell.catalog.results.itemPrefab);
                Assert.IsNotNull(shell.details.startDateInput); Assert.IsNotNull(shell.cart.cartItems); Assert.IsNotNull(shell.account.bookings);
                Assert.IsTrue(shell.GetComponentsInChildren<TMP_Text>(true).Length>50,"UI must exist before Play Mode.");
                Assert.AreEqual(0, roots.SelectMany(r => r.GetComponentsInChildren<BookingManager>(true)).Count(),"Old overlapping UI removed.");
                foreach (var presenter in shell.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null && c.GetType().Namespace == "TravelApp"))
                {
                    var data = new SerializedObject(presenter); var prop = data.GetIterator();
                    while(prop.NextVisible(true))
                        if(prop.propertyType==SerializedPropertyType.ObjectReference && prop.name!="m_Script") Assert.IsNotNull(prop.objectReferenceValue,presenter.name+"."+prop.propertyPath);
                }
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }
    }
    [Test] public void DesignerCanPreviewAllScreensWithoutRunning()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/AppShell.prefab");
        var clone = Object.Instantiate(prefab);
        try
        {
            var screen=clone.GetComponent<TravelScreenUI>();
            screen.pageHeadings[(int)TravelPage.Home].text = "My edited heading";
            screen.pageSubtitles[(int)TravelPage.Home].text = "My edited subtitle";
            foreach(TravelPage page in System.Enum.GetValues(typeof(TravelPage)))
            {
                screen.PreviewPage(page); Assert.IsTrue(screen.pages[(int)page].activeSelf); Assert.AreEqual(1,screen.pages.Count(p=>p.activeSelf));
            }
            Assert.AreEqual("My edited heading",screen.pageHeadings[(int)TravelPage.Home].text);
            Assert.AreEqual("My edited subtitle",screen.pageSubtitles[(int)TravelPage.Home].text);
        }
        finally { Object.DestroyImmediate(clone); }
    }
    [Test] public void DataBindingPreservesInspectorImageStyling()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/ProductCard.prefab");
        var clone=Object.Instantiate(prefab);
        try
        {
            var item=clone.GetComponent<TravelItemView>(); item.image.color=Color.red; item.image.preserveAspect=false;
            item.Set("TYPE","NAME","DESCRIPTION","$10",null,"",null);
            Assert.AreEqual(Color.red,item.image.color); Assert.IsFalse(item.image.preserveAspect);
        }
        finally { Object.DestroyImmediate(clone); }
    }
}
