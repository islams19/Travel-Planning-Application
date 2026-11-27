using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

// A compact section of Trip Review; all edits write directly to the existing booking's passengers.
public sealed class TravelerDetailsForm : MonoBehaviour
{
    static readonly Color Ink=new Color32(29,40,59,255),Muted=new Color32(96,113,139,255),Blue=new Color32(47,80,247,255),Pale=new Color32(235,240,255,255);
    PassengerParty party;
    DateSelectionPage style;
    Action<float> resized;
    string signature;
    readonly List<Row> rows=new List<Row>();
    readonly List<TMP_InputField> fields=new List<TMP_InputField>();
    RectTransform contact;
    KeyboardFocusOrder focus;
    public float Height {get;private set;}
    public event Action DetailsChanged;
    sealed class Row
    {
        public RectTransform Frame;
        public Transform Body;
        public TMP_InputField First,Last;
        public UnityEngine.UI.Button Toggle;
        public PassengerParty.Traveler Traveler;
        public bool Expanded;
        public int Index;
    }
    public void Bind(DateSelectionPage theme,PassengerParty passengers,Action<float> onResize)
    {
        resized=onResize;
        if(ReferenceEquals(party,passengers) && signature==passengers.SeatSignature){Layout();return;}
        foreach(Transform child in transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        rows.Clear();fields.Clear();party=passengers;signature=party.SeatSignature;style=theme;
        if(string.IsNullOrWhiteSpace(party.email))party.email=TravelApp.TravelAppRuntime.Instance?.Services?.Accounts.Email??"";
        Text("Traveler heading",transform,24,6,850,36,"Traveler details",26,Ink,true,TextAlignmentOptions.Left);
        Text("Traveler guidance",transform,24,48,850,34,"Add names and a primary contact before payment. You can save an unfinished draft.",17,Muted,false,TextAlignmentOptions.Left);
        for(int i=0;i<party.Count;i++)
        {
            var traveler=party.travelers[i];var panel=style.Panel("Traveler "+(i+1)+" details",transform,0,0,910,190);
            var row=new Row{Frame=(RectTransform)panel.parent,Traveler=traveler,Expanded=i==0,Index=i};
            row.Toggle=style.Control("Expand traveler "+(i+1),panel,20,12,866,44,"",Pale,Blue,19,2);
            DropdownChevron.Attach(row.Toggle);
            row.Body=new GameObject("Name fields",typeof(RectTransform)).transform;row.Body.SetParent(panel,false);
            var body=(RectTransform)row.Body;body.anchorMin=body.anchorMax=body.pivot=Vector2.up;body.sizeDelta=new Vector2(906,130);body.anchoredPosition=new Vector2(0,-68);
            row.First=Field(row.Body,"Traveler "+(i+1)+" first name","First name",24,0,410,traveler.firstName,80,()=>PassengerParty.NameError(traveler.firstName),value=>traveler.firstName=value);
            row.Last=Field(row.Body,"Traveler "+(i+1)+" last name","Last name",462,0,420,traveler.lastName,80,()=>PassengerParty.NameError(traveler.lastName),value=>traveler.lastName=value);
            row.Toggle.onClick.AddListener(()=>{row.Expanded=!row.Expanded;Layout();});
            rows.Add(row);
        }
        var cp=style.Panel("Booking contact",transform,0,0,910,190);contact=(RectTransform)cp.parent;
        Text("Contact heading",cp,24,14,850,32,"Primary booking contact",23,Ink,true,TextAlignmentOptions.Left);
        Field(cp,"Booking email","Email",24,58,410,party.email,120,()=>TravelApp.EmailAddressValidator.IsValid(party.email)?null:"Enter a valid email.",value=>party.email=value);
        var phone=Field(cp,"Booking phone","Phone number",462,58,420,party.phone,30,()=>PassengerParty.PhoneError(party.phone),value=>party.phone=value);
        phone.contentType=TMP_InputField.ContentType.Standard;
        if(focus==null)focus=gameObject.AddComponent<KeyboardFocusOrder>();
        // Traversal follows visual order, including collapsed-section toggles and the contact fields.
        var order=new List<Selectable>();foreach(var row in rows){order.Add(row.Toggle);order.Add(row.First);order.Add(row.Last);}order.AddRange(fields.Where(f=>f.name=="Booking email"||f.name=="Booking phone"));
        focus.Configure(order.ToArray());Layout();
    }
    TMP_InputField Field(Transform parent,string name,string caption,float x,float y,float width,string value,int limit,Func<string> validation,Action<string> changed)
    {
        Text(name+" label",parent,x,y,width,26,caption,18,Ink,true,TextAlignmentOptions.Left);
        var shape=style.Shape(name,parent,x,y+32,width,52,new Color32(241,244,249,255),2);shape.gameObject.SetActive(false);
        var viewport=(RectTransform)new GameObject("Text viewport",typeof(RectTransform),typeof(RectMask2D)).transform;viewport.SetParent(shape,false);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(14,0);viewport.offsetMax=new Vector2(-14,0);
        var text=Text("Text",viewport,0,0,width-28,52,"",20,Ink,false,TextAlignmentOptions.Left);var hint=Text("Placeholder",viewport,0,0,width-28,52,caption,20,Muted,false,TextAlignmentOptions.Left);
        foreach(var t in new[]{text,hint}){t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;t.textWrappingMode=TextWrappingModes.NoWrap;}
        var input=shape.gameObject.AddComponent<TMP_InputField>();input.textViewport=viewport;input.textComponent=text;input.placeholder=hint;input.targetGraphic=shape.GetComponent<Image>();input.targetGraphic.raycastTarget=true;
        input.characterLimit=limit;input.customCaretColor=true;input.caretColor=Blue;input.lineType=TMP_InputField.LineType.SingleLine;input.SetTextWithoutNotify(value??"");
        input.onValueChanged.AddListener(v=>{changed(v);RefreshCaptions();DetailsChanged?.Invoke();});
        FormFieldFeedback.Attach(input,validation);shape.gameObject.SetActive(true);fields.Add(input);return input;
    }
    void RefreshCaptions()
    {
        foreach(var row in rows)
            row.Toggle.GetComponentInChildren<TMP_Text>().text=$"Traveler {row.Index+1} · {PassengerParty.Label(row.Traveler.category)}"+(row.Traveler.Name.Length>0?" · "+row.Traveler.Name:"");
    }
    void Layout()
    {
        float y=94;
        foreach(var row in rows)
        {
            row.Frame.anchoredPosition=new Vector2(0,-y);float height=row.Expanded?196:72;row.Frame.sizeDelta=new Vector2(910,height);
            var surface=(RectTransform)row.Toggle.transform.parent;surface.sizeDelta=new Vector2(907,height-3);row.Body.gameObject.SetActive(row.Expanded);row.Toggle.GetComponent<DropdownChevron>().SetExpanded(row.Expanded);y+=height+14;
        }
        contact.anchoredPosition=new Vector2(0,-y);Height=y+206;((RectTransform)transform).sizeDelta=new Vector2(0,Height);RefreshCaptions();resized?.Invoke(Height);
    }
    public void FocusFirst()
    {
        var row=rows.FirstOrDefault(r=>PassengerParty.NameError(r.Traveler.firstName)!=null || PassengerParty.NameError(r.Traveler.lastName)!=null);
        TMP_InputField target;
        if(row!=null){row.Expanded=true;target=PassengerParty.NameError(row.Traveler.firstName)!=null?row.First:row.Last;}
        else target=fields.FirstOrDefault(f=>f.name=="Booking email" && !TravelApp.EmailAddressValidator.IsValid(party.email) || f.name=="Booking phone" && PassengerParty.PhoneError(party.phone)!=null)??rows[0].First;
        if(target==rows[0].First)rows[0].Expanded=true;
        Layout();Canvas.ForceUpdateCanvases();ScrollTo((RectTransform)transform);FormFieldFeedback.Focus(target);
    }
    void ScrollTo(RectTransform target)
    {
        var scroll=GetComponentInParent<ScrollRect>();if(scroll==null)return;
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,target);
        float y=scroll.content.anchoredPosition.y+scroll.viewport.rect.yMax-bounds.max.y-16;
        scroll.StopMovement();scroll.content.anchoredPosition=new Vector2(scroll.content.anchoredPosition.x,Mathf.Clamp(y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
    }
    public bool ValidateAll()
    {
        TMP_InputField first=null;
        foreach(var field in fields)
        {
            if(!field.GetComponent<FormFieldFeedback>().Validate())
            {
                var row=rows.FirstOrDefault(r=>r.First==field||r.Last==field);if(row!=null)row.Expanded=true;
                if(first==null)first=field;
            }
        }
        Layout();if(first!=null){Canvas.ForceUpdateCanvases();var row=rows.FirstOrDefault(r=>r.First==first||r.Last==first);ScrollTo(row?.Frame??contact);FormFieldFeedback.Focus(first);}return first==null&&party.HasValidDetails;
    }
}
