using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBFetchData
{
    class TickType
    {
        public const int BID_SIZE = 0;
        public const int BID = 1;
        public const int ASK = 2;
        public const int ASK_SIZE = 3;
        public const int LAST = 4;
        public const int LAST_SIZE = 5;
        public const int HIGH = 6;
        public const int LOW = 7;
        public const int VOLUME = 8;
        public const int CLOSE = 9;
        public const int BID_OPTION = 10;
        public const int ASK_OPTION = 11;
        public const int LAST_OPTION = 12;
        public const int MODEL_OPTION = 13;
        public const int OPEN = 14;
        public const int LOW_13_WEEK = 15;
        public const int HIGH_13_WEEK = 16;
        public const int LOW_26_WEEK = 17;
        public const int HIGH_26_WEEK = 18;
        public const int LOW_52_WEEK = 19;
        public const int HIGH_52_WEEK = 20;
        public const int AVG_VOLUME = 21;
        public const int OPEN_INTEREST = 22;
        public const int OPTION_HISTORICAL_VOL = 23;
        public const int OPTION_IMPLIED_VOL = 24;
        public const int OPTION_BID_EXCH = 25;
        public const int OPTION_ASK_EXCH = 26;
        public const int OPTION_CALL_OPEN_INTEREST = 27;
        public const int OPTION_PUT_OPEN_INTEREST = 28;
        public const int OPTION_CALL_VOLUME = 29;
        public const int OPTION_PUT_VOLUME = 30;
        public const int INDEX_FUTURE_PREMIUM = 31;
        public const int BID_EXCH = 32;
        public const int ASK_EXCH = 33;
        public const int AUCTION_VOLUME = 34;
        public const int AUCTION_PRICE = 35;
        public const int AUCTION_IMBALANCE = 36;
        public const int MARK_PRICE = 37;
        public const int BID_EFP_COMPUTATION = 38;
        public const int ASK_EFP_COMPUTATION = 39;
        public const int LAST_EFP_COMPUTATION = 40;
        public const int OPEN_EFP_COMPUTATION = 41;
        public const int HIGH_EFP_COMPUTATION = 42;
        public const int LOW_EFP_COMPUTATION = 43;
        public const int CLOSE_EFP_COMPUTATION = 44;
        public const int LAST_TIMESTAMP = 45;
        public const int SHORTABLE = 46;
        public const int FUNDAMENTAL_RATIOS = 47;
        public const int RT_VOLUME = 48;
        public const int HALTED = 49;
        public const int BID_YIELD = 50;
        public const int ASK_YIELD = 51;
        public const int LAST_YIELD = 52;
        public const int CUST_OPTION_COMPUTATION = 53;
        public const int TRADE_COUNT = 54;
        public const int TRADE_RATE = 55;
        public const int VOLUME_RATE = 56;
        public const int LAST_RTH_TRADE = 57;

        public static string Display (int ticktype)
        {
            switch (ticktype)
            {
                case BID_SIZE:
                    return "BID_SIZE";

                case BID:
                    return "BID";

                case ASK:
                    return "ASK";

                case ASK_SIZE:
                    return "ASK_SIZE";

                case LAST:
                    return "LAST";

                case LAST_SIZE:
                    return "LAST_SIZE";

                case HIGH:
                    return "HIGH";

                case LOW:
                    return "LOW";

                case VOLUME:
                    return "VOLUME";

                case CLOSE:
                    return "CLOSE";

                case OPTION_CALL_OPEN_INTEREST:
                    return "OPTION_CALL_OPEN_INTEREST";

                case OPTION_PUT_OPEN_INTEREST:
                    return "OPTION_PUT_OPEN_INTEREST";

                case BID_OPTION:
                    return "BID_OPTION";

                case ASK_OPTION:
                    return "ASK_OPTION";

                case LAST_OPTION:
                    return "LAST_OPTION";

                case MODEL_OPTION:
                    return "MODEL_OPTION";

                default:
                    return "Unknown tick type";
            }
        }
    }
}
