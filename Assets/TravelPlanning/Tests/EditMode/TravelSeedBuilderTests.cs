using System;
using System.IO;
using NUnit.Framework;
using TravelPlanning.Data;
using TravelPlanning.UI.Editor;
using UnityEngine;

namespace TravelPlanning.Tests
{
    public sealed class TravelSeedBuilderTests
    {
#region InvalidCatalogCannotReplaceExistingSeed
        [Test]
        public void InvalidCatalogCannotReplaceExistingSeed()
        {
            string folder = Path.Combine(Path.GetTempPath(), "TravelSeedTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string source = Path.Combine(folder, "invalid.json");
                string output = Path.Combine(folder, "existing.db");
                File.WriteAllText(source, "{}");
                File.WriteAllText(output, "Preserve this existing output.");
                Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.BuildFromJson(source, output));
                Assert.That(File.ReadAllText(output), Is.EqualTo("Preserve this existing output."));
                Assert.That(Directory.GetFiles(folder, "*.staging-*"), Is.Empty);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

#endregion
#region ImporterRejectsDuplicatesUnknownReferencesBadDatesAndNegativePrices
        [Test]
        public void ImporterRejectsDuplicatesUnknownReferencesBadDatesAndNegativePrices()
        {
            var catalog = Load();
            catalog.destinations[1].id = catalog.destinations[0].id;
            Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.ValidateData(catalog));
            catalog = Load();
            catalog.flights[0].originAirportId = "XXX";
            Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.ValidateData(catalog));
            catalog = Load();
            catalog.flights[0].departureUtc = "not-a-date";
            Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.ValidateData(catalog));
            catalog = Load();
            catalog.places[0].priceCents = -1;
            Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.ValidateData(catalog));
        }

#endregion
#region ImporterAcceptsAnotherDestinationWithoutCodeChanges
        [Test]
        public void ImporterAcceptsAnotherDestinationWithoutCodeChanges()
        {
            var catalog = Load();
            var destinations = catalog.destinations;
            Array.Resize(ref destinations, destinations.Length + 1);
            // Clone the record through JSON to use the actual DTO without a second test model.
            destinations[destinations.Length - 1] = JsonUtility.FromJson<SeedDestination>(JsonUtility.ToJson(destinations[0]));
            destinations[destinations.Length - 1].id = "extra-destination";
            destinations[destinations.Length - 1].name = "Additional destination";
            catalog.destinations = destinations;
            Assert.DoesNotThrow(() => TravelSeedBuilder.ValidateData(catalog));
        }

#endregion
#region Load
        private static TravelSeedData Load() => JsonUtility.FromJson<TravelSeedData>(File.ReadAllText(TravelSeedBuilder.JsonPath));
#endregion
    }
}
