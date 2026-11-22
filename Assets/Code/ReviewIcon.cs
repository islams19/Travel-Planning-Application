using UnityEngine;
using UnityEngine.UI;
public sealed class ReviewIcon : MaskableGraphic
{
    public enum Symbol { Flight, Travelers, Seat, Options, Payment, Clock }
    Symbol symbol;
    public static void Add(Transform parent,float x,float y,float size,Symbol kind,Color tint)
    {
        var r=(RectTransform)new GameObject(kind+" icon",typeof(RectTransform),typeof(CanvasRenderer)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.up;r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=Vector2.one*size;
        var g=r.gameObject.AddComponent<ReviewIcon>();g.symbol=kind;g.color=tint;g.raycastTarget=false;
    }
    protected override void OnPopulateMesh(VertexHelper m)
    {
        m.Clear();
        switch(symbol)
        {
            case Symbol.Flight:
                var plane=new[]{new Vector2(.48f,.92f),new Vector2(.52f,.92f),new Vector2(.57f,.57f),new Vector2(.91f,.35f),new Vector2(.91f,.27f),new Vector2(.55f,.4f),new Vector2(.55f,.18f),new Vector2(.7f,.1f),new Vector2(.7f,.04f),new Vector2(.5f,.1f),new Vector2(.3f,.04f),new Vector2(.3f,.1f),new Vector2(.45f,.18f),new Vector2(.45f,.4f),new Vector2(.09f,.27f),new Vector2(.09f,.35f),new Vector2(.43f,.57f)};
                for(int i=0;i<plane.Length;i++){var a=plane[i];var b=plane[(i+1)%plane.Length];L(m,a.x,a.y,b.x,b.y);}break;
            case Symbol.Travelers: Circle(m,.4f,.72f,.15f);Circle(m,.78f,.64f,.11f);L(m,.15f,.15f,.2f,.4f);L(m,.2f,.4f,.6f,.4f);L(m,.6f,.4f,.65f,.15f);L(m,.75f,.55f,.85f,.4f);L(m,.85f,.4f,.9f,.15f);break;
            case Symbol.Seat: Circle(m,.48f,.82f,.12f);L(m,.43f,.62f,.45f,.36f);L(m,.45f,.36f,.78f,.36f);L(m,.78f,.36f,.86f,.12f);L(m,.18f,.63f,.25f,.17f);L(m,.25f,.17f,.65f,.17f);break;
            case Symbol.Options: Circle(m,.5f,.5f,.38f);L(m,.3f,.5f,.7f,.5f);L(m,.5f,.3f,.5f,.7f);break;
            case Symbol.Payment: L(m,.1f,.2f,.9f,.2f);L(m,.9f,.2f,.9f,.8f);L(m,.9f,.8f,.1f,.8f);L(m,.1f,.8f,.1f,.2f);L(m,.1f,.6f,.9f,.6f);break;
            case Symbol.Clock: Circle(m,.5f,.5f,.4f);L(m,.5f,.5f,.5f,.75f);L(m,.5f,.5f,.7f,.4f);break;
        }
    }
    void Circle(VertexHelper m,float x,float y,float r) {for(int i=0;i<48;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;L(m,x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,x+Mathf.Cos(b)*r,y+Mathf.Sin(b)*r);}}
    void L(VertexHelper m,float x,float y,float z,float w)
    {
        var r=rectTransform.rect;var a=new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);var b=new Vector2(r.xMin+z*r.width,r.yMin+w*r.height);
        UIStroke.Line(m,a,b,color);
    }
}
