using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBFetchData
{
    class Constants
    {
        public const int MARKET_DATA                 = 0x10000000;
        public const int STOCK_DATA                  = 0;
        public const int HISTORICAL_IV               = 0x04000000;
        public const int HISTORICAL_PRICEDAILY       = 0x02000000;
        public const int HISTORICAL_PRICEHOURLY      = 0x01000000;
        public const int OPTIONS_MARKET_DATA         = 0x40000000;
        public const int SCANNER_MARKET_DATA         = 0x50000000;
        public const int OPTIONS_OPTIONS_DATA        = 0x60000000;
        public const int ANALYZE_OPTIONS_DATA        = 0x70000000;
        public const int ANALYZE_OPTIONS_MARKET_DATA = 0x11000000;
        public const int PRIMARY_EXCHANGE            = 0x12000000;
    }

    class Holidays
    {
        public static List<DateTime> MarketHolidays = new List<DateTime> 
                                                            {   
                                                                new DateTime (2017, 5, 29),
                                                                new DateTime (2017, 7, 4),
                                                                new DateTime (2017, 9, 4),
                                                                new DateTime (2017, 11, 23),
                                                                new DateTime (2017, 12, 25),
                                                                new DateTime (2018, 1, 1),
                                                                new DateTime (2018, 1, 15),
                                                                new DateTime (2018, 2, 19),
                                                                new DateTime (2018, 3, 30),
                                                                new DateTime (2018, 5, 28),
                                                                new DateTime (2018, 7, 4),
                                                                new DateTime (2018, 9, 3),
                                                                new DateTime (2018, 11, 22),
                                                                new DateTime (2018, 12, 25)
                                                                
                                                            };
    }
    public static class MyExtensions
    {
        public static int NoBusinessDays (this DateTime StartDate, DateTime EndDate)
        {
            int b = 0;
            while (StartDate < EndDate)
            {
                if (StartDate.DayOfWeek != DayOfWeek.Saturday && StartDate.DayOfWeek != DayOfWeek.Sunday && !Holidays.MarketHolidays.Contains (StartDate))
                {
                    b++;
                }
                StartDate += new TimeSpan (1, 0, 0, 0);
            }
            return b;
        }

        public static DateTime NextBusinessDay (this DateTime CurrDate)
        {
            DateTime dt = CurrDate;
            while (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday || Holidays.MarketHolidays.Contains (dt))
            {
                dt += new TimeSpan (1, 0, 0, 0);
            }
            return dt;
        }

        public static bool IsTodayBusinessDay (this DateTime dt)
        {
            if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday || Holidays.MarketHolidays.Contains (dt))
            {
                return false;
            }
            return true;
        }

        public static DateTime PreviousBusinessDay (this DateTime CurrDate)
        {
            DateTime dt = CurrDate;
            do
            {
                dt -= new TimeSpan (1, 0, 0, 0);
            } while (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday || Holidays.MarketHolidays.Contains (dt));
            return dt;

        }
    }

}
