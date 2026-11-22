using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Visual form order, with nonmodal popup controls inserted after their trigger.
public sealed class KeyboardFocusOrder : MonoBehaviour
{
    Selectable[] controls=System.Array.Empty<Selectable>();
    readonly Dictionary<Selectable,Transform> popups=new Dictionary<Selectable,Transform>();
    public void Configure(params Selectable[] values)=>controls=values;
    public void RegisterPopup(Selectable trigger,Transform popup)=>popups[trigger]=popup;
    public IEnumerable<Selectable> Ordered()
    {
        foreach(var control in controls)
        {
            if(control==null)continue;yield return control;
            if(popups.TryGetValue(control,out var popup)&&popup!=null&&popup.gameObject.activeInHierarchy)
                foreach(var child in popup.GetComponentsInChildren<Selectable>().Where(s=>!(s is Scrollbar)))yield return child;
        }
    }
    public void ClosingPopup(Transform popup)
    {
        var selected=EventSystem.current?.currentSelectedGameObject;
        if(selected==null||!selected.transform.IsChildOf(popup))return;
        var trigger=popups.FirstOrDefault(p=>p.Value==popup).Key;
        if(trigger!=null)KeyboardFocusNavigation.Instance?.Focus(trigger);
    }
}
