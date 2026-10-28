using System;
using TMPro;
using UnityEngine;
namespace TravelApp
{
    public sealed class TravelCartView : MonoBehaviour
    {
        public TravelItemList cartItems, checkoutItems;
        public TMP_Text cartTotal, checkoutTotal;
        public UnityEngine.UI.Button reviewButton, confirmButton;
        TravelScreenUI screen;
        public void Bind(TravelScreenUI owner) { screen = owner; }
        public void Refresh()
        {
            var items = screen.Services.Bookings.Cart;
            decimal total = 0;
            foreach (var item in items)
            {
                var p = screen.Services.Product(item.productId);
                if (p != null && p.price >= 0 && p.price <= 1000000000 && !float.IsInfinity(p.price) && !float.IsNaN(p.price)) total += Math.Round((decimal)p.price, 2, MidpointRounding.AwayFromZero);
            }
            cartTotal.text = checkoutTotal.text = "$" + total.ToString("0.00");
            reviewButton.interactable = confirmButton.interactable = items.Count > 0;
            cartItems.Render(items.Count, (card, index) =>
            {
                var item = items[index]; var p = screen.Services.Product(item.productId);
                card.Set("IN YOUR CART", p != null ? p.productName : "Product no longer available", item.startDate + " to " + item.endDate,
                    p != null ? "$" + p.price.ToString("0.00") : "Unavailable", p != null ? p.productImage : null,
                    "Remove", () => { var result = screen.Services.Bookings.Remove(index); screen.Report(result); if (result.success) Refresh(); });
            });
            checkoutItems.Render(items.Count, (card, index) =>
            {
                var item = items[index]; var p = screen.Services.Product(item.productId);
                card.Set("YOUR SELECTION", p != null ? p.productName : "Unavailable product", item.startDate + " to " + item.endDate,
                    p != null ? "$" + p.price.ToString("0.00") : "Unavailable", p != null ? p.productImage : null, "", null);
            });
        }
        public void ConfirmCheckout()
        {
            var result = screen.Services.Bookings.Checkout(); screen.Report(result);
            if (result.success) screen.Account();
        }
    }
}
