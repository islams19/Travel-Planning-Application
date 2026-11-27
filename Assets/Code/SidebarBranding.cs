using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared shell styling only: existing buttons and navigation callbacks remain the input owners.
public sealed class SidebarBranding : MonoBehaviour
{
    static readonly Color Blue=new Color32(47,80,247,255),Muted=new Color32(153,169,192,255);
    Texture2D roundedTexture;
    Sprite rounded;
    public void Build(UnityEngine.UI.Button[] buttons)
    {
        rounded=Rounded();
        var badge=Rect("Voyage logo",transform,40,40,60,60);
        var image=badge.gameObject.AddComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;image.color=Blue;image.raycastTarget=false;
        ReviewIcon.Add(badge,10,10,40,ReviewIcon.Symbol.Flight,Color.white);
        var plane=(RectTransform)badge.GetChild(0);plane.pivot=new Vector2(.5f,.5f);plane.anchoredPosition=new Vector2(30,-30);plane.localRotation=Quaternion.Euler(0,0,-45);
        DateSelectionPage.Text("Voyage brand",transform,116,43,224,54,"Voyage",36,Color.white,true,TextAlignmentOptions.Left);
        DateSelectionPage.Text("Navigation heading",transform,40,166,300,26,"MENU",14,Muted,false,TextAlignmentOptions.Left);
        string[] labels={"Search Flights","Explore World","My Trips"};
        var symbols=new[]{ReviewIcon.Symbol.Search,ReviewIcon.Symbol.Globe,ReviewIcon.Symbol.Calendar};
        for(int i=0;i<3;i++)
        {
            var button=buttons[i];var rect=(RectTransform)button.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=Vector2.up;
            rect.anchoredPosition=new Vector2(40,-(214+i*88));rect.sizeDelta=new Vector2(300,66);
            var background=(Image)button.targetGraphic;background.sprite=rounded;background.type=Image.Type.Sliced;background.pixelsPerUnitMultiplier=1;background.color=Color.clear;
            foreach(var shadow in button.GetComponents<Shadow>())shadow.enabled=false;
            foreach(var raster in button.GetComponentsInChildren<Image>())if(raster!=background)raster.gameObject.SetActive(false);
            var label=button.GetComponentInChildren<TMP_Text>();label.text=labels[i];label.fontSize=23;label.fontStyle=FontStyles.Normal;label.alignment=TextAlignmentOptions.Left;label.enableAutoSizing=false;label.textWrappingMode=TextWrappingModes.NoWrap;label.raycastTarget=false;
            var textRect=label.rectTransform;textRect.anchorMin=textRect.anchorMax=textRect.pivot=Vector2.up;textRect.anchoredPosition=new Vector2(66,0);textRect.sizeDelta=new Vector2(218,66);label.margin=Vector4.zero;
            ReviewIcon.Add(button.transform,22,18,30,symbols[i],Muted);
        }
        if(buttons.Length>3)buttons[3].gameObject.SetActive(false);
    }
    static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.gameObject.layer=parent.gameObject.layer;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up;rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
    }
    Sprite Rounded()
    {
        const int size=96,radius=18;roundedTexture=new Texture2D(size,size,TextureFormat.RGBA32,false);roundedTexture.wrapMode=TextureWrapMode.Clamp;
        var pixels=new Color32[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {float dx=x-Mathf.Clamp(x,radius,size-radius-1),dy=y-Mathf.Clamp(y,radius,size-radius-1);float alpha=Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy));pixels[y*size+x]=new Color(1,1,1,alpha);}
        roundedTexture.SetPixels32(pixels);roundedTexture.Apply();return Sprite.Create(roundedTexture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(22,22,22,22));
    }
    void OnDestroy(){if(rounded!=null)Destroy(rounded);if(roundedTexture!=null)Destroy(roundedTexture);}
}
