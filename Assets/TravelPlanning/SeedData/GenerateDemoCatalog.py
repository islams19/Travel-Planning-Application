"""Optional maintainer tool: recreates catalog.json, replacing hand edits. Python 3.9+."""
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path

# Offsets below apply only to June 2027. The C# importer independently checks the
# full Windows time zone rules against every departure's UTC/local date.
cities = '''nyc|New York City|United States|North America|JFK|Eastern Standard Time|-4
vancouver|Vancouver|Canada|North America|YVR|Pacific Standard Time|-7
cancun|Cancun|Mexico|North America|CUN|Eastern Standard Time (Mexico)|-5
rio|Rio de Janeiro|Brazil|South America|GIG|E. South America Standard Time|-3
london|London|United Kingdom|Europe|LHR|GMT Standard Time|1
paris|Paris|France|Europe|CDG|Romance Standard Time|2
rome|Rome|Italy|Europe|FCO|W. Europe Standard Time|2
capetown|Cape Town|South Africa|Africa|CPT|South Africa Standard Time|2
dubai|Dubai|United Arab Emirates|Middle East|DXB|Arabian Standard Time|4
tokyo|Tokyo|Japan|Asia|HND|Tokyo Standard Time|9
singapore|Singapore|Singapore|Asia|SIN|Singapore Standard Time|8
sydney|Sydney|Australia|Oceania|SYD|AUS Eastern Standard Time|10'''

# Real place names; all associated prices/reviews are fictional, labeled data.
venues = '''nyc|The Plaza;New York Marriott Marquis;Park Central Hotel New York|Katz's Delicatessen;Le Bernardin;Joe's Pizza|Empire State Building Observatory;The Metropolitan Museum of Art;Statue of Liberty National Monument|Central Park;Times Square;Brooklyn Bridge
vancouver|Fairmont Hotel Vancouver;Pan Pacific Vancouver;Hyatt Regency Vancouver|Miku Vancouver;Blue Water Cafe;Vij's|Vancouver Aquarium;Capilano Suspension Bridge Park;Grouse Mountain|Stanley Park;Granville Island;Gastown Steam Clock
cancun|Hyatt Ziva Cancun;JW Marriott Cancun Resort and Spa;Fiesta Americana Condesa Cancun|La Habichuela Cancun;Lorenzillo's Cancun;Puerto Madero Cancun|Museo Maya de Cancun;Interactive Aquarium Cancun;El Rey Archaeological Zone|Playa Delfines;Mercado 28;Parque de las Palapas
rio|Copacabana Palace;Hilton Rio de Janeiro Copacabana;Fairmont Rio de Janeiro Copacabana|Confeitaria Colombo;Marius Degustare;Aprazivel|Christ the Redeemer;Sugarloaf Mountain Cable Car;Museum of Tomorrow|Copacabana Beach;Ipanema Beach;Escadaria Selaron
london|The Savoy;The Ritz London;Park Plaza Westminster Bridge London|Dishoom Covent Garden;Rules London;The Wolseley|Tower of London;London Eye;Westminster Abbey|Hyde Park;Trafalgar Square;Covent Garden
paris|Ritz Paris;Pullman Paris Tour Eiffel;Hotel Lutetia|Le Train Bleu;Bouillon Chartier Grands Boulevards;Les Deux Magots|Louvre Museum;Musee d'Orsay;Eiffel Tower|Jardin du Luxembourg;Place des Vosges;Montmartre
rome|Hotel de Russie;Rome Cavalieri;The St. Regis Rome|Roscioli Salumeria con Cucina;Armando al Pantheon;Pizzarium Bonci|Colosseum;Vatican Museums;Borghese Gallery|Trevi Fountain;Piazza Navona;Spanish Steps
capetown|The Table Bay Hotel;Cape Grace;Mount Nelson A Belmond Hotel|The Pot Luck Club;The Test Kitchen Fledgelings;La Colombe|Table Mountain Aerial Cableway;Robben Island Museum;Two Oceans Aquarium|Victoria and Alfred Waterfront;Camps Bay Beach;Bo-Kaap
dubai|Atlantis The Palm;Jumeirah Burj Al Arab;Armani Hotel Dubai|Al Fanar Restaurant Dubai Festival City;Ravi Restaurant Satwa;Pierchic|At the Top Burj Khalifa;Museum of the Future;Dubai Aquarium and Underwater Zoo|Dubai Marina;Al Fahidi Historical Neighbourhood;Kite Beach
tokyo|Imperial Hotel Tokyo;The Tokyo Station Hotel;Keio Plaza Hotel Tokyo|Gonpachi Nishiazabu;Tsukiji Sushidai;Afuri Harajuku|Tokyo National Museum;Tokyo Skytree;teamLab Planets TOKYO|Senso-ji;Shibuya Crossing;Ueno Park
singapore|Marina Bay Sands;Raffles Singapore;The Fullerton Hotel Singapore|JUMBO Seafood Riverside Point;Lau Pa Sat;Candlenut|Singapore Zoo;Gardens by the Bay;Singapore Flyer|Merlion Park;Chinatown Singapore;Singapore Botanic Gardens
sydney|Park Hyatt Sydney;Shangri-La Sydney;Four Seasons Hotel Sydney|Quay Restaurant;Bennelong;Icebergs Dining Room and Bar|Sydney Opera House Tour;Taronga Zoo Sydney;SEA LIFE Sydney Aquarium|Bondi Beach;The Rocks;Royal Botanic Garden Sydney'''

airlines = 'AA|American Airlines;AC|Air Canada;UA|United Airlines;LA|LATAM Airlines;BA|British Airways;AF|Air France;AZ|ITA Airways;EK|Emirates;SQ|Singapore Airlines;JL|Japan Airlines;QF|Qantas;VS|Virgin Atlantic'
routes = '''JFK|LHR|BA|AA|420|52000
JFK|CDG|AF|AA|440|56000
JFK|YVR|AC|UA|360|32000
JFK|CUN|AA|UA|250|28000
JFK|GIG|AA|LA|600|72000
LHR|FCO|BA|AZ|150|18000
LHR|CPT|BA|VS|690|82000
LHR|DXB|BA|EK|420|48000
DXB|SIN|EK|SQ|450|53000
HND|SIN|JL|SQ|430|47000
HND|SYD|JL|QF|580|75000
SIN|SYD|SQ|QF|480|61000'''

data = dict(catalogVersion='demo-2027-06-v1', startDate='2027-06-01', endDate='2027-06-30',
            destinations=[], airports=[], airlines=[], flights=[], places=[], reviews=[])
by_city, offsets = {}, {}
for row in cities.splitlines():
    key, name, country, region, airport, zone, offset = row.split('|')
    by_city[key] = (name, country)
    offsets[airport] = int(offset)
    data['destinations'].append(dict(id=key, name=name, country=country, region=region,
        description=f'Explore {name} using this offline June 2027 demonstration catalog.'))
    data['airports'].append(dict(id=airport, destinationId=key, name=f'{name} airport ({airport})', timeZone=zone))
for row in airlines.split(';'):
    key, name = row.split('|')
    data['airlines'].append(dict(id=key, name=name))
from urllib.parse import quote
units = ['per room/night', 'per person sample meal budget', 'per adult sample activity budget', 'public-area visit; optional services excluded']
for row in venues.splitlines():
    key, *groups = row.split('|')
    city, country = by_city[key]
    for category, names in enumerate(groups):
        kind = ['hotel', 'restaurant', 'experience', 'hotspot'][category]
        for i, name in enumerate(names.split(';')):
            place_id = f'{key}-{kind}-{i+1}'
            price = [16000+i*8500, 2500+i*2200, 3000+i*1800, 0][category]
            data['places'].append(dict(id=place_id, kind=kind, destinationId=key, name=name,
                description=f'DEMO budget {units[category]}. Fictional USD price, not a quote or availability claim.',
                address=f'{city}, {country} (city-level reference only; verify street address on Maps)',
                priceCents=price, googleMapsUrl='https://www.google.com/maps/search/?api=1&query='+quote(name+' '+city)))
            for review in (1, 2):
                data['reviews'].append(dict(id=f'{place_id}-review-{review}', placeId=place_id, kind=kind,
                    travelerName=f'Demo traveler {review}', rating=3+(i+review)%3,
                    body='FICTIONAL DEMO REVIEW: '+['An enjoyable stop in our sample itinerary.', 'Leave extra time in the sample schedule.'][review-1]+' Authored test data, not a Google review or real traveler statement.'))
for route_number, row in enumerate(routes.splitlines(), 1):
    a, b, airline_a, airline_b, minutes, cents = row.split('|')
    for direction, (origin, destination) in enumerate(((a,b),(b,a))):
        for day in range(1, 31):
            for variant, airline in enumerate((airline_a, airline_b)):
                local = datetime(2027, 6, day, 8+variant*9, tzinfo=timezone(timedelta(hours=offsets[origin])))
                departure = local.astimezone(timezone.utc)
                arrival = departure+timedelta(minutes=int(minutes)+direction*15)
                data['flights'].append(dict(id=f'{origin}-{destination}-202706{day:02d}-{airline}',
                    airlineId=airline, flightNumber=f'{airline}{route_number*100+direction*10+variant}',
                    originAirportId=origin, destinationAirportId=destination,
                    departureUtc=departure.strftime('%Y-%m-%dT%H:%M:%SZ'), arrivalUtc=arrival.strftime('%Y-%m-%dT%H:%M:%SZ'),
                    departureLocalDate=local.strftime('%Y-%m-%d'), priceCents=int(cents)+variant*3500+(day%7)*700,
                    availableSeats=9+day%6))
Path(__file__).with_name('catalog.json').write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print({key:len(value) for key,value in data.items() if isinstance(value,list)})
