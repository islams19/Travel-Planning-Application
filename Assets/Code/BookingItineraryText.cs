using System.Collections.Generic;
using System.Linq;
using System.Text;

// Shared presentation of the existing flight/booking snapshots, never a separate booking model.
public static class BookingItineraryText
{
    public static string Describe(string airline,IReadOnlyList<FlightResultsPage.Segment> segments,
        IReadOnlyList<FlightResultsPage.Layover> layovers,IReadOnlyList<IReadOnlyList<string>> segmentSeats,
        string bookingCabin,int fare,int passengers,FlightResultsPage.BaggageAllowance baggage=null,PassengerParty party=null)
    {
        string Seats(int index)=>string.Join(" · ",segmentSeats[index].Select((seat,traveler)=>
            (passengers>1 ? $"Traveler {traveler+1}: " : "")+"Seat "+(seat ?? "Choose")));
        int duration=(int)(segments[segments.Count-1].ArrivalUtc-segments[0].DepartureUtc).TotalMinutes;
        string carriers=FlightResultsPage.CarrierLabel(airline,segments);
        string Operator(FlightResultsPage.Segment segment)=>segment.Airline!=airline && !string.IsNullOrEmpty(segment.Airline) ? segment.Airline+" " : "";
        string cabin=string.IsNullOrEmpty(bookingCabin) ? "" : bookingCabin+" · ";
        string bags=baggage==null ? "" : "\n"+baggage.Summary+" · Illustrative allowance";
        string price=$"${fare:N0} per adult · Itinerary total ${(party?.TotalFare(fare)??fare*passengers):N0}";
        if(party!=null)price+="\n"+party.Breakdown(fare);
        if(segments.Count==1)
        {
            var segment=segments[0];
            return $"{carriers} · Nonstop\n{segment.OriginCode} → {segment.DestinationCode} · {segment.DepartureLabel} – {segment.ArrivalLabel}\n"+
                "Seats: "+string.Join(", ",segmentSeats[0])+(party?.CountFor(PassengerParty.Category.InfantOnLap)>0?" · Lap infant: no separate seat":"")+ $"\n{FlightResultsPage.DurationLabel(duration)} total · {cabin}{price}{bags}";
        }
        var text=new StringBuilder($"{carriers} · {layovers.Count} stop{(layovers.Count==1 ? "" : "s")}");
        for(int i=0;i<segments.Count;i++)
        {
            var segment=segments[i];
            text.Append($"\n\n<b>{segment.OriginCode} → {segment.DestinationCode}</b> · {Operator(segment)}{segment.FlightNumber}\n"+
                $"{segment.DepartureLabel} – {segment.ArrivalLabel} · {FlightResultsPage.DurationLabel(segment.DurationMinutes)}\n"+Seats(i));
            if(i<layovers.Count)
            {
                var layover=layovers[i];
                text.Append($"\n{FlightResultsPage.DurationLabel(layover.DurationMinutes)} layover in {layover.City} ({layover.AirportCode})");
            }
        }
        return text+$"\n\n{FlightResultsPage.DurationLabel(duration)} total · {cabin}{price}{bags}";
    }
}
