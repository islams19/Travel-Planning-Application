using UnityEngine;
using UnityEngine.UI;

// Feathered vector strokes stay smooth on overlay canvases without a raster icon atlas.
public static class UIStroke
{
    public static void Line(VertexHelper mesh,Vector2 a,Vector2 b,Color tint,float width=1.8f)
    {
        var n=new Vector2(-(b-a).y,(b-a).x).normalized;
        Quad(mesh,a-n*width*.5f,a+n*width*.5f,b+n*width*.5f,b-n*width*.5f,tint,tint,tint,tint);
        var clear=tint;clear.a=0;float outer=width*.5f+.65f;
        Quad(mesh,a+n*width*.5f,a+n*outer,b+n*outer,b+n*width*.5f,tint,clear,clear,tint);
        Quad(mesh,a-n*outer,a-n*width*.5f,b-n*width*.5f,b-n*outer,clear,tint,tint,clear);
    }
    static void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd)
    {int i=mesh.currentVertCount;mesh.AddVert(a,ca,Vector2.zero);mesh.AddVert(b,cb,Vector2.zero);mesh.AddVert(c,cc,Vector2.zero);mesh.AddVert(d,cd,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);}
}
