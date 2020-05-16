using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBFetchData
{
    class StockPars
    {
        public string Ticker { get; set; }
        public int Id { get; set; }
        public string TimeSpan { get; set; }
        public DateTime EndDate { get; set; }
        public string SecType { get; set; }
        public string Exchange { get; set; }
        public string PrimaryExchange { get; set; }

        public StockPars (string ticker, int id, string timespan, DateTime enddate, string sec_type, string exchange, string  primary_exchange)
        {
            Ticker = ticker;
            Id = id;
            TimeSpan = timespan;
            EndDate = enddate;
            SecType = sec_type;
            Exchange = exchange;
            PrimaryExchange = primary_exchange;
        }

    }

    class PriceInfo
    {
        public string Ticker { get; set; }
        public DateTime PriceDate { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public int Volume { get; set; }
        public double WAP { get; set; }
    }
    class CompanyInfo
    {
        public string Ticker;
        //public string Company;
        //public string Sector;
        //public string Industry;
        //public double LastTrade;
        //public DateTime NextEarningsDate;
        //public float AnalystsOpinion;
        public string SecType;
        public string Exchange;
        public string PrimExchange;

        public CompanyInfo (string ticker, string sectype, string exchange, string prim_exchange)
        {
            Ticker = ticker;
            //Company = "";
            //Sector = "";
            //Industry = "";
            //LastTrade = 0.0;
            //NextEarningsDate = DateTime.MinValue;
            //AnalystsOpinion = -1;
            SecType = sectype;
            Exchange = exchange;
            PrimExchange = prim_exchange;
        }
    }
}
