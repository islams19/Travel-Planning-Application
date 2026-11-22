using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keeps long segment itineraries readable inside the existing payment/receipt panels.
public static class ItineraryScroll
{
    public static void Wrap(TMP_Text text)
    {
        if(text.font==null) text.font=TMP_Settings.defaultFontAsset;
        text.fontSharedMaterial=text.font.material;
        var original=text.rectTransform;
        var root=Rect("Itinerary scroll",original.parent);
        root.anchorMin=original.anchorMin; root.anchorMax=original.anchorMax; root.pivot=original.pivot;
        root.anchoredPosition=original.anchoredPosition; root.sizeDelta=original.sizeDelta;
        var image=root.gameObject.AddComponent<Image>(); image.color=Color.clear; image.raycastTarget=true;
        var viewport=Rect("Viewport",root); viewport.anchorMax=Vector2.one; viewport.offsetMax=new Vector2(-18,0);
        viewport.gameObject.AddComponent<RectMask2D>();
        original.SetParent(viewport,false); original.anchorMin=Vector2.up; original.anchorMax=Vector2.one;
        original.pivot=Vector2.up; original.anchoredPosition=Vector2.zero; original.sizeDelta=Vector2.zero;
        text.enableAutoSizing=false;
        var scroll=root.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=original;
        scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=50;
        var track=Rect("Scrollbar",root); track.anchorMin=Vector2.right; track.anchorMax=Vector2.one; track.offsetMin=new Vector2(-10,0);
        track.gameObject.AddComponent<Image>().color=new Color32(223,228,235,255);
        var handle=Rect("Handle",track); handle.anchorMax=Vector2.one;
        var handleImage=handle.gameObject.AddComponent<Image>(); handleImage.color=new Color32(96,113,139,255);
        var bar=track.gameObject.AddComponent<Scrollbar>(); bar.direction=Scrollbar.Direction.BottomToTop;
        bar.handleRect=handle; bar.targetGraphic=handleImage; scroll.verticalScrollbar=bar;
        scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
    }

    public static void Refresh(TMP_Text text)
    {
        var scroll=text.GetComponentInParent<ScrollRect>(true);
        Canvas.ForceUpdateCanvases();
        float height=text.GetPreferredValues(text.text,scroll.viewport.rect.width,0).y+12;
        text.rectTransform.sizeDelta=new Vector2(0,Mathf.Max(scroll.viewport.rect.height,height));
        scroll.verticalNormalizedPosition=1;
    }

    static RectTransform Rect(string name,Transform parent)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        rect.gameObject.layer=parent.gameObject.layer; rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=Vector2.zero; rect.sizeDelta=Vector2.zero; return rect;
    }
}
