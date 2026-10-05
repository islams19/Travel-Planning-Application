using System.Collections.Generic;
using TMPro;
using TravelPlanning.Destinations;
using UnityEngine;

namespace TravelPlanning.UI.Destinations
{
    /// <summary>Owns the cards for one category, so clearing a city never duplicates its old cards.</summary>
    public sealed class PlaceSection : MonoBehaviour
    {
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text emptyMessage;
        [Header("Screen references")]
        [SerializeField]
        private Transform items;
        private readonly List<GameObject> cards = new List<GameObject>();
        public int CardCount => cards.Count;

        public void Show(IReadOnlyList<PlaceOption> places, PlaceCard prefab, System.Action<PlaceOption> onReviews = null,
            System.Action<PlaceOption> onSave = null, System.Action<PlaceOption> onTrack = null)
        {
            Clear();
            emptyMessage.gameObject.SetActive(places.Count == 0);
            foreach (var place in places)
            {
                var card = Instantiate(prefab, items);
                card.Show(place, onReviews, onSave, onTrack);
                cards.Add(card.gameObject);
            }
        }

        public void Clear()
        {
            foreach (var card in cards)
            {
                if (card)
                {
                    card.SetActive(false);
                    Destroy(card);
                }
            }

            cards.Clear();
            emptyMessage.gameObject.SetActive(false);
        }
    }
}
