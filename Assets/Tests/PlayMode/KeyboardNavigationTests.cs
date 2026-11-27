using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UIButton=UnityEngine.UI.Button;
using Object=UnityEngine.Object;

public class KeyboardNavigationTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Private).GetValue(o);
    static UIButton Button(Transform t,string n)=>t.GetComponentsInChildren<UIButton>(true).First(b=>b.name==n&&b.gameObject.activeInHierarchy);
    Keyboard keyboard;
    [TearDown]public void Cleanup(){if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);}
    IEnumerator Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return null;yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
    }
    void Selected(Component value)=>Assert.AreEqual(value.gameObject,EventSystem.current.currentSelectedGameObject);
    [UnityTest,Timeout(45000)]public IEnumerator RealKeyboardTraversesLoginSignupSearchPopupAndPaymentWithoutChangingText()
    {
        keyboard=InputSystem.AddDevice<Keyboard>();
        yield return SceneManager.LoadSceneAsync("Login");yield return null;yield return null;
        var login=Object.FindFirstObjectByType<DesktopLoginUI>();var focus=KeyboardFocusNavigation.Instance;
        var username=Field<TMP_InputField>(login,"username");var password=Field<TMP_InputField>(login,"password");var email=Field<TMP_InputField>(login,"email");
        username.text="Keyboard User";password.text="unchanged";focus.Focus(username);yield return null;
        yield return Press(Key.Tab);Selected(password);Assert.AreEqual("unchanged",password.text);
        yield return Press(Key.Tab);Selected(Field<UIButton>(login,"submit"));
        yield return Press(Key.LeftShift,Key.Tab);Selected(password);
        focus.Focus(Field<UIButton>(login,"switchMode"));yield return Press(Key.Space);Assert.IsTrue(Field<bool>(login,"registering"));
        focus.Focus(username);yield return Press(Key.Tab);Selected(email);yield return Press(Key.Tab);Selected(password);
        Assert.IsTrue(password.GetComponentsInChildren<FocusOutline>().Any(r=>r.gameObject.activeInHierarchy));
        focus.Focus(Field<UIButton>(login,"switchMode"));yield return Press(Key.Enter);Assert.IsFalse(Field<bool>(login,"registering"),"Enter should activate a button once");
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();var form=Field<CompactFlightSearch>(nav,"compactSearch");
        var from=Field<TMP_InputField>(Field<DestinationAutocomplete>(nav,"originSearch"),"input");var to=Field<TMP_InputField>(Field<DestinationAutocomplete>(nav,"destinationSearch"),"input");
        focus.Focus(from);yield return Press(Key.Tab);Selected(to); // Disabled swap is skipped until the route is valid.
        Field<DestinationAutocomplete>(nav,"destinationSearch").TrySelectAirportDestination("SYD");
        focus.Focus(from);yield return Press(Key.Tab);Selected(Button(form.transform,"Swap airports"));yield return Press(Key.Tab);Selected(to);
        yield return Press(Key.LeftShift,Key.Tab);Selected(Button(form.transform,"Swap airports"));yield return Press(Key.LeftShift,Key.Tab);Selected(from);
        var traveler=Button(form.transform,"Search travelers");focus.Focus(traveler);yield return Press(Key.Space);
        yield return Press(Key.Tab);Selected(Button(form.transform,"More travelers"));
        yield return Press(Key.Space);Assert.AreEqual(2,Field<FlightResultsPage>(nav,"flightResults").PassengerCount);
        yield return Press(Key.Tab);Selected(Button(form.transform,"More Child"));
        yield return Press(Key.Tab);Selected(Button(form.transform,"More InfantInSeat"));
        yield return Press(Key.Tab);Selected(Button(form.transform,"More InfantOnLap"));
        yield return Press(Key.Tab);Selected(Button(form.transform,"Done travelers"));yield return Press(Key.Space);Selected(traveler);
        yield return Press(Key.Tab);Selected(Button(form.transform,"Search cabin"));
        var dates=Field<DateSelectionPage>(nav,"datePage");
        dates.GetType().GetMethod("RestoreDates",Private).Invoke(dates,new object[]{new DateTime(2027,7,14),null,true});
        nav.GetType().GetMethod("ShowFlightResults",Private).Invoke(nav,new object[]{new DateTime(2027,7,14),null});
        var results=Field<FlightResultsPage>(nav,"flightResults");var detailTrigger=results.GetComponentsInChildren<UIButton>().First(b=>b.name=="Flight details");
        focus.Focus(detailTrigger);detailTrigger.onClick.Invoke();yield return null;yield return null;
        var modal=Field<RectTransform>(results,"detailsOverlay");Assert.IsTrue(focus.Candidates().All(s=>s.transform.IsChildOf(modal)));
        yield return Press(Key.Tab);Selected(Button(modal,"Close flight details"));yield return Press(Key.Space);Assert.IsFalse(modal.gameObject.activeSelf);Selected(detailTrigger);
        results.gameObject.SetActive(false);
        var pay=Field<PaymentPlaceholderPage>(nav,"paymentPage");
        foreach(var page in Field<GameObject[]>(nav,"pages"))page.SetActive(false);pay.gameObject.SetActive(true);yield return null;
        var inputs=new[]{"nameInput","numberInput","expirationInput","cvvInput","postalInput"}.Select(n=>Field<TMP_InputField>(pay,n)).ToArray();
        inputs[0].text="Keyboard Traveler";inputs[1].text="4242424242424242";
        focus.Focus(inputs[0]);for(int i=1;i<inputs.Length;i++){yield return Press(Key.Tab);Selected(inputs[i]);}
        yield return Press(Key.Tab);Selected(Field<UIButton>(pay,"pay"));
        for(int i=inputs.Length-1;i>=0;i--){yield return Press(Key.LeftShift,Key.Tab);Selected(inputs[i]);}
        Assert.AreEqual("Keyboard Traveler",inputs[0].text);Assert.AreEqual("4242 4242 4242 4242",inputs[1].text);
        Assert.AreEqual(1,Object.FindObjectsByType<KeyboardFocusNavigation>(FindObjectsSortMode.None).Length);
    }
}
