using TMPro;
using TravelPlanning.Reviews;
using UnityEngine;

namespace TravelPlanning.UI.Reviews
{
    /// <summary>Shows the complete stored review with both numeric and visual ratings.</summary>
    public sealed class ReviewRow : MonoBehaviour
    {
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text travelerName, ratingText, demoLabel, body;
        [SerializeField]
        private StarGraphic[] stars;
        public int FilledStars { get; private set; }

        public void Show(ReviewOption review)
        {
            travelerName.text = review.TravelerName;
            ratingText.text = review.Rating + " / 5";
            demoLabel.text = review.IsDemo ? "Fictional demo review — not a Google review" : "Review stored in this local catalog";
            body.text = review.Body;
            FilledStars = review.Rating;
            for (int index = 0; index < stars.Length; index++)
            {
                stars[index].SetFilled(index < review.Rating);
            }
        }
    }
}
