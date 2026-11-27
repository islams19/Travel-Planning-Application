using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Selected page styling is independent of EventSystem focus and pointer hover.
public sealed class SidebarItemVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    UnityEngine.UI.Button button;
    Color normal;
    readonly Dictionary<Graphic,Color> original=new Dictionary<Graphic,Color>();
    public bool IsSelected { get; private set; }
    public bool IsHovered { get; private set; }
    public void Configure(UnityEngine.UI.Button value)
    {
        button=value;normal=Color.clear;button.transition=Selectable.Transition.None;
        foreach(var graphic in GetComponentsInChildren<Graphic>())
            if(graphic!=button.targetGraphic)original[graphic]=new Color32(153,169,192,255);
        Refresh();
    }
    public void SetSelected(bool selected) { IsSelected=selected;Refresh(); }
    public void OnPointerEnter(PointerEventData data) { IsHovered=true;Refresh(); }
    public void OnPointerExit(PointerEventData data) { IsHovered=false;Refresh(); }
    void OnDisable() { IsHovered=false; }
    void Refresh()
    {
        if(button==null)return;
        button.targetGraphic.color=IsSelected ? (IsHovered?new Color32(65,99,255,255):new Color32(47,80,247,255)) :
            IsHovered?new Color32(28,42,72,255):normal;
        foreach(var pair in original) if(pair.Key!=null)pair.Key.color=IsSelected?Color.white:pair.Value;
    }
}
