using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

// Popup edits the passenger state already owned by FlightResultsPage.
public sealed class PassengerSelector : MonoBehaviour
{
    FlightResultsPage options;
    TMP_Text fareNote;
    readonly TMP_Text[] counts=new TMP_Text[4];
    readonly UnityEngine.UI.Button[] less=new UnityEngine.UI.Button[4],more=new UnityEngine.UI.Button[4];
    static readonly Color Ink=new Color32(29,40,59,255),Blue=new Color32(47,80,247,255),Pale=new Color32(235,240,255,255),Muted=new Color32(96,113,139,255);
    public void Build(DateSelectionPage style,Transform parent,FlightResultsPage state,Action close)
    {
        options=state;Text("Travelers title",parent,20,12,400,30,"Travelers · up to 6",23,Ink,true,TextAlignmentOptions.Left);
        fareNote=Text("Category fare note",parent,20,44,400,24,"Illustrative fares: 100% / 75% / 75% / 10%",15,Muted,false,TextAlignmentOptions.Left);
        for(int i=0;i<4;i++)
        {
            int index=i;var category=(PassengerParty.Category)i;float y=76+i*76;
            Text("Category "+category,parent,20,y,214,30,(i==0?"Adults":i==1?"Children":i==2?"Infants in seats":"Infants on lap"),20,Ink,true,TextAlignmentOptions.Left);
            Text("Age "+category,parent,20,y+29,214,24,PassengerParty.Age(category),16,Muted,false,TextAlignmentOptions.Left);
            less[i]=style.Control(i==0?"Fewer travelers":"Fewer "+category,parent,244,y+4,48,44,"−",Pale,Blue,25,2);
            counts[i]=Text("Count "+category,parent,298,y+4,56,44,"",23,Ink,true);
            more[i]=style.Control(i==0?"More travelers":"More "+category,parent,364,y+4,48,44,"+",Pale,Blue,25,2);
            less[i].onClick.AddListener(()=>Change(index,-1));more[i].onClick.AddListener(()=>Change(index,1));
        }
        Text("Lap infant rule",parent,20,382,400,24,"One lap infant per adult. Lap infants need no seat.",15,Muted,false,TextAlignmentOptions.Left);
        style.Control("Done travelers",parent,20,422,392,36,"Done",Pale,Blue,19,2).onClick.AddListener(()=>close());Refresh();
    }
    bool Allowed(int index,int change)
    {
        var p=options.Passengers;int[] c={p.CountFor(PassengerParty.Category.Adult),p.CountFor(PassengerParty.Category.Child),p.CountFor(PassengerParty.Category.InfantInSeat),p.CountFor(PassengerParty.Category.InfantOnLap)};c[index]+=change;
        return c[0]>=1 && c[1]>=0 && c[2]>=0 && c[3]>=0 && c[3]<=c[0] && c[0]+c[1]+c[2]+c[3]<=6;
    }
    void Change(int index,int delta)
    {
        if(!Allowed(index,delta))return;var p=options.Passengers;int[] c={p.CountFor(PassengerParty.Category.Adult),p.CountFor(PassengerParty.Category.Child),p.CountFor(PassengerParty.Category.InfantInSeat),p.CountFor(PassengerParty.Category.InfantOnLap)};c[index]+=delta;
        options.SetPassengerCategories(c[0],c[1],c[2],c[3]);Refresh();
    }
    public void Refresh()
    {
        if(options==null)return;
        var rates=options.Passengers.rates??new PassengerParty.FareRates();
        fareNote.text=$"Illustrative fares: {rates.adult}% / {rates.child}% / {rates.infantInSeat}% / {rates.infantOnLap}%";
        for(int i=0;i<4;i++)
        {
            counts[i].text=options.Passengers.CountFor((PassengerParty.Category)i).ToString();less[i].interactable=Allowed(i,-1);more[i].interactable=Allowed(i,1);
            foreach(var button in new[]{less[i],more[i]})
            {button.targetGraphic.color=button.interactable?Pale:new Color32(235,238,243,255);button.GetComponentInChildren<TMP_Text>().color=button.interactable?Blue:Muted;}
        }
    }
}
