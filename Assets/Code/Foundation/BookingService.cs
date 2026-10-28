using System;
using System.Collections.Generic;
using System.Linq;
namespace TravelApp
{
    public sealed class BookingService
    {
        readonly AppServices app;
        public BookingService(AppServices app) { this.app = app; }
        public IReadOnlyList<CartItem> Cart => app.Accounts.Current?.cart.AsReadOnly() ?? (IReadOnlyList<CartItem>)Array.Empty<CartItem>();
        public IReadOnlyList<BookingData> History => app.Accounts.Current?.bookings.AsReadOnly() ?? (IReadOnlyList<BookingData>)Array.Empty<BookingData>();
        public IReadOnlyList<ReviewData> Reviews => app.Data.reviews.AsReadOnly();
        public Result Add(string id, string start, string end)
        {
            if (!app.Accounts.IsLoggedIn) return Result.Fail("Please log in first.");
            var error = Validate(app.Product(id), start, end);
            if (error != null) return Result.Fail(error);
            if (Cart.Any(i => i.productId == id && i.startDate == start && i.endDate == end)) return Result.Fail("This booking is already in your cart.");
            return app.Commit(() => app.Accounts.Current.cart.Add(new CartItem { productId = id, startDate = start, endDate = end }), "Added to cart.");
        }
        public Result Remove(int index)
        {
            if (!app.Accounts.IsLoggedIn || index < 0 || index >= Cart.Count) return Result.Fail("Cart item not found.");
            return app.Commit(() => app.Accounts.Current.cart.RemoveAt(index), "Removed from cart.");
        }
        public Result Checkout()
        {
            if (!app.Accounts.IsLoggedIn) return Result.Fail("Please log in first.");
            if (Cart.Count == 0) return Result.Fail("Your cart is empty.");
            var receipt = new BookingData { id = Guid.NewGuid().ToString("N"), createdUtc = DateTime.UtcNow.ToString("O") };
            foreach (var item in Cart)
            {
                var p = app.Product(item.productId); string error = Validate(p, item.startDate, item.endDate);
                if (error != null) return Result.Fail(error + " Remove the item or choose new dates.");
                receipt.items.Add(new BookingLine { productId = p.id, name = p.productName, location = p.location, startDate = item.startDate, endDate = item.endDate, priceCents = (long)Math.Round((decimal)p.price * 100m, MidpointRounding.AwayFromZero) });
            }
            return app.Commit(() => { app.Accounts.Current.bookings.Add(receipt); app.Accounts.Current.cart.Clear(); }, "Booking confirmed. No payment was taken.");
        }
        public Result Cancel(string id)
        {
            var booking = History.FirstOrDefault(b => b.id == id);
            if (booking == null || booking.cancelled) return Result.Fail("Active booking not found.");
            return app.Commit(() => booking.cancelled = true, "Booking cancelled.");
        }
        public Result Review(string id, int rating, string text)
        {
            if (!History.Any(b => !b.cancelled && b.items.Any(i => i.productId == id))) return Result.Fail("Book this product before reviewing it.");
            if (rating < 1 || rating > 5 || string.IsNullOrWhiteSpace(text) || text.Length > 1000) return Result.Fail("Choose 1–5 stars and write a review of up to 1000 characters.");
            return app.Commit(() =>
            {
                var review = app.Data.reviews.Find(r => r.productId == id && AccountService.Same(r.username, app.Accounts.Username));
                if (review == null) { review = new ReviewData { productId = id, username = app.Accounts.Username }; app.Data.reviews.Add(review); }
                review.rating = rating; review.text = text.Trim();
            }, "Review saved.");
        }
        static string Validate(Product p, string start, string end)
        {
            if (p == null || !p.available) return "This product is no longer available.";
            if (float.IsNaN(p.price) || float.IsInfinity(p.price) || p.price < 0 || p.price > 1000000000) return "This product has an invalid price.";
            if (!ProductQuery.Date(start, out var s) || !ProductQuery.Date(end, out var e) || !ProductSearch.Covers(p, s, e)) return "Choose dates inside the product's availability (YYYY-MM-DD).";
            return null;
        }
    }
}
