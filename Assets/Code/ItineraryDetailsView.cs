using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One presentation of the existing itinerary model, shared by Results and Review.
public static class ItineraryDetailsView
{
    static readonly Color Ink=new Color32(29,40,59,255),Muted=new Color32(96,113,139,255),Blue=new Color32(47,80,247,255),Pale=new Color32(235,240,255,255);
    public static float Build(DateSelectionPage style,RectTransform parent,float width,FlightResultsPage.Flight flight,string cabin,int fare,int passengers,IReadOnlyList<IReadOnlyList<string>> seats=null)
    {
        TMP_Text Label(Transform p,string name,float x,float y,float w,float h,string text,float size=19,bool bold=false,Color? color=null)
        {
            var t=DateSelectionPage.Text(name,p,x,y,w,h,text,size,color??Ink,bold,TextAlignmentOptions.TopLeft);
            t.textWrappingMode=TextWrappingModes.Normal;return t;
        }
        float inner=width-48,col=(inner-24)/2,y=0;
        var summary=style.Shape("Itinerary summary",parent,0,y,width,116,Pale,2);
        Label(summary,"Summary route",24,18,inner,32,flight.Segments.First().OriginCode+" → "+flight.Segments.Last().DestinationCode,25,true);
        Label(summary,"Summary travel",24,58,inner,50,flight.Segments[0].TravelDate.ToString("ddd, MMM d, yyyy",System.Globalization.CultureInfo.InvariantCulture)+" · "+FlightResultsPage.DurationLabel(flight.DurationMinutes)+" total\n"+(flight.Stops==0?"Nonstop":$"{flight.Stops} stops")+" · "+cabin,18,false,Muted);y+=134;
        for(int i=0;i<flight.Segments.Count;i++)
        {
            var s=flight.Segments[i];
            var card=style.Panel("Flight segment "+(i+1),parent,0,y,width,350);
            Label(card,"Segment route",24,18,inner,32,$"Flight {i+1} · {s.OriginCode} → {s.DestinationCode}",23,true);
            var dep=Label(card,"Local departure",24,66,col,36,s.DepartureLabel,24,true);
            var arr=Label(card,"Local arrival",col+48,66,col,36,s.ArrivalLabel,24,true);
            foreach(var t in new[]{dep,arr}){t.enableAutoSizing=true;t.fontSizeMin=18;t.fontSizeMax=24;t.textWrappingMode=TextWrappingModes.NoWrap;}
            var from=TravelApp.FlightMap.TravelLocationCatalog.Find(s.OriginCode);var to=TravelApp.FlightMap.TravelLocationCatalog.Find(s.DestinationCode);
            var fromLabel=Label(card,"Departure airport",24,108,col,100,$"{from?.city} ({s.OriginCode})\n{from?.airport}\n{s.DepartureTerminal}",18,false,Muted);
            var toLabel=Label(card,"Arrival airport",col+48,108,col,100,$"{to?.city} ({s.DestinationCode})\n{to?.airport}\n{s.ArrivalTerminal}",18,false,Muted);
            foreach(var airportLabel in new[]{fromLabel,toLabel})if(airportLabel.font==null)airportLabel.font=TMP_Settings.defaultFontAsset;
            float airportHeight=Mathf.Max(72,Mathf.Max(fromLabel.GetPreferredValues(fromLabel.text,col,0).y,toLabel.GetPreferredValues(toLabel.text,col,0).y)+8);
            foreach(var airportLabel in new[]{fromLabel,toLabel})airportLabel.rectTransform.sizeDelta=new Vector2(col,airportHeight);
            float metadataY=108+airportHeight+18;
            Label(card,"Flight identity",24,metadataY,inner,30,$"{s.Airline} · {s.FlightNumber}",20,true);
            Label(card,"Aircraft and cabin",24,metadataY+36,inner,54,$"{FlightResultsPage.DurationLabel(s.DurationMinutes)} · {s.AircraftType}\nCabin · {cabin}",18);
            Label(card,"Amenities heading",24,metadataY+96,inner,26,"Amenities",18,true);
            var amenities=Label(card,"Configured amenities",24,metadataY+128,inner,60,string.Join(" · ",s.Amenities),18,false,Muted);
            if(amenities.font==null)amenities.font=TMP_Settings.defaultFontAsset;
            float amenityHeight=Mathf.Max(28,amenities.GetPreferredValues(amenities.text,inner,0).y+8);amenities.rectTransform.sizeDelta=new Vector2(inner,amenityHeight);
            float bottom=metadataY+128+amenityHeight+14;
            if(seats!=null)
            {
                var assigned=Label(card,"Seat assignments",24,bottom,inner,60,string.Join(" · ",seats[i].Select((seat,t)=>$"Traveler {t+1}: Seat {seat}")),18);
                if(assigned.font==null)assigned.font=TMP_Settings.defaultFontAsset;
                float seatHeight=assigned.GetPreferredValues(assigned.text,inner,0).y+12;assigned.rectTransform.sizeDelta=new Vector2(inner,seatHeight);bottom+=seatHeight;
            }
            else {Label(card,"Seat assignment",24,bottom,inner,28,"Seats · choose after selecting this itinerary",17,false,Muted);bottom+=34;}
            float height=bottom+22;((RectTransform)card.parent).sizeDelta=new Vector2(width,height);card.sizeDelta=new Vector2(width-3,height-3);y+=height+18;
            if(i<flight.Layovers.Count)
            {
                var stop=flight.Layovers[i];var connection=style.Shape("Layover "+(i+1),parent,0,y,width,72,Pale,2);
                Label(connection,"Layover information",24,18,inner,44,stop.Description,20,true,Blue);y+=90;
            }
        }
        var baggage=style.Panel("Baggage and fare",parent,0,y,width,236);
        Label(baggage,"Baggage heading",24,18,inner,30,"Baggage",23,true);
        Label(baggage,"Baggage allowances",24,62,inner,50,flight.Baggage.Summary,18);
        Label(baggage,"Fare summary",24,116,inner,52,$"{cabin} · ${fare:N0} per traveler\nEstimated total ${fare*passengers:N0} · {passengers} traveler{(passengers==1?"":"s")}",19,true);
        Label(baggage,"Baggage policy note",24,178,inner,72,FlightResultsPage.BaggageAllowance.PolicyNote,16,false,Muted);
        float finalHeight=width<700?280:250;((RectTransform)baggage.parent).sizeDelta=new Vector2(width,finalHeight);baggage.sizeDelta=new Vector2(width-3,finalHeight-3);y+=finalHeight+18;
        var note=Label(parent,"Illustrative details",0,y,width,76,"Airport-local times. Day offsets use each airport’s local calendar date.\nAircraft, terminals and amenities are illustrative, not airline guarantees.",16,false,Muted);
        if(note.font==null)note.font=TMP_Settings.defaultFontAsset;
        float noteHeight=note.GetPreferredValues(note.text,width,0).y+24;note.rectTransform.sizeDelta=new Vector2(width,noteHeight);y+=noteHeight;
        parent.sizeDelta=new Vector2(0,y);return y;
    }
    public static void Clear(Transform root)
    {foreach(Transform child in root){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}}
}
