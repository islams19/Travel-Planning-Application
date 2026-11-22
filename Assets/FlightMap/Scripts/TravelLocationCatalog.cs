using System;
using System.Collections.Generic;
using System.Linq;

namespace TravelApp.FlightMap
{
    // Shared airport metadata and canonical booking labels. Both Explore and Search Flights consume
    // this table; map-only legacy airports retain a null BookingDestination. Coordinates: OurAirports
    // public-domain airports.csv, retrieved 2026-11-21. See Documentation/LocationCatalog.md.
    public static class TravelLocationCatalog
    {
        public static readonly IReadOnlyList<FlightLocation> Airports = Array.AsReadOnly(new[]
        {
            new FlightLocation("JFK", "John F. Kennedy International Airport", "New York", "United States", 40.6394470, -73.7793170, "New York, New York", "America/New_York"),
            new FlightLocation("LAX", "Los Angeles International Airport", "Los Angeles", "United States", 33.9425010, -118.4079970, "Los Angeles, California", "America/Los_Angeles"),
            new FlightLocation("MIA", "Miami International Airport", "Miami", "United States", 25.7960110, -80.2897510, "Miami, Florida", "America/New_York"),
            new FlightLocation("ORD", "Chicago O'Hare International Airport", "Chicago", "United States", 41.9786000, -87.9048000, "Chicago, Illinois", "America/Chicago"),
            new FlightLocation("ATL", "Hartsfield Jackson Atlanta International Airport", "Atlanta", "United States", 33.6367000, -84.4281010, null, "America/New_York"),
            new FlightLocation("SFO", "San Francisco International Airport", "San Francisco", "United States", 37.6198060, -122.3748210, "San Francisco, California", "America/Los_Angeles"),
            new FlightLocation("SEA", "Seattle–Tacoma International Airport", "Seattle", "United States", 47.4479430, -122.3102760, null, "America/Los_Angeles"),
            new FlightLocation("DFW", "Dallas Fort Worth International Airport", "Dallas", "United States", 32.8968010, -97.0380020, null, "America/Chicago"),
            new FlightLocation("BOS", "Boston Logan International Airport", "Boston", "United States", 42.3619700, -71.0079000, "Boston, Massachusetts", "America/New_York"),
            new FlightLocation("IAD", "Washington Dulles International Airport", "Washington", "United States", 38.9445000, -77.4558030, null, "America/New_York"),
            new FlightLocation("YYZ", "Toronto Pearson International Airport", "Toronto", "Canada", 43.6759350, -79.6294210, "Toronto, Canada", "America/Toronto"),
            new FlightLocation("MEX", "Mexico City Benito Juárez International Airport", "Mexico City", "Mexico", 19.4358220, -99.0703300, "Mexico City, Mexico", "America/Mexico_City"),
            new FlightLocation("LHR", "London Heathrow Airport", "London", "United Kingdom", 51.4707480, -0.4599090, "London, United Kingdom", "Europe/London"),
            new FlightLocation("CDG", "Charles de Gaulle International Airport", "Paris", "France", 49.0089600, 2.5541170, "Paris, France", "Europe/Paris"),
            new FlightLocation("FRA", "Frankfurt Main Airport", "Frankfurt", "Germany", 50.0267060, 8.5583500, null, "Europe/Berlin"),
            new FlightLocation("DXB", "Dubai International Airport", "Dubai", "United Arab Emirates", 25.2497900, 55.3709920, "Dubai, United Arab Emirates", "Asia/Dubai"),
            new FlightLocation("DEL", "Indira Gandhi International Airport", "Delhi", "India", 28.5556300, 77.0951900, "Delhi, India", "Asia/Kolkata"),
            new FlightLocation("HND", "Tokyo Haneda International Airport", "Tokyo", "Japan", 35.5496780, 139.7869580, "Tokyo, Japan", "Asia/Tokyo"),
            new FlightLocation("SIN", "Singapore Changi Airport", "Singapore", "Singapore", 1.3501900, 103.9940030, "Singapore", "Asia/Singapore"),
            new FlightLocation("SYD", "Sydney Kingsford Smith International Airport", "Sydney", "Australia", -33.9460980, 151.1770020, "Sydney, Australia", "Australia/Sydney"),
            new FlightLocation("GRU", "São Paulo/Guarulhos–Governor André Franco Montoro International Airport", "São Paulo", "Brazil", -23.4312740, -46.4699540, "São Paulo, Brazil", "America/Sao_Paulo"),
            new FlightLocation("JNB", "O.R. Tambo International Airport", "Johannesburg", "South Africa", -26.1400810, 28.2468010, "Johannesburg, South Africa", "Africa/Johannesburg"),
            new FlightLocation("AMS", "Amsterdam Airport Schiphol", "Amsterdam", "Netherlands", 52.3086010, 4.7638900, "Amsterdam, Netherlands", "Europe/Amsterdam"),
            new FlightLocation("ATH", "Athens Eleftherios Venizelos International Airport", "Athens", "Greece", 37.9364010, 23.9445000, "Athens, Greece", "Europe/Athens"),
            new FlightLocation("AKL", "Auckland International Airport", "Auckland", "New Zealand", -37.0119900, 174.7863310, "Auckland, New Zealand", "Pacific/Auckland"),
            new FlightLocation("DPS", "Denpasar I Gusti Ngurah Rai International Airport", "Bali", "Indonesia", -8.7484090, 115.1671230, "Bali, Indonesia", "Asia/Makassar"),
            new FlightLocation("BKK", "Suvarnabhumi Airport", "Bangkok", "Thailand", 13.6811000, 100.7470020, "Bangkok, Thailand", "Asia/Bangkok"),
            new FlightLocation("BCN", "Josep Tarradellas Barcelona-El Prat Airport", "Barcelona", "Spain", 41.2971000, 2.0784600, "Barcelona, Spain", "Europe/Madrid"),
            new FlightLocation("PEK", "Beijing Capital International Airport", "Beijing", "China", 40.0773490, 116.5967020, "Beijing, China", "Asia/Shanghai"),
            new FlightLocation("BER", "Berlin Brandenburg Airport", "Berlin", "Germany", 52.3617380, 13.5023410, "Berlin, Germany", "Europe/Berlin"),
            new FlightLocation("BOG", "El Dorado International Airport", "Bogotá", "Colombia", 4.7015900, -74.1469000, "Bogotá, Colombia", "America/Bogota"),
            new FlightLocation("EZE", "Ezeiza International Airport - Ministro Pistarini", "Buenos Aires", "Argentina", -34.8222000, -58.5358000, "Buenos Aires, Argentina", "America/Argentina/Buenos_Aires"),
            new FlightLocation("CAI", "Cairo International Airport", "Cairo", "Egypt", 30.1115340, 31.3966940, "Cairo, Egypt", "Africa/Cairo"),
            new FlightLocation("CUN", "Cancún International Airport", "Cancún", "Mexico", 21.0408170, -86.8734700, "Cancún, Mexico", "America/Cancun"),
            new FlightLocation("CPT", "Cape Town International Airport", "Cape Town", "South Africa", -33.9740300, 18.6043330, "Cape Town, South Africa", "Africa/Johannesburg"),
            new FlightLocation("CPH", "Copenhagen Kastrup Airport", "Copenhagen", "Denmark", 55.6179008, 12.6560001, "Copenhagen, Denmark", "Europe/Copenhagen"),
            new FlightLocation("DOH", "Hamad International Airport", "Doha", "Qatar", 25.2730560, 51.6080560, "Doha, Qatar", "Asia/Qatar"),
            new FlightLocation("DUB", "Dublin Airport", "Dublin", "Ireland", 53.4287130, -6.2621210, "Dublin, Ireland", "Europe/Dublin"),
            new FlightLocation("HKG", "Hong Kong International Airport", "Hong Kong", "Hong Kong", 22.3118400, 113.9148620, "Hong Kong", "Asia/Hong_Kong"),
            new FlightLocation("IST", "İstanbul Airport", "Istanbul", "Türkiye", 41.2748740, 28.7321360, "Istanbul, Türkiye", "Europe/Istanbul"),
            new FlightLocation("CGK", "Soekarno-Hatta International Airport", "Jakarta", "Indonesia", -6.1255700, 106.6559980, "Jakarta, Indonesia", "Asia/Jakarta"),
            new FlightLocation("KUL", "Kuala Lumpur International Airport", "Kuala Lumpur", "Malaysia", 2.7455800, 101.7099990, "Kuala Lumpur, Malaysia", "Asia/Kuala_Lumpur"),
            new FlightLocation("LOS", "Murtala Muhammed International Airport", "Lagos", "Nigeria", 6.5773700, 3.3211600, "Lagos, Nigeria", "Africa/Lagos"),
            new FlightLocation("LIM", "Jorge Chávez International Airport", "Lima", "Peru", -12.0219000, -77.1143050, "Lima, Peru", "America/Lima"),
            new FlightLocation("LIS", "Lisbon Humberto Delgado Airport", "Lisbon", "Portugal", 38.7813000, -9.1359200, "Lisbon, Portugal", "Europe/Lisbon"),
            new FlightLocation("MAD", "Adolfo Suárez Madrid–Barajas Airport", "Madrid", "Spain", 40.4934070, -3.5722490, "Madrid, Spain", "Europe/Madrid"),
            new FlightLocation("MNL", "Ninoy Aquino International Airport", "Manila", "Philippines", 14.5086000, 121.0199970, "Manila, Philippines", "Asia/Manila"),
            new FlightLocation("RAK", "Marrakesh Menara Airport", "Marrakech", "Morocco", 31.6048070, -8.0357880, "Marrakech, Morocco", "Africa/Casablanca"),
            new FlightLocation("MEL", "Melbourne Airport", "Melbourne", "Australia", -37.6707320, 144.8378980, "Melbourne, Australia", "Australia/Melbourne"),
            new FlightLocation("MXP", "Milan Malpensa International Airport", "Milan", "Italy", 45.6306000, 8.7281100, "Milan, Italy", "Europe/Rome"),
            new FlightLocation("YUL", "Montreal / Pierre Elliott Trudeau International Airport", "Montreal", "Canada", 45.4678370, -73.7422940, "Montreal, Canada", "America/Toronto"),
            new FlightLocation("BOM", "Chhatrapati Shivaji Maharaj International Airport", "Mumbai", "India", 19.0886990, 72.8678970, "Mumbai, India", "Asia/Kolkata"),
            new FlightLocation("NBO", "Jomo Kenyatta International Airport", "Nairobi", "Kenya", -1.3188860, 36.9282330, "Nairobi, Kenya", "Africa/Nairobi"),
            new FlightLocation("KIX", "Kansai International Airport", "Osaka", "Japan", 34.4272990, 135.2440030, "Osaka, Japan", "Asia/Tokyo"),
            new FlightLocation("PRG", "Václav Havel Airport Prague", "Prague", "Czechia", 50.1008740, 14.2599110, "Prague, Czechia", "Europe/Prague"),
            new FlightLocation("GIG", "Rio de Janeiro Galeão – Tom Jobim International Airport", "Rio de Janeiro", "Brazil", -22.8099990, -43.2505570, "Rio de Janeiro, Brazil", "America/Sao_Paulo"),
            new FlightLocation("FCO", "Rome–Fiumicino Leonardo da Vinci International Airport", "Rome", "Italy", 41.8045320, 12.2519980, "Rome, Italy", "Europe/Rome"),
            new FlightLocation("SCL", "Comodoro Arturo Merino Benítez International Airport", "Santiago", "Chile", -33.3902520, -70.7946970, "Santiago, Chile", "America/Santiago"),
            new FlightLocation("JTR", "Santorini International Airport", "Santorini", "Greece", 36.4000450, 25.4786380, "Santorini, Greece", "Europe/Athens"),
            new FlightLocation("ICN", "Incheon International Airport", "Seoul", "South Korea", 37.4691010, 126.4509960, "Seoul, South Korea", "Asia/Seoul"),
            new FlightLocation("PVG", "Shanghai Pudong International Airport", "Shanghai", "China", 31.1434000, 121.8050000, "Shanghai, China", "Asia/Shanghai"),
            new FlightLocation("ARN", "Stockholm-Arlanda Airport", "Stockholm", "Sweden", 59.6484900, 17.9288290, "Stockholm, Sweden", "Europe/Stockholm"),
            new FlightLocation("TPE", "Taiwan Taoyuan International Airport", "Taipei", "Taiwan", 25.0777000, 121.2330020, "Taipei, Taiwan", "Asia/Taipei"),
            new FlightLocation("YVR", "Vancouver International Airport", "Vancouver", "Canada", 49.1939010, -123.1839980, "Vancouver, Canada", "America/Vancouver"),
            new FlightLocation("VIE", "Vienna International Airport", "Vienna", "Austria", 48.1102980, 16.5697000, "Vienna, Austria", "Europe/Vienna"),
            new FlightLocation("WAW", "Warsaw Chopin Airport", "Warsaw", "Poland", 52.1656990, 20.9671000, "Warsaw, Poland", "Europe/Warsaw"),
            new FlightLocation("ZRH", "Zürich Airport", "Zurich", "Switzerland", 47.4580560, 8.5480560, "Zurich, Switzerland", "Europe/Zurich"),
            new FlightLocation("EWR", "Newark Liberty International Airport", "New York", "United States", 40.6894000, -74.1705450, "New York, New York", "America/New_York"),
            new FlightLocation("LGA", "LaGuardia Airport", "New York", "United States", 40.7771990, -73.8725970, "New York, New York", "America/New_York"),
            new FlightLocation("LGW", "London Gatwick Airport", "London", "United Kingdom", 51.1487440, -0.1857390, "London, United Kingdom", "Europe/London"),
            new FlightLocation("ORY", "Paris-Orly Airport", "Paris", "France", 48.7294990, 2.3589630, "Paris, France", "Europe/Paris"),
            new FlightLocation("NRT", "Narita International Airport", "Tokyo", "Japan", 35.7685800, 140.3887140, "Tokyo, Japan", "Asia/Tokyo"),
            new FlightLocation("PKX", "Beijing Daxing International Airport", "Beijing", "China", 39.5012890, 116.4139670, "Beijing, China", "Asia/Shanghai"),
            new FlightLocation("SHA", "Shanghai Hongqiao International Airport", "Shanghai", "China", 31.1981040, 121.3342600, "Shanghai, China", "Asia/Shanghai"),
            new FlightLocation("DMK", "Don Mueang International Airport", "Bangkok", "Thailand", 13.9126000, 100.6070020, "Bangkok, Thailand", "Asia/Bangkok"),
            new FlightLocation("GMP", "Seoul Gimpo International Airport", "Seoul", "South Korea", 37.5583000, 126.7910000, "Seoul, South Korea", "Asia/Seoul"),
            new FlightLocation("ITM", "Osaka Itami International Airport", "Osaka", "Japan", 34.7808530, 135.4408290, "Osaka, Japan", "Asia/Tokyo"),
            new FlightLocation("SAW", "Istanbul Sabiha Gökçen International Airport", "Istanbul", "Türkiye", 40.8986020, 29.3092000, "Istanbul, Türkiye", "Europe/Istanbul"),
            new FlightLocation("LIN", "Milano Linate Airport", "Milan", "Italy", 45.4450990, 9.2767400, "Milan, Italy", "Europe/Rome"),
            new FlightLocation("CIA", "Ciampino–G. B. Pastine International Airport", "Rome", "Italy", 41.7987690, 12.5953310, "Rome, Italy", "Europe/Rome"),
            new FlightLocation("AEP", "Aeroparque Jorge Newbery", "Buenos Aires", "Argentina", -34.5594190, -58.4155360, "Buenos Aires, Argentina", "America/Argentina/Buenos_Aires"),
            new FlightLocation("CGH", "Congonhas–Deputado Freitas Nobre Airport", "São Paulo", "Brazil", -23.6276570, -46.6546010, "São Paulo, Brazil", "America/Sao_Paulo"),
            new FlightLocation("TSA", "Taipei Songshan International Airport", "Taipei", "Taiwan", 25.0672440, 121.5528220, "Taipei, Taiwan", "Asia/Taipei"),
        });
        public static readonly IReadOnlyList<string> BookingDestinations = Array.AsReadOnly(Airports
            .Where(location => location.BookingDestination != null).Select(location => location.BookingDestination)
            .Distinct().OrderBy(label => label, StringComparer.InvariantCulture).ToArray());
        public static FlightLocation Find(string code) => string.IsNullOrWhiteSpace(code) ? null : Airports
            .FirstOrDefault(location => string.Equals(location.code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
