using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static class MockPaymentValidator
{
    public static string Validate(string name,string number,string expiration,string cvv,string postal,DateTime today)
        => Errors(name,number,expiration,cvv,postal,today).FirstOrDefault(error=>error!=null);

    public static string[] Errors(string name,string number,string expiration,string cvv,string postal,DateTime today)
    {
        name=name??"";number=number??"";expiration=expiration??"";cvv=cvv??"";postal=postal??"";
        var errors=new string[5];
        if(string.IsNullOrWhiteSpace(name)||name.Trim().Length<2||name.Length>80||!name.Any(char.IsLetter)||name.Any(char.IsControl))errors[0]="Enter a cardholder name.";
        string digits=number.Replace(" ","").Replace("-","");
        if(digits.Length<13||digits.Length>19||digits.Any(c=>c<'0'||c>'9'))errors[1]="Use 13–19 digits.";
        if(!Regex.IsMatch(expiration,@"^(0[1-9]|1[0-2])/[0-9]{2}$"))errors[2]="Use MM/YY.";
        else
        {
            int month=int.Parse(expiration.Substring(0,2),CultureInfo.InvariantCulture),year=2000+int.Parse(expiration.Substring(3,2),CultureInfo.InvariantCulture);
            if(new DateTime(year,month,1).AddMonths(1)<=today.Date)errors[2]="Use a current or future month.";
        }
        if(!Regex.IsMatch(cvv,@"^[0-9]{3,4}$"))errors[3]="Use 3–4 digits.";
        if(!Regex.IsMatch(postal.Trim(),@"^[A-Za-z0-9][A-Za-z0-9 -]{1,10}[A-Za-z0-9]$"))errors[4]="Use 3–12 letters or digits.";
        return errors;
    }
}
