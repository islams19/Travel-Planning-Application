using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEngine;

// The existing booking's passenger state: counts, traveler details and contact travel together.
[Serializable]
public sealed class PassengerParty
{
    public enum Category { Adult, Child, InfantInSeat, InfantOnLap }
    [Serializable] public sealed class Traveler
    {
        public Category category;
        public string firstName="",lastName="";
        public bool RequiresSeat => category!=Category.InfantOnLap;
        public string Name => ((firstName??"")+" "+(lastName??"")).Trim();
    }
    [Serializable] public sealed class FareRates
    {
        // Configurable illustrative percentages, applied after the shared cabin adjustment.
        public int adult=100,child=75,infantInSeat=75,infantOnLap=10;
        public int For(Category category)=>category==Category.Adult?adult:category==Category.Child?child:category==Category.InfantInSeat?infantInSeat:infantOnLap;
    }
    public List<Traveler> travelers=new List<Traveler>{new Traveler()};
    public string email="",phone="";
    public FareRates rates=new FareRates();
    public int Count => travelers.Count;
    public int SeatCount => travelers.Count(t=>t.RequiresSeat);
    public int CountFor(Category category)=>travelers.Count(t=>t.category==category);
    public bool IsValid => travelers!=null && Count>=1 && Count<=6 && travelers.All(t=>t!=null) && CountFor(Category.Adult)>=1 && CountFor(Category.InfantOnLap)<=CountFor(Category.Adult) && travelers.All(t=>t!=null && Enum.IsDefined(typeof(Category),t.category));
    public string SeatSignature => string.Join(",",travelers.Select(t=>(int)t.category));
    public string PricingSignature => SeatSignature+":"+JsonUtility.ToJson(rates);
    public IReadOnlyList<Traveler> Seated => travelers.Where(t=>t.RequiresSeat).ToList().AsReadOnly();
    public static PassengerParty Adults(int count)=>new PassengerParty{travelers=Enumerable.Range(0,Mathf.Clamp(count,1,6)).Select(_=>new Traveler()).ToList()};
    public static PassengerParty Restore(PassengerParty party,int legacyCount)=>party!=null && party.IsValid?party.Clone():Adults(legacyCount);
    public PassengerParty Clone()=>JsonUtility.FromJson<PassengerParty>(JsonUtility.ToJson(this));
    public bool SetCounts(int adults,int children,int infantsInSeat,int infantsOnLap)
    {
        if(adults<1 || children<0 || infantsInSeat<0 || infantsOnLap<0 || infantsOnLap>adults || adults+children+infantsInSeat+infantsOnLap>6)return false;
        var next=new List<Traveler>();int[] counts={adults,children,infantsInSeat,infantsOnLap};
        for(int i=0;i<4;i++)
        {
            var category=(Category)i;var existing=travelers.Where(t=>t.category==category).ToList();
            for(int n=0;n<counts[i];n++)next.Add(n<existing.Count?existing[n]:new Traveler{category=category});
        }
        travelers=next;return true;
    }
    public int FareFor(int adultFare,Category category)=>(int)Math.Round(adultFare*(rates??new FareRates()).For(category)/100m,MidpointRounding.AwayFromZero);
    public int TotalFare(int adultFare)=>travelers.Sum(t=>FareFor(adultFare,t.category));
    public string Breakdown(int adultFare)=>string.Join(" · ",Enum.GetValues(typeof(Category)).Cast<Category>().Where(c=>CountFor(c)>0).Select(c=>$"{CountFor(c)} {Label(c).ToLowerInvariant()} × ${FareFor(adultFare,c):N0}"));
    public string CategoriesSummary=>string.Join(" · ",Enum.GetValues(typeof(Category)).Cast<Category>().Where(c=>CountFor(c)>0).Select(c=>$"{CountFor(c)} {Plural(c,CountFor(c))}"));
    public static string Plural(Category c,int count)=>count==1?Label(c).ToLowerInvariant():c==Category.Child?"children":c==Category.Adult?"adults":c==Category.InfantInSeat?"infants in seats":"infants on lap";
    public static string Label(Category c)=>c==Category.Adult?"Adult":c==Category.Child?"Child":c==Category.InfantInSeat?"Infant in seat":"Infant on lap";
    public static string Age(Category c)=>c==Category.Adult?"12+":c==Category.Child?"2–11":"Under 2";
    public static string NameError(string value)
    {
        value=(value??"").Trim();
        if(value.Length<1 || value.Length>80 || !value.Any(char.IsLetter) || value.Any(c=>!char.IsLetter(c) && char.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark && c!=' ' && c!='-' && c!='\'' && c!='’'))return "Enter a valid name.";
        return null;
    }
    public static string PhoneError(string value)
    {
        value=value??"";int digits=value.Count(c=>c>='0'&&c<='9');
        return digits<7 || digits>15 || value.Any(c=>!char.IsDigit(c)&&!" +()- .".Contains(c))?"Use 7–15 digits.":null;
    }
    public bool HasValidDetails=>IsValid && travelers.All(t=>NameError(t.firstName)==null && NameError(t.lastName)==null) && TravelApp.EmailAddressValidator.IsValid(email) && PhoneError(phone)==null;
    public string DetailsSummary=>string.Join("\n",travelers.Select((t,i)=>$"Traveler {i+1}: {(t.Name.Length==0?"Name not entered":t.Name)} · {Label(t.category)}"))+"\nContact: "+email+" · "+phone;
}
