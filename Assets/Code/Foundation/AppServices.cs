using System;
using System.IO;
using UnityEngine;
namespace TravelApp
{
    // Composition root: services share one save document, never a scene or UI object.
    public sealed class AppServices
    {
        public TravelCatalog Catalog { get; }
        public AccountService Accounts { get; }
        public BookingService Bookings { get; }
        internal SaveData Data { get; private set; }
        readonly ISaveStore store;
        public event Action Changed;
        public AppServices(TravelCatalog catalog, ISaveStore store)
        {
            Catalog = catalog; this.store = store; Data = store.Load();
            Accounts = new AccountService(this); Bookings = new BookingService(this);
        }
        internal Result Commit(Action mutation, string message)
        {
            string before = JsonUtility.ToJson(Data);
            try { mutation(); store.Save(Data); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                Data = JsonUtility.FromJson<SaveData>(before);
                return Result.Fail("Could not save changes. Check available disk space and folder access.");
            }
            Notify(); return Result.Ok(message);
        }
        internal void Notify() { Changed?.Invoke(); }
        public Product Product(string id) => Catalog.products.Find(p => p != null && p.id == id);
    }
}
