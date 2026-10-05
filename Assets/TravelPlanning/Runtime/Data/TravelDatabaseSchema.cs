using System;
using SQLite;

namespace TravelPlanning.Data
{
    /// <summary>The database blueprint. Only the seed builder creates this version.</summary>
    public static class TravelDatabaseSchema
    {
        public const int Version = 1;
        public const int ApplicationId = 0x5452504C;
        private const string Targets = "flight_id TEXT REFERENCES flights(id), hotel_id TEXT REFERENCES hotels(id), " +
            "restaurant_id TEXT REFERENCES restaurants(id), experience_id TEXT REFERENCES experiences(id), " +
            "hotspot_id TEXT REFERENCES hotspots(id)";
#region Create
        public static void Create(SQLiteConnection connection)
        {
            if (connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'") != 0)
                throw new InvalidOperationException("Create requires an empty seed database; existing data was not replaced.");
            connection.Execute("PRAGMA foreign_keys = ON");
            connection.RunInTransaction(() =>
            {
                connection.Execute("CREATE TABLE metadata (key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL)");
                connection.Execute(
                    "CREATE TABLE users (id TEXT PRIMARY KEY NOT NULL, email_normalized TEXT NOT NULL UNIQUE " +
                    "CHECK(email_normalized=lower(trim(email_normalized))), " +
                    "password_hash TEXT NOT NULL CHECK(length(password_hash)>0), password_salt TEXT NOT NULL " +
                    "CHECK(length(password_salt)>0), " +
                    "password_iterations INTEGER NOT NULL CHECK(password_iterations>0), password_algorithm TEXT NOT " +
                    "NULL DEFAULT 'PBKDF2-SHA256', created_utc TEXT NOT NULL)");
                connection.Execute(
                    "CREATE TABLE destinations (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL, country TEXT NOT " +
                    "NULL, region TEXT NOT NULL, description TEXT NOT NULL)");
                connection.Execute(
                    "CREATE TABLE airports (id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=3), destination_id TEXT " +
                    "NOT NULL REFERENCES destinations(id), name TEXT NOT NULL, time_zone TEXT NOT NULL)");
                connection.Execute("CREATE TABLE airlines (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL)");
                connection.Execute(
                    "CREATE TABLE flights (id TEXT PRIMARY KEY NOT NULL, airline_id TEXT NOT NULL REFERENCES " +
                    "airlines(id), flight_number TEXT NOT NULL, " +
                    "origin_airport_id TEXT NOT NULL REFERENCES airports(id), destination_airport_id TEXT NOT NULL " +
                    "REFERENCES airports(id), " +
                    "departure_utc TEXT NOT NULL, arrival_utc TEXT NOT NULL CHECK(arrival_utc>departure_utc), " +
                    "departure_local_date TEXT NOT NULL, " +
                    "price_cents INTEGER NOT NULL CHECK(typeof(price_cents)='integer' AND price_cents>=0), " +
                    "available_seats INTEGER NOT NULL CHECK(available_seats>=0), currency TEXT NOT NULL " +
                    "CHECK(currency='USD'), " +
                    "CHECK(origin_airport_id<>destination_airport_id))");
                foreach (string table in new[]
                {
                    "hotels",
                    "restaurants",
                    "experiences",
                    "hotspots"
                }

                )
                    connection.Execute(
                        "CREATE TABLE " +
                        table +
                        " (id TEXT PRIMARY KEY NOT NULL, destination_id TEXT NOT NULL REFERENCES destinations(id), " +
                        "name TEXT NOT NULL, description TEXT NOT NULL, address TEXT NOT NULL, price_cents INTEGER " +
                        "CHECK(price_cents IS NULL OR (typeof(price_cents)='integer' AND price_cents>=0)), " +
                        "currency TEXT NOT NULL CHECK(currency='USD'), google_maps_url TEXT NOT NULL" +
                        (table == "hotels"
                        ? ", CHECK(price_cents IS NOT NULL))"
                        : ")"));
                connection.Execute(
                    "CREATE TABLE reviews (id TEXT PRIMARY KEY NOT NULL, hotel_id TEXT REFERENCES hotels(id), " +
                    "restaurant_id TEXT REFERENCES restaurants(id), " +
                    "experience_id TEXT REFERENCES experiences(id), hotspot_id TEXT REFERENCES hotspots(id), " +
                    "traveler_name TEXT NOT NULL, " +
                    "rating INTEGER NOT NULL CHECK(typeof(rating)='integer' AND rating BETWEEN 1 AND 5), body TEXT NOT NULL, " +
                    "is_demo INTEGER NOT NULL DEFAULT 1 CHECK(is_demo IN (0,1)), " +
                    "CHECK((hotel_id IS NOT NULL)+(restaurant_id IS NOT NULL)+(experience_id IS NOT NULL)+(hotspot_id IS NOT NULL)=1))");
                connection.Execute(
                    "CREATE TABLE trips (id TEXT PRIMARY KEY NOT NULL, user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE, " +
                    "name TEXT NOT NULL, start_date TEXT NOT NULL, end_date TEXT NOT NULL " +
                    "CHECK(end_date>=start_date), created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, " +
                    "UNIQUE(id,user_id))");
                connection.Execute(
                    "CREATE TABLE saved_items (id TEXT PRIMARY KEY NOT NULL, user_id TEXT NOT NULL, trip_id TEXT NOT NULL, " +
                    Targets +
                    ", created_utc TEXT NOT NULL, " +
                    "FOREIGN KEY(trip_id,user_id) REFERENCES trips(id,user_id) ON DELETE CASCADE, " +
                    "CHECK((flight_id IS NOT NULL)+(hotel_id IS NOT NULL)+(restaurant_id IS NOT NULL)+(experience_id " +
                    "IS NOT NULL)+(hotspot_id IS NOT NULL)=1))");
                connection.Execute(
                    "CREATE TABLE price_watches (id TEXT PRIMARY KEY NOT NULL, user_id TEXT NOT NULL REFERENCES " +
                    "users(id) ON DELETE CASCADE, " +
                    "flight_id TEXT REFERENCES flights(id), hotel_id TEXT REFERENCES hotels(id), last_price_cents " +
                    "INTEGER NOT NULL CHECK(typeof(last_price_cents)='integer' AND last_price_cents>=0), active " +
                    "INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)), created_utc TEXT NOT NULL, UNIQUE(id,user_id), " +
                    "CHECK((flight_id IS NOT NULL)+(hotel_id IS NOT NULL)=1))");
                connection.Execute(
                    "CREATE TABLE price_history (id TEXT PRIMARY KEY NOT NULL, flight_id TEXT REFERENCES " +
                    "flights(id), hotel_id TEXT REFERENCES hotels(id), " +
                    "old_price_cents INTEGER NOT NULL CHECK(typeof(old_price_cents)='integer' AND old_price_cents>=0), " +
                    "new_price_cents INTEGER NOT NULL CHECK(typeof(new_price_cents)='integer' AND new_price_cents>=0 " +
                    "AND new_price_cents<>old_price_cents), changed_utc TEXT NOT NULL, " +
                    "CHECK((flight_id IS NOT NULL)+(hotel_id IS NOT NULL)=1))");
                connection.Execute(
                    "CREATE TABLE notifications (id TEXT PRIMARY KEY NOT NULL, user_id TEXT NOT NULL, watch_id TEXT NOT NULL, " +
                    "price_history_id TEXT NOT NULL REFERENCES price_history(id), title TEXT NOT NULL, body TEXT NOT " +
                    "NULL, created_utc TEXT NOT NULL, " +
                    "is_read INTEGER NOT NULL DEFAULT 0 CHECK(is_read IN (0,1)), UNIQUE(watch_id,price_history_id), " +
                    "FOREIGN KEY(watch_id,user_id) REFERENCES price_watches(id,user_id) ON DELETE CASCADE)");
                CreateIndexes(connection);
                // A notification must describe a price change for the item this user watches.
                foreach (string action in new[]
                {
                    "INSERT",
                    "UPDATE"
                }

                )
                    connection.Execute(
                        "CREATE TRIGGER notification_target_" +
                        action.ToLowerInvariant() +
                        " BEFORE " +
                        action +
                        " ON notifications " +
                        "WHEN NOT EXISTS (SELECT 1 FROM price_watches w JOIN price_history h ON h.id=NEW.price_history_id " +
                        "WHERE w.id=NEW.watch_id AND w.user_id=NEW.user_id AND ((w.flight_id IS NOT NULL AND w.flight_id=h.flight_id) OR " +
                        "(w.hotel_id IS NOT NULL AND w.hotel_id=h.hotel_id))) BEGIN SELECT RAISE(ABORT,'Notification " +
                        "target does not match watch'); END");
                // Target identities are permanent. Remove/recreate a watch instead of repointing it.
                connection.Execute(
                    "CREATE TRIGGER watch_target_immutable BEFORE UPDATE OF user_id,flight_id,hotel_id ON price_watches " +
                    "WHEN NEW.user_id<>OLD.user_id OR NEW.flight_id IS NOT OLD.flight_id OR NEW.hotel_id IS NOT OLD.hotel_id " +
                    "BEGIN SELECT RAISE(ABORT,'Watch ownership and target cannot change'); END");
                connection.Execute(
                    "CREATE TRIGGER history_immutable BEFORE UPDATE ON price_history BEGIN SELECT RAISE(ABORT,'Price " +
                    "history is immutable'); END");
                connection.Execute("PRAGMA application_id = " + ApplicationId);
                connection.Execute("PRAGMA user_version = " + Version);
            });
        }
#endregion

#region Create Indexes
        private static void CreateIndexes(SQLiteConnection connection)
        {
            connection.Execute(
                "CREATE INDEX flights_search ON flights(origin_airport_id,destination_airport_id,departure_local_date,price_cents)");
            connection.Execute("CREATE INDEX flights_airline ON flights(airline_id)");
            connection.Execute("CREATE INDEX flights_destination ON flights(destination_airport_id)");
            connection.Execute("CREATE INDEX airports_destination ON airports(destination_id)");
            connection.Execute("CREATE INDEX trips_user ON trips(user_id)");
            connection.Execute("CREATE INDEX saved_items_owner ON saved_items(user_id,trip_id)");
            connection.Execute("CREATE INDEX notifications_unread ON notifications(user_id,is_read,created_utc)");
            connection.Execute("CREATE INDEX notifications_history ON notifications(price_history_id)");
            foreach (string table in new[]
            {
                "hotels",
                "restaurants",
                "experiences",
                "hotspots"
            }

            )
                connection.Execute("CREATE INDEX " + table + "_destination ON " + table + "(destination_id)");
            foreach (string target in new[]
            {
                "flight",
                "hotel",
                "restaurant",
                "experience",
                "hotspot"
            }

            )
            {
                string column = target + "_id";
                connection.Execute(
                    "CREATE UNIQUE INDEX saved_" +
                    target +
                    " ON saved_items(trip_id," +
                    column +
                    ") WHERE " +
                    column +
                    " IS NOT NULL");
                connection.Execute(
                    "CREATE INDEX saved_" +
                    target +
                    "_target ON saved_items(" +
                    column +
                    ") WHERE " +
                    column +
                    " IS NOT NULL");
                if (target != "flight")
                    connection.Execute(
                        "CREATE INDEX reviews_" +
                        target +
                        " ON reviews(" +
                        column +
                        ") WHERE " +
                        column +
                        " IS NOT NULL");
                if (target == "flight" || target == "hotel")
                {
                    connection.Execute(
                        "CREATE UNIQUE INDEX watches_" +
                        target +
                        " ON price_watches(user_id," +
                        column +
                        ") WHERE " +
                        column +
                        " IS NOT NULL");
                    connection.Execute(
                        "CREATE INDEX watches_" +
                        target +
                        "_target ON price_watches(" +
                        column +
                        ") WHERE " +
                        column +
                        " IS NOT NULL");
                    connection.Execute(
                        "CREATE INDEX history_" +
                        target +
                        " ON price_history(" +
                        column +
                        ",changed_utc) WHERE " +
                        column +
                        " IS NOT NULL");
                }
            }
        }
#endregion

#region Validate
        public static void Validate(SQLiteConnection connection)
        {
            if (connection.ExecuteScalar<int>("PRAGMA application_id") != ApplicationId ||
                connection.ExecuteScalar<int>("PRAGMA user_version") != Version)
                throw new InvalidOperationException("This database is not a supported Travel Planner database.");
            if (connection.ExecuteScalar<string>("PRAGMA integrity_check") != "ok" ||
                connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_foreign_key_check") != 0)
                throw new InvalidOperationException("The database failed its integrity or foreign-key check.");
            foreach (string table in new[]
            {
                "metadata",
                "users",
                "destinations",
                "airports",
                "airlines",
                "flights",
                "hotels",
                "restaurants",
                "experiences",
                "hotspots",
                "reviews",
                "trips",
                "saved_items",
                "price_watches",
                "notifications",
                "price_history"
            }

            )
                if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", table) != 1)
                    throw new InvalidOperationException("Missing required table: " + table);
            // A matching version number alone cannot prove that columns/indexes still exist.
            foreach (string contract in new[]
            {
                "metadata:key,value",
                "users:id,email_normalized,password_hash,password_salt,password_iterations,password_algorithm,created_utc",
                "destinations:id,name,country,region,description",
                "airports:id,destination_id,name,time_zone",
                "airlines:id,name",
                "flights:id,airline_id,flight_number,origin_airport_id,destination_airport_id,departure_utc,arrival_utc,departure_local_date,price_cents,available_seats,currency",
                "hotels:id,destination_id,name,description,address,price_cents,currency,google_maps_url",
                "restaurants:id,destination_id,name,description,address,price_cents,currency,google_maps_url",
                "experiences:id,destination_id,name,description,address,price_cents,currency,google_maps_url",
                "hotspots:id,destination_id,name,description,address,price_cents,currency,google_maps_url",
                "reviews:id,hotel_id,restaurant_id,experience_id,hotspot_id,traveler_name,rating,body,is_demo",
                "trips:id,user_id,name,start_date,end_date,created_utc,updated_utc",
                "saved_items:id,user_id,trip_id,flight_id,hotel_id,restaurant_id,experience_id,hotspot_id,created_utc",
                "price_watches:id,user_id,flight_id,hotel_id,last_price_cents,active,created_utc",
                "price_history:id,flight_id,hotel_id,old_price_cents,new_price_cents,changed_utc",
                "notifications:id,user_id,watch_id,price_history_id,title,body,created_utc,is_read"
            }

            )
            {
                string[] parts = contract.Split(':');
                foreach (string column in parts[1].Split(','))
                    if (connection.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM pragma_table_info(?) WHERE name=?",
                        parts[0],
                        column) != 1)
                        throw new InvalidOperationException("Missing required column: " + parts[0] + "." + column);
            }

            foreach (string index in new[]
            {
                "flights_search",
                "notifications_unread",
                "saved_flight",
                "saved_hotel",
                "saved_restaurant",
                "saved_experience",
                "saved_hotspot",
                "watches_flight",
                "watches_hotel"
            }

            )
                RequireObject(connection, "index", index);
            foreach (string trigger in new[]
            {
                "notification_target_insert",
                "notification_target_update",
                "watch_target_immutable",
                "history_immutable"
            }

            )
                RequireObject(connection, "trigger", trigger);
        }
#endregion

#region Require Object
        private static void RequireObject(SQLiteConnection connection, string type, string name)
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type=? AND name=?", type, name) != 1)
                throw new InvalidOperationException("Missing required " + type + ": " + name);
        }
#endregion
    }
}
