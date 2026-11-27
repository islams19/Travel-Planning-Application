using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Decorative glyph shares the existing control's hit area and never intercepts its input.
public sealed class DropdownChevron : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    ChevronGlyph glyph;
    TMP_Dropdown dropdown;
    bool hovered, pressed, expanded;
    public float BackgroundAlpha => glyph == null ? 0 : glyph.BackgroundAlpha;
    // leftAligned: field-style dropdowns line their value up with neighbouring text inputs.
    public static DropdownChevron Attach(Selectable control,bool leftAligned=false)
    {
        var existing=control.GetComponent<DropdownChevron>();
        if(existing!=null) return existing;
        var result=control.gameObject.AddComponent<DropdownChevron>();
        result.dropdown=control as TMP_Dropdown;
        if(result.dropdown!=null)
        {
            foreach(Transform child in control.transform)
            {
                var oldText=child.GetComponent<TMP_Text>();
                if(child.name=="Arrow" || child.name=="Dropdown indicator" ||
                    oldText!=null && oldText!=result.dropdown.captionText &&
                    (oldText.text=="v" || oldText.text=="▼" || oldText.text=="⌄")) child.gameObject.SetActive(false);
            }
        }
        var rect=(RectTransform)new GameObject("Chevron",typeof(RectTransform),typeof(CanvasRenderer)).transform;
        rect.SetParent(control.transform,false); rect.anchorMin=rect.anchorMax=new Vector2(1,.5f);
        rect.pivot=new Vector2(.5f,.5f); rect.sizeDelta=new Vector2(36,36); rect.anchoredPosition=new Vector2(-22,0);
        result.glyph=rect.gameObject.AddComponent<ChevronGlyph>(); result.glyph.raycastTarget=false;
        var label=result.dropdown!=null ? result.dropdown.captionText : control.GetComponentInChildren<TMP_Text>();
        result.glyph.color=label!=null ? label.color : new Color32(47,80,247,255);
        if(label!=null)
        {
            label.margin=new Vector4(leftAligned?16:12,0,42,0);
            label.alignment=leftAligned?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center;
            if(result.dropdown==null)
            {
                label.fontSizeMax=label.fontSize; label.fontSizeMin=Mathf.Max(16,label.fontSize-3);
                label.enableAutoSizing=true;
            }
        }
        return result;
    }
    public void PointRight() { if(glyph!=null) {glyph.PointsRight=true;glyph.SetVerticesDirty();} }
    public void SetExpanded(bool value) { expanded=value; Refresh(); }
    void Update() { if(dropdown!=null && expanded!=dropdown.IsExpanded) SetExpanded(dropdown.IsExpanded); }
    void Refresh()
    {
        if(glyph==null) return;
        glyph.BackgroundAlpha=pressed || expanded ? .17f : hovered ? .09f : 0;
        glyph.SetVerticesDirty();
    }
    public void OnPointerEnter(PointerEventData _) { hovered=true; Refresh(); }
    public void OnPointerExit(PointerEventData _) { hovered=false; pressed=false; Refresh(); }
    public void OnPointerDown(PointerEventData _) { pressed=true; Refresh(); }
    public void OnPointerUp(PointerEventData _) { pressed=false; Refresh(); }
    void OnDisable() { hovered=pressed=expanded=false; Refresh(); }
}

public sealed class ChevronGlyph : MaskableGraphic
{
    public float BackgroundAlpha { get; set; }
    public bool PointsRight { get; set; }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var rect=rectTransform.rect; Vector2 center=rect.center;
        if(BackgroundAlpha>0)
        {
            var tint=color; tint.a=BackgroundAlpha;
            mesh.AddVert(center,tint,Vector2.zero);
            for(int i=0;i<=32;i++)
            {
                float angle=i*Mathf.PI*2/32;
                mesh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*rect.width*.48f,tint,Vector2.zero);
                if(i>0) mesh.AddTriangle(0,i,i+1);
            }
        }
        if(PointsRight) {Line(mesh,center+new Vector2(-3,6),center+new Vector2(3,0));Line(mesh,center+new Vector2(3,0),center+new Vector2(-3,-6));}
        else {Line(mesh,center+new Vector2(-6,3),center+new Vector2(0,-3));Line(mesh,center+new Vector2(0,-3),center+new Vector2(6,3));}
    }
    void Line(VertexHelper mesh,Vector2 a,Vector2 b)
    {
        UIStroke.Line(mesh,a,b,color,2f);
    }
}
