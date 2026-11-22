using UnityEngine;
using UnityEngine.UI;

public sealed class BaggageIcon : MaskableGraphic
{
    bool checkedBag;
    internal static void Add(Transform parent,float x,float y,bool large,Color tint)
    {
        var rect=(RectTransform)new GameObject(large?"Checked bag icon":"Carry-on icon",typeof(RectTransform),typeof(CanvasRenderer)).transform;
        rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up;
        rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(24,24);
        var icon=rect.gameObject.AddComponent<BaggageIcon>(); icon.checkedBag=large; icon.color=tint; icon.raycastTarget=false;
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); float bottom=checkedBag?.16f:.22f,top=checkedBag?.72f:.65f;
        Line(mesh,.22f,bottom,.78f,bottom); Line(mesh,.78f,bottom,.78f,top);
        Line(mesh,.78f,top,.22f,top); Line(mesh,.22f,top,.22f,bottom);
        Line(mesh,.4f,top,.4f,.9f); Line(mesh,.4f,.9f,.6f,.9f); Line(mesh,.6f,.9f,.6f,top);
        Line(mesh,.34f,bottom,.34f,.08f); Line(mesh,.66f,bottom,.66f,.08f);
        if(checkedBag) { Line(mesh,.4f,.3f,.4f,.6f); Line(mesh,.6f,.3f,.6f,.6f); }
    }
    void Line(VertexHelper mesh,float x1,float y1,float x2,float y2)
    {
        var r=rectTransform.rect;
        var a=new Vector2(r.xMin+x1*r.width,r.yMin+y1*r.height); var b=new Vector2(r.xMin+x2*r.width,r.yMin+y2*r.height);
        UIStroke.Line(mesh,a,b,color);
    }
}
