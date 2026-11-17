using System;
using System.Collections.Generic;
namespace TravelApp
{
    [Serializable] public sealed class SaveData
    {
        public int version = 1;
        public List<AccountData> accounts = new List<AccountData>();
        public List<ReviewData> reviews = new List<ReviewData>();
    }
    [Serializable] public sealed class AccountData
    {
        public string username, email, salt, passwordHash;
        public List<CartItem> cart = new List<CartItem>();
        public List<BookingData> bookings = new List<BookingData>();
    }
    [Serializable] public sealed class CartItem
    {
        public string productId, startDate, endDate;
    }
    [Serializable] public sealed class BookingData
    {
        public string id, createdUtc;
        public List<BookingLine> items = new List<BookingLine>();
        public bool cancelled;
        public decimal total { get { decimal sum = 0; foreach (var item in items) sum += item.price; return sum; } }
    }
    [Serializable] public sealed class BookingLine
    {
        public string productId, name, location, startDate, endDate;
        // JsonUtility serializes integer cents reliably; receipts never depend on catalog prices.
        public long priceCents;
        public decimal price => priceCents / 100m;
    }
    [Serializable] public sealed class ReviewData
    {
        public string username, productId, text;
        public int rating;
    }
    public readonly struct Result
    {
        public readonly bool success;
        public readonly string message;
        public Result(bool success, string message) { this.success = success; this.message = message; }
        public static Result Ok(string message) => new Result(true, message);
        public static Result Fail(string message) => new Result(false, message);
    }
}
