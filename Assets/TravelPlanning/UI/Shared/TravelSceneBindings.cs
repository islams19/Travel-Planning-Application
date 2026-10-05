using System;
using System.Linq;
using TravelPlanning.UI.Desktop;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Trips;
using TravelPlanning.UI.Tracking;
using TravelPlanning.UI.Polish;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TravelPlanning.UI.Shared
{
    /// <summary>Typed runtime links between independently editable screen scenes.</summary>
    public sealed class TravelSceneBindings
    {
        public LoginPage Account { get; private set; }
        public FlightSearchPage Flights { get; private set; }
        public DestinationHubPage Hub { get; private set; }
        public ReviewsPage Reviews { get; private set; }
        public SavedTripsPage Trips { get; private set; }
        public PriceTrackingPage Tracking { get; private set; }
        public NotificationBadge Badge { get; private set; }
        public GameObject LoginCanvas { get; private set; }
        public GameObject HomeCanvas { get; private set; }
        public GameObject FlightCanvas { get; private set; }
        public GameObject HubCanvas { get; private set; }
        public GameObject ReviewsCanvas { get; private set; }
        public GameObject TripsCanvas { get; private set; }
        public GameObject TrackingCanvas { get; private set; }
        public Transform RegistrationCard { get; private set; }
        public Transform HomeCard { get; private set; }
        public CanvasGroup[] UnderlyingGroups => new[] { HomeCanvas, FlightCanvas, HubCanvas }.Select(canvas => canvas.GetComponent<CanvasGroup>()).ToArray();
        public GameObject[] Canvases { get; private set; }
        public Button Button(Transform root, string path) => root.Find(path).GetComponent<Button>();
        internal static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        private static GameObject Root(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (root.name == name) return root;
            throw new InvalidOperationException("Missing travel screen root: " + name);
        }
        internal TravelSceneBindings()
        {
            Account = Find<LoginPage>();
            Flights = Find<FlightSearchPage>();
            Hub = Find<DestinationHubPage>();
            Reviews = Find<ReviewsPage>();
            Trips = Find<SavedTripsPage>();
            Tracking = Find<PriceTrackingPage>();
            Badge = Find<NotificationBadge>();
            LoginCanvas = Root("Canvas");
            HomeCanvas = Root("HomeCanvas");
            FlightCanvas = Root("FlightCanvas");
            HubCanvas = Root("DestinationCanvas");
            ReviewsCanvas = Root("ReviewsCanvas");
            TripsCanvas = Root("SavedTripsCanvas");
            TrackingCanvas = Root("PriceTrackingCanvas");
            var registration = Root("RegistrationCanvas");
            RegistrationCard = registration.transform.Find("Right Panel/RegistrationCard");
            HomeCard = HomeCanvas.transform.Find("Right Panel/SignedInCard");
            Canvases = new[] { LoginCanvas, registration, HomeCanvas, FlightCanvas, HubCanvas, ReviewsCanvas, TripsCanvas, TrackingCanvas };
        }
        internal void ConfigureAll()
        {
            Account.Configure(this);
            Flights.Configure(this);
            Hub.Configure(this);
            Reviews.Configure(this);
            Trips.Configure(this);
            Tracking.Configure(this);
            Badge.Configure(this);
            Find<DesktopKeyboardNavigation>()?.Configure(this);
            Find<FlightSmokeRunner>()?.Configure(this);
            Find<DestinationSmokeRunner>()?.Configure(this);
            Find<ReviewSmokeRunner>()?.Configure(this);
            Find<TripSmokeRunner>()?.Configure(this);
            Find<TrackingSmokeRunner>()?.Configure(this);
            Find<ReleaseSmokeRunner>()?.Configure(this);
        }
        internal void EnableControllers()
        {
            foreach (var canvas in Canvases)
                foreach (var controller in canvas.GetComponentsInChildren<DesktopCanvasFit>(true)) controller.enabled = true;
            MonoBehaviour[] controllers = { Account, Flights, Hub, Reviews, Trips, Tracking, Badge,
                Find<DesktopKeyboardNavigation>(), Find<FlightSmokeRunner>(), Find<DestinationSmokeRunner>(),
                Find<ReviewSmokeRunner>(), Find<TripSmokeRunner>(), Find<TrackingSmokeRunner>(), Find<ReleaseSmokeRunner>() };
            foreach (var controller in controllers) if (controller) controller.enabled = true;
        }
    }

}
