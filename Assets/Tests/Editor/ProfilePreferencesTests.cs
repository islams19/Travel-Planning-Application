using System;
using System.Linq;
using NUnit.Framework;
using TravelApp;
using TravelApp.FlightMap;
using UnityEngine;

public sealed class ProfilePreferencesTests
{
    sealed class Store:ISaveStore
    {
        public string json="{\"version\":1,\"accounts\":[],\"reviews\":[]}";
        public SaveData Load()=>JsonUtility.FromJson<SaveData>(json);
        public void Save(SaveData data)=>json=JsonUtility.ToJson(data);
    }
    [Test] public void ProfilePreferencesUseExistingSaveAndRemainAccountScoped()
    {
        var catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        try
        {
            var store=new Store();var app=new AppServices(catalog,store);
            Assert.AreEqual("JFK",app.Accounts.Preferences.homeAirportCode);
            Assert.IsTrue(app.Accounts.Register("ProfileOne","one@example.com","test-pass").success);
            Assert.IsTrue(app.Accounts.SaveProfile("Traveler One","new@example.com",new TravelPreferences{homeAirportCode="EWR",cabinClass="Business",seatType="Window",distanceUnits="Miles"}).success);
            var restored=new AppServices(catalog,store);Assert.IsTrue(restored.Accounts.Login("ProfileOne","test-pass").success);
            Assert.AreEqual("Traveler One",restored.Accounts.DisplayName);Assert.AreEqual("new@example.com",restored.Accounts.Email);Assert.AreEqual("EWR",restored.Accounts.Preferences.homeAirportCode);Assert.AreEqual("Business",restored.Accounts.Preferences.cabinClass);Assert.AreEqual("Window",restored.Accounts.Preferences.seatType);Assert.AreEqual("Miles",restored.Accounts.Preferences.distanceUnits);
            Assert.IsFalse(restored.Accounts.SaveProfile("","bad",restored.Accounts.Preferences).success);
            Assert.IsFalse(restored.Accounts.SaveProfile("Name","one@example.com",new TravelPreferences{homeAirportCode="INVALID"}).success);
            restored.Accounts.Logout();Assert.AreEqual("JFK",restored.Accounts.Preferences.homeAirportCode);
            Assert.IsTrue(restored.Accounts.Register("ProfileTwo","two@example.com","test-pass").success);Assert.AreEqual("Economy",restored.Accounts.Preferences.cabinClass);
        }
        finally{UnityEngine.Object.DestroyImmediate(catalog);}
    }
    [TestCase("LGA","ATH")][TestCase("LGA","SYD")][TestCase("JTR","JFK")]
    public void RestrictedAirportsRequirePlausibleConnections(string origin,string destination)
    {
        var from=TravelLocationCatalog.Find(origin);var to=TravelLocationCatalog.Find(destination);
        var flights=MockFlightGenerator.Generate(from,to,new DateTime(2027,5,14));Assert.AreEqual(12,flights.Count);
        Assert.IsTrue(flights.All(f=>f.Stops>0));
        foreach(var segment in flights.SelectMany(f=>f.Segments))Assert.IsTrue(MockRouteRules.CanOperateLeg(TravelLocationCatalog.Find(segment.OriginCode),TravelLocationCatalog.Find(segment.DestinationCode)));
        CollectionAssert.AreEqual(flights.Select(f=>f.Id),MockFlightGenerator.Generate(from,to,new DateTime(2027,5,14)).Select(f=>f.Id));
    }
    [Test] public void LoginIsFirstEnabledBuildScene()
    {Assert.AreEqual("Assets/Scenes/Login.unity",UnityEditor.EditorBuildSettings.scenes.First(s=>s.enabled).path);}
}
