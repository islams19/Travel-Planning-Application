using System;
using NUnit.Framework;

public class MockPaymentTests
{
    [TestCase("Alex Example", "4242 4242 4242 4242", "11/26", "123", "10001", true)]
    [TestCase("Alex Example", "4242424242424242", "12/30", "1234", "K1A 0B1", true)]
    [TestCase("", "4242424242424242", "12/30", "123", "10001", false)]
    [TestCase("Alex", "", "12/30", "123", "10001", false)]
    [TestCase("Alex", "4242abc4242424242", "12/30", "123", "10001", false)]
    [TestCase("Alex", "4242424242424242", "13/30", "123", "10001", false)]
    [TestCase("Alex", "4242424242424242", "10/26", "123", "10001", false)]
    [TestCase("Alex", "4242424242424242", "12/30", "1a3", "10001", false)]
    [TestCase("Alex", "4242424242424242", "12/30", "", "10001", false)]
    [TestCase("Alex", "4242424242424242", "12/30", "123", "", false)]
    [TestCase("Alex", "4242424242424242", "12/30", "123", "!!!", false)]
    public void ValidatesMockFields(string name, string card, string expiry, string cvv, string postal, bool valid)
    {
        Assert.AreEqual(valid, MockPaymentValidator.Validate(name, card, expiry, cvv, postal,
            new DateTime(2026, 11, 19)) == null);
    }
}
