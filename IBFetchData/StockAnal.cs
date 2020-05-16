using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBFetchData
{
    class StockAnal
    {
        public List<OptionInfo> OptionChain { get; set; }
        public string Ticker { get; set; }
        public string Company { get; set; }
        public string Sector { get; set; }
        public string Industry { get; set; }
        public double? LastDailyTrade { get; set; }
        public Int64? MarketCap { get; set; }
        public int? DailyVol { get; set; }
        public DateTime? Ex_DividendDate { get; set; }
        public string NextEarnings { get; set; }
        public double? IVRank { get; set; }
        public double? IVPercentile { get; set; }
        public string AnalystRating { get; set; }
        public double? PriceChange5Day { get; set; }
        public double? PriceChange10Day { get; set; }
        public double? PriceChange15Day { get; set; }
        public double? PercentBB { get; set; }
        public string SecType { get; set; }
        public string Exchange { get; set; }

        public StockAnal (string ticker, string company, string sector, string industry, decimal? lastdailytrade, decimal? marketcap, decimal? dailyvol, DateTime? ex_div, string next_earnings, string rating, double? ivrank, double? iv, double? price5d, double? price10d, double? price15d, double? percentBB, string sectype, string exchange)
        {
            Ticker = ticker;
            Company = company;
            Sector = sector;
            Industry = industry;
            LastDailyTrade = (double?) lastdailytrade;
            MarketCap = (Int64?) marketcap;
            DailyVol = (int?) dailyvol;
            Ex_DividendDate = ex_div;
            NextEarnings = next_earnings;
            AnalystRating = rating;
            IVRank = ivrank;
            IVPercentile = iv;
            PriceChange5Day = price5d;
            PriceChange10Day = price10d;
            PriceChange15Day = price15d;
            SecType = sectype;
            Exchange = exchange;
            PercentBB = percentBB;
        }
    
    }
}
