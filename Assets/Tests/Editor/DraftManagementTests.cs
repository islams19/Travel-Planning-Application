using System;
using System.IO;
using NUnit.Framework;
using TravelApp;
using UnityEngine;

public class DraftManagementTests
{
    [Test] public void RenamePersistsWithExistingSaveFormatAndSurvivesSubsequentSaves()
    {
        string folder=Path.Combine(Path.GetTempPath(),"trip-management-"+Guid.NewGuid().ToString("N"));
        var catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        try
        {
            string path=Path.Combine(folder,"save.json");var service=new AppServices(catalog,new JsonFileStore(path));
            var draft=new TripDraft{destination="Tokyo, Japan",passengers=3,cabin="Business",departureTicks=new DateTime(2027,7,14).Ticks};
            draft.outbound.seats=new[]{"1A","1B","1D"};Assert.IsTrue(service.SaveDraft(draft).success);
            Assert.IsTrue(service.RenameDraft(draft.id," Summer in Japan ").success);
            Assert.AreEqual("Summer in Japan",service.DraftTrips[0].title);
            draft.returnTicks=new DateTime(2027,7,24).Ticks;Assert.IsTrue(service.SaveDraft(draft).success);
            var restored=new AppServices(catalog,new JsonFileStore(path));Assert.AreEqual(1,restored.DraftTrips.Count);
            var saved=restored.DraftTrips[0];Assert.AreEqual("Summer in Japan",saved.title);Assert.AreEqual(draft.Return,saved.Return);
            CollectionAssert.AreEqual(draft.outbound.seats,saved.outbound.seats);Assert.IsTrue(DateTime.TryParse(saved.updatedUtc,out _));
            foreach(string invalid in new[]{" ",new string('x',61),"Bad\nname"})Assert.IsFalse(restored.RenameDraft(draft.id,invalid).success);
            Assert.AreEqual("Summer in Japan",restored.DraftTrips[0].title);
            Assert.IsTrue(restored.RemoveDraft(draft.id).success);Assert.AreEqual(0,new AppServices(catalog,new JsonFileStore(path)).DraftTrips.Count);
        }
        finally {UnityEngine.Object.DestroyImmediate(catalog);if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [Test] public void DraftManagementRespectsOwnershipAndRollsBackFailedRenames()
    {
        var catalog=ScriptableObject.CreateInstance<TravelCatalog>();var store=new FaultStore();
        try
        {
            var service=new AppServices(catalog,store);service.Accounts.Register("Owner","owner@example.com","test");
            var draft=new TripDraft{title="Original"};service.SaveDraft(draft);
            store.Fail=true;Assert.IsFalse(service.RenameDraft(draft.id,"Changed").success);Assert.AreEqual("Original",service.DraftTrips[0].title);
            store.Fail=false;service.Accounts.Logout();Assert.IsFalse(service.RenameDraft(draft.id,"Other user").success);
            service.RemoveDraft(draft.id);service.Accounts.Login("Owner","test");Assert.AreEqual("Original",service.DraftTrips[0].title);
        }
        finally {UnityEngine.Object.DestroyImmediate(catalog);}
    }
    sealed class FaultStore:ISaveStore
    {
        public bool Fail;string json;
        public SaveData Load()=>json==null?new SaveData():JsonUtility.FromJson<SaveData>(json);
        public void Save(SaveData value){if(Fail)throw new IOException("Test write failure");json=JsonUtility.ToJson(value);}
    }
}
