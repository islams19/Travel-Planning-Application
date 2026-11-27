using System;
using System.Reflection;

// Several flows pick calendar days by position in the current month. Pinning "today" to the 1st
// keeps those days selectable now that the calendar greys out dates that have already passed.
static class CalendarClock
{
    static readonly FieldInfo Today=typeof(DateSelectionPage).GetField("Today",BindingFlags.Static|BindingFlags.NonPublic);
    public static void PinToStartOfMonth() {var now=DateTime.Today;var first=new DateTime(now.Year,now.Month,1);Today.SetValue(null,(Func<DateTime>)(()=>first));}
    public static void Reset()=>Today.SetValue(null,(Func<DateTime>)(()=>DateTime.Today));
}
