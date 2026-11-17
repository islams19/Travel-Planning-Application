using System;
using UnityEngine;

[Serializable]
public struct TravelDate
{
    public int year;

    [Range(1, 12)]
    public int month;

    [Range(1, 31)]
    public int day;

    public DateTime ToDateTime()
    {
        try
        {
            return new DateTime(year, month, day);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}