using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TravelApp
{
    public sealed class TravelDetailsView : MonoBehaviour
    {
        public TMP_Text productName, category, description, price, availability, reviews;
        public Image productImage;
        public TMP_InputField startDateInput, endDateInput, ratingInput, reviewInput;
        public GameObject reviewForm;
        TravelScreenUI screen;
        Product product;
        public void Bind(TravelScreenUI owner) { screen = owner; }
        public void Show(Product value, string start, string end)
        {
            product = value; ClearReview();
            productName.text = value.productName; category.text = value.productType + " / " + value.location;
            description.text = value.description; price.text = "$" + value.price.ToString("0.00") + " per booking";
            productImage.sprite = value.productImage; productImage.enabled = value.productImage != null;
            availability.text = "Available " + value.startDate.ToDateTime().ToString("MMM d, yyyy") + " - " + value.endDate.ToDateTime().ToString("MMM d, yyyy");
            startDateInput.text = string.IsNullOrWhiteSpace(start) ? value.startDate.ToDateTime().ToString("yyyy-MM-dd") : start;
            endDateInput.text = string.IsNullOrWhiteSpace(end) ? startDateInput.text : end;
            RefreshReviews();
        }
        void RefreshReviews()
        {
            var saved = screen.Services.Bookings.Reviews.Where(r => r.productId == product.id).ToList();
            reviews.text = saved.Count == 0 ? "No reviews yet. Your experience could be the first." : string.Join("\n\n", saved.Select(r => r.username + "  /  " + r.rating + " out of 5\n" + r.text));
            reviewForm.SetActive(screen.Services.Bookings.History.Any(b => !b.cancelled && b.items.Any(i => i.productId == product.id)));
        }
        public void AddToCart() { if (product != null) screen.Report(screen.Services.Bookings.Add(product.id, startDateInput.text.Trim(), endDateInput.text.Trim())); }
        public void SubmitReview()
        {
            if (product == null) return;
            int.TryParse(ratingInput.text, out var rating);
            var result = screen.Services.Bookings.Review(product.id, rating, reviewInput.text); screen.Report(result);
            if (result.success) RefreshReviews();
        }
        public void ClearReview() { ratingInput.text = ""; reviewInput.text = ""; }
    }
}
