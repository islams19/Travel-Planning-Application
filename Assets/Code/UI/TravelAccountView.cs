using System.Linq;
using TMPro;
using UnityEngine;
namespace TravelApp
{
    public sealed class TravelAccountView : MonoBehaviour
    {
        public TMP_Text accountName, bookingCount;
        public TravelItemList bookings;
        TravelScreenUI screen;
        public void Bind(TravelScreenUI owner) { screen = owner; }
        public void Refresh()
        {
            accountName.text = screen.Services.Accounts.Username;
            var history = screen.Services.Bookings.History.Reverse().ToList();
            bookingCount.text = history.Count + " bookings in your travel journal";
            // One receipt entry per purchased product keeps every review action reachable.
            var entries = history.SelectMany(b => b.items.Select(item => new { booking = b, item })).ToList();
            bookings.Render(entries.Count, (card, index) =>
            {
                var b = entries[index].booking; var item = entries[index].item;
                var product = screen.Services.Product(item.productId);
                string code = b.id.Substring(0, System.Math.Min(8,b.id.Length));
                card.Set((b.cancelled ? "CANCELLED" : "CONFIRMED") + " / BOOKING " + code, item.name,
                    item.location + " / " + item.startDate + " to " + item.endDate + "\nReceipt total: $" + b.total.ToString("0.00"),
                    "$" + item.price.ToString("0.00"), product != null ? product.productImage : null,
                    "Cancel booking", b.cancelled ? (System.Action)null : () => screen.ConfirmCancellation(b.id),
                    "View & review", product == null || b.cancelled ? (System.Action)null : () => screen.ShowProduct(product, item.startDate, item.endDate));
            });
        }
    }
}
