using UnityEngine;
using UnityEngine.UI;

// All artwork is bundled locally. Unknown carriers retain a readable code badge.
public static class AirlineLogo
{
    public static Texture2D Load(string code) => string.IsNullOrEmpty(code) ? null : Resources.Load<Texture2D>("AirlineLogos/"+code);
    internal static void Build(DateSelectionPage style,Transform parent,float x,float y,float width,float height,string code)
    {
        var frame=style.Shape("Airline badge",parent,x,y,width,height,new Color32(245,247,252,255),3);
        var texture=Load(code);
        if(texture==null)
        {
            DateSelectionPage.Text("Airline code",frame,0,0,width,height,code,19,new Color32(47,80,247,255),true);
            return;
        }
        var logo=new GameObject("Airline logo",typeof(RectTransform),typeof(RawImage));
        var rect=(RectTransform)logo.transform; rect.SetParent(frame,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
        float factor=Mathf.Min(1,Mathf.Min((width-20)/texture.width,(height-12)/texture.height));
        rect.sizeDelta=new Vector2(texture.width*factor,texture.height*factor);
        var image=logo.GetComponent<RawImage>(); image.texture=texture; image.raycastTarget=false;
    }
}
