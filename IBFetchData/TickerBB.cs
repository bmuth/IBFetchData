using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBFetchData
{
    public class TickerBB
    {
        public string ticker { get; set; }
        public string Company { get; set; }
        public double? IVrank { get; set; }
        public double? IVpercentile { get; set; }
        public double? Price5D { get; set; }
        public double? Price10D { get; set; }
        public double? Price15D { get; set; }
        public DateTime PriceDate { get; set; }
        public double Price { get; set; }
        public double LowerBB { get; set; }
        public double UpperBB { get; set; }
        public double SMA { get; set; }
        public double? PercentBB { get; set; }

        public TickerBB (string t, string company, DateTime date, double price, double? ivrank, double? ivpercentile, double? price5d, double? price10d, double? price15d, double? percentBB)
        {
            ticker = t;
            Company = company;
            IVpercentile = ivpercentile;
            IVrank = ivrank;
            Price5D = price5d;
            Price10D = price10d;
            Price15D = price15d;
            PercentBB = percentBB;
            Price = price;
            PriceDate = date;
        }

        public TickerBB (string t, string company, double? ivrank, double? ivpercentile, double? price5d, double? price10d, double? price15d, double? percentBB)
        {
            ticker = t;
            Company = company;
            IVpercentile = ivpercentile;
            IVrank = ivrank;
            Price5D = price5d;
            Price10D = price10d;
            Price15D = price15d;
            PercentBB = percentBB;
        }

        public TickerBB (string t, DateTime pd, double price, double? ivpercentile, double lowerBB, double upperBB, double sma, double percentBB)
        {
            ticker = t;
            Price = price;
            PriceDate = pd;
            IVpercentile = ivpercentile * 100.0;
            LowerBB = lowerBB;
            SMA = sma;
            UpperBB = upperBB;
            PercentBB = percentBB;
        }
    };


}
