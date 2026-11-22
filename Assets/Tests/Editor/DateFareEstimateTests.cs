using System;
using System.Linq;
using NUnit.Framework;

public class DateFareEstimateTests
{
    [Test] public void CalendarPricesRepeatAcrossCallsAndVaryByDate()
    {
        var start=new DateTime(2027,5,1);
        var first=Enumerable.Range(0,62).Select(i=>DateFareEstimates.EconomyFare(start.AddDays(i),"JFK","LAX").Value).ToArray();
        var second=Enumerable.Range(0,62).Select(i=>DateFareEstimates.EconomyFare(start.AddDays(i),"JFK","LAX").Value).ToArray();
        CollectionAssert.AreEqual(first,second);
        Assert.Greater(first.Distinct().Count(),8);
        Assert.IsTrue(first.All(price=>price>=90 && price<700 && price%5==0));
        Assert.AreEqual(first[0],DateFareEstimates.EconomyFare(start,"LAX","JFK"));
    }
    [Test] public void LongerRoutesHavePlausiblyHigherEstimates()
    {
        var date=new DateTime(2027,5,4);
        Assert.Greater(DateFareEstimates.EconomyFare(date,"JFK","HND"),DateFareEstimates.EconomyFare(date,"JFK","LAX"));
    }
    [Test] public void MissingOrInvalidRoutesDoNotInventFares()
    {
        Assert.IsNull(DateFareEstimates.EconomyFare(new DateTime(2027,5,4),null,"LAX"));
        Assert.IsNull(DateFareEstimates.EconomyFare(new DateTime(2027,5,4),"JFK","INVALID"));
        Assert.IsNull(DateFareEstimates.EconomyFare(new DateTime(2027,5,4),"JFK","JFK"));
        Assert.IsNull(DateFareEstimates.EconomyFare(new DateTime(2027,5,4),"JFK","EWR"));
    }
}
