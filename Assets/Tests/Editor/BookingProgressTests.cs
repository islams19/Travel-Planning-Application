using NUnit.Framework;

public class BookingProgressTests
{
    [TestCase(false)] [TestCase(true)]
    public void ProgressReflectsCurrentPageAndBackwardNavigation(bool roundTrip)
    {
        foreach(var step in new[]{TripDraft.Step.Search,TripDraft.Step.DepartureFlights,TripDraft.Step.DepartureSeats,TripDraft.Step.Review,TripDraft.Step.Payment})
        {
            int current=step==TripDraft.Step.Search?0:step==TripDraft.Step.DepartureFlights?1:step==TripDraft.Step.DepartureSeats?2:step==TripDraft.Step.Review?3:4;
            Assert.AreEqual(BookingProgress.State.Current,BookingProgress.StateFor(step,roundTrip,current));
            for(int i=current+1;i<5;i++) Assert.AreEqual(BookingProgress.State.Upcoming,BookingProgress.StateFor(step,roundTrip,i));
        }
        Assert.AreEqual(BookingProgress.State.Upcoming,BookingProgress.StateFor(TripDraft.Step.DepartureFlights,roundTrip,3),"Going backward must not retain a completed Review step");
        Assert.AreEqual(BookingProgress.State.Completed,BookingProgress.StateFor(TripDraft.Step.Payment,roundTrip,2));
    }
    [Test] public void RoundTripDoesNotPrematurelyCompleteFlightsOrSeats()
    {
        Assert.AreEqual(BookingProgress.State.Incomplete,BookingProgress.StateFor(TripDraft.Step.DepartureSeats,true,1));
        Assert.AreEqual(BookingProgress.State.Current,BookingProgress.StateFor(TripDraft.Step.ReturnFlights,true,1));
        Assert.AreEqual(BookingProgress.State.Incomplete,BookingProgress.StateFor(TripDraft.Step.ReturnFlights,true,2));
        Assert.AreEqual(BookingProgress.State.Completed,BookingProgress.StateFor(TripDraft.Step.ReturnSeats,true,1));
        Assert.AreEqual(BookingProgress.State.Current,BookingProgress.StateFor(TripDraft.Step.ReturnSeats,true,2));
        Assert.AreEqual(BookingProgress.State.Completed,BookingProgress.StateFor(TripDraft.Step.DepartureSeats,false,1));
    }
}
