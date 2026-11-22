using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace TravelApp
{
    public sealed class TravelAppRuntime : MonoBehaviour
    {
        public static TravelAppRuntime Instance { get; private set; }
        public AppServices Services { get; private set; }
        // Completed flight snapshots belong to the application session, never a page GameObject.
        private readonly List<CompletedDemoBooking> completedFlights = new List<CompletedDemoBooking>();
        public IReadOnlyList<CompletedDemoBooking> CompletedFlights => completedFlights
            .Where(record => record.Booking.Username == Services?.Accounts.Username).ToList().AsReadOnly();
        public CompletedDemoBooking CompleteFlightPayment(PaymentBooking booking)
        {
            if (booking == null || booking.Username != Services?.Accounts.Username)
                throw new InvalidOperationException("Payment must belong to the current booking user.");
            var existing = completedFlights.Find(record => ReferenceEquals(record.Booking, booking));
            if (existing != null) return existing;
            var receipt = new CompletedDemoBooking(booking);
            completedFlights.Insert(0, receipt);
            return receipt;
        }
        public bool IsNavigating { get; private set; }
        public event Action<string> Message;
        public string StorageWarning { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            new GameObject("Travel Application").AddComponent<TravelAppRuntime>();
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<KeyboardFocusNavigation>();
            var catalog = Resources.Load<TravelCatalog>("TravelCatalog");
            if (catalog == null) { Debug.LogError("Travel catalog missing. Use Travel App > Synchronize Catalog before playing."); return; }
            var store = new JsonFileStore(System.IO.Path.Combine(Application.persistentDataPath, "travel-data-v1.json"));
            Services = new AppServices(catalog, store); StorageWarning = store.Warning;

        }
        public void Show(Result result) { Message?.Invoke(result.message); }
        public void Tell(string message) { Message?.Invoke(message); }
        public void Navigate(string scene)
        {
            if (IsNavigating) return;
            if (Services == null) return;
            if (scene != "LoginScreen" && !Services.Accounts.IsLoggedIn) scene = "LoginScreen";
            if (!Application.CanStreamedLevelBeLoaded(scene)) { Tell("Screen is not included in Build Settings: " + scene); return; }
            StartCoroutine(Load(scene));
        }
        IEnumerator Load(string scene)
        {
            IsNavigating = true;
            var operation = SceneManager.LoadSceneAsync(scene);
            while (!operation.isDone) yield return null;
            IsNavigating = false;
        }
        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
        }
    }
}
