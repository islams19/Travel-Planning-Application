using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Popular-destination card photos, registered by the search page so other screens can reuse them.
public static class DestinationPhotos
{
    static readonly Dictionary<string,Sprite> photos=new Dictionary<string,Sprite>();
    static string Key(string city)
    {
        if(string.IsNullOrWhiteSpace(city))return "";
        var plain=city.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c));
        return new string(plain.ToArray()).ToLowerInvariant();
    }
    public static void Register(string city,Sprite sprite) {if(sprite!=null && Key(city).Length>0)photos[Key(city)]=sprite;}
    public static Sprite Find(string city) => photos.TryGetValue(Key(city),out var sprite) && sprite!=null ? sprite : null;
    public static Sprite ForAirport(string code) => Find(TravelApp.FlightMap.TravelLocationCatalog.Find(code)?.city);
}
