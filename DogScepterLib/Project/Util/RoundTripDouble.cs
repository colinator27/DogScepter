using System.Globalization;

namespace DogScepterLib.Project.Util;

public static class RoundTripDouble
{
    // Implemented from https://stackoverflow.com/a/49663470
    public static string ToRoundTrip(double value)
    {
        string str = value.ToString("R", CultureInfo.InvariantCulture);
        int x = str.IndexOf('E');
        if (x < 0)
            return str;

        int x1 = x + 1;
        string exp = str[x1..];
        int e = int.Parse(exp);

        string s;
        int numDecimals = 0;
        if (value < 0)
        {
            int len = x - 3;
            if (e >= 0)
            {
                if (len > 0)
                {
                    s = str.Substring(0, 2) + str.Substring(3, len);
                    numDecimals = len;
                }
                else
                    s = str.Substring(0, 2);
            }
            else
            {
                if (len > 0)
                {
                    s = str.Substring(1, 1) + str.Substring(3, len);
                    numDecimals = len;
                }
                else
                    s = str.Substring(1, 1);
            }
        }
        else
        {
            int len = x - 2;
            if (len > 0)
            {
                s = str[0] + str.Substring(2, len);
                numDecimals = len;
            }
            else
                s = str[0].ToString();
        }

        if (e >= 0)
        {
            e -= numDecimals;
            s += new string('0', e);
        }
        else
        {
            e = (-e - 1);
            if (value < 0)
                s = "-0." + new string('0', e) + s;
            else
                s = "0." + new string('0', e) + s;
        }

        return s;
    }
}
