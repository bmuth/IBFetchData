using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBFetchData
{
    class Utils
    {
        /*******************************************
         * 
         * Massage ticker name
         * 
         * ****************************************/

        public static string Massage (string ticker)
        {
            return ticker.Replace ('.', ' ');
        }


        /*******************************************
          * 
          * Convert from UnixTime to DateTime
          * 
          * ****************************************/

        static internal DateTime FromUnixTime (long unixTime)
        {
            var epoch = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds (unixTime).ToLocalTime ();
        }

        /***********************************************************
          * 
          * DatabasePrice (string ticker)
          * 
          * ********************************************************/

        static internal double? DatabasePrice (string ticker)
        {
            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {
                var p = (from stock in dc.Stocks
                         where stock.Ticker == ticker
                         select stock.LastTrade).First ();
                return (double?) p;
            }
        }

        /*******************************************************************
  * 
  * Compute Next Expiry Date
  * 
  * ****************************************************************/

        static internal DateTime ComputeNextExpiryDate (DateTime dt)
        {
            DateTime d = new DateTime (dt.Year, dt.Month, 1);
            if (d.DayOfWeek == DayOfWeek.Saturday)
            {
                d += new TimeSpan (7, 0, 0, 0);
            }
            d -= new TimeSpan ((int) d.DayOfWeek, 0, 0, 0);

            d += new TimeSpan (5 + 14, 0, 0, 0);

            if (Holidays.MarketHolidays.Contains (d))
            {
                d -= new TimeSpan (1, 0, 0, 0);
            }

            if (d < dt)
            {
                return ComputeNextExpiryDate (dt += new TimeSpan (14, 0, 0, 0));
            }
            return d;
        }

        /*****************************************
         * 
         * ComputeDaysToExpire
         * 
         * **************************************/

        internal static int ComputeDaysToExpire (string expiry)
        {
            int days = 0;
            TimeSpan one_day = new TimeSpan (1, 0, 0, 0);

            DateTime Expires = DateTime.ParseExact (expiry, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime d = DateTime.Now;
            while (d < Expires)
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                {
                    days++;
                }
                d += one_day;
            }

            return days;
        }

        /*********************************************
         * 
         * Parse percentage to double
         * 
         * ******************************************/

        internal static double PercentageParse (string percent)
        {
            percent = percent.Trim (new char[] {'%'});
            return double.Parse (percent) / 100.0;
        }

        internal static double CurrencyParse (string p)
        {
            p = p.TrimStart (new char[] { '$' });
            return double.Parse (p);
        }
    }
}
