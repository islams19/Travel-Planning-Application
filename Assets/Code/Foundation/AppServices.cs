using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
            if (Data.draftTrips == null) Data.draftTrips = new List<TripDraft>();
            Accounts = new AccountService(this); Bookings = new BookingService(this);
        }
        private bool OwnsDraft(TripDraft draft) => string.Equals(draft.username ?? "", Accounts.Username ?? "", StringComparison.Ordinal);
        public IReadOnlyList<TripDraft> DraftTrips => Data.draftTrips
            .Where(draft => draft != null && OwnsDraft(draft))
            .OrderByDescending(draft => draft.updatedUtc)
            .Select(draft => JsonUtility.FromJson<TripDraft>(JsonUtility.ToJson(draft))).ToList().AsReadOnly();

        public Result SaveDraft(TripDraft draft)
        {
            if (draft == null) return Result.Fail("No trip to save.");
            if (string.IsNullOrEmpty(draft.id)) draft.id = Guid.NewGuid().ToString("N");
            if (Data.draftTrips.Any(d => d.id == draft.id && !OwnsDraft(d)))
                return Result.Fail("This draft belongs to another account.");
            var snapshot = JsonUtility.FromJson<TripDraft>(JsonUtility.ToJson(draft));
            if(string.IsNullOrWhiteSpace(snapshot.title)) snapshot.title=Data.draftTrips.FirstOrDefault(d=>d.id==snapshot.id && OwnsDraft(d))?.title;
            snapshot.username = Accounts.Username;
            snapshot.updatedUtc = DateTime.UtcNow.ToString("O");
            return Commit(() =>
            {
                Data.draftTrips.RemoveAll(d => d.id == snapshot.id && OwnsDraft(d));
                Data.draftTrips.Add(snapshot);
            }, "Trip saved for later.");
        }

        public Result RenameDraft(string id,string title)
        {
            var draft=DraftTrips.FirstOrDefault(d=>d.id==id);
            if(draft==null)return Result.Fail("Saved trip not found.");
            title=(title??"").Trim();
            if(title.Length<1 || title.Length>60 || title.Any(char.IsControl))return Result.Fail("Use 1–60 characters for the trip name.");
            draft.title=title;return SaveDraft(draft);
        }

        public Result RemoveDraft(string id) => Commit(() =>
            Data.draftTrips.RemoveAll(d => d.id == id && OwnsDraft(d)), "Draft removed.");

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
