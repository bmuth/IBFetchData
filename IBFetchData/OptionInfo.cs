using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SWI.Controls;

/* Other way of suppressing columns is to set AutoCreateColumns to false
 *
 *         [Browsable(false)]
 * 
 */

namespace IBFetchData
{
    [Serializable]
    public class OptionInfo
     {
        public static LogCtl m_Log;
        public string Currency { get; set; }
        public string Exchange { get; set; }
        public string Expiry { get; set; }
        public double Strike { get; set; }
        public string Symbol { get; set; }
        public string LocalSymbol { get; set; }
        public string Multiplier { get; set; }
        public string SecId { get; set; }
        public string SecIdType { get; set; }
        public string SecType { get; set; }
        public string Right { get; set; }
        public string TradingClass { get; set; }
        public double? Bid { get; set; }
        public double? Ask {get; set; }
        public double? Last { get; set; }
        public double? Price { get; set; }
        public double? Delta { get; set; }
        public double Gamma { get; set; }
        public double Theta { get; set; }
        public double Vega { get; set; }
        public double ThetaVegaRatio { get; set; }
        public double? ImpliedVolatility { get; set; }
        public double UndPrice { get; set; }
        public double? ProbITM {get; set; }
        public double Dividend { get; set; }
        public int? OpenInterest { get; set; }

        public double? BPR { get; set; }
        public string ROC { get; set; }

        public OptionInfo ()
        {
        }

        public OptionInfo (string currency, string exchange, string expiry, double strike, string symbol, string localsymbol,
            string multiplier, string secid, string secidtype, string sectype, string right, string tradingclass)
        {
            Currency = currency;
            Exchange = exchange;
            Expiry = expiry;
            Strike = strike;
            Symbol = symbol;
            LocalSymbol = localsymbol;
            Multiplier = multiplier;
            SecId = secid;
            SecIdType = secidtype;
            SecType = sectype;
            Right = right;
            TradingClass = tradingclass;
        }

        /**********************************************************
          * 
          * ComputeProbITM
          * 
          * *******************************************************/
        internal void ComputeProbITM ()
        {
            if (this.ImpliedVolatility == null)
            {
                m_Log.Log (ErrorLevel.logERR, string.Format ("ComputeProbITM unable to compute Prob ITM for {0}", this.Symbol));
                return;

            }
            double vol = (double) this.ImpliedVolatility / Math.Sqrt (252);
            double K = this.Strike;
            double S = this.UndPrice;

            /* If the underlying price is 0 
             * ----------------------------
             * then fetch the last price from the database. This sometimes happens off-hours for some
             * reason.... the underlying price is not returned */

            if (S == 0.0)
            {
                double? price = Utils.DatabasePrice (this.Symbol);
                if (price == null)
                {
                    return;
                }
                S = (double) price;
            }

            int DaysToExpire = Utils.ComputeDaysToExpire (this.Expiry);

            double variance = vol * vol;
            double d2 = Math.Log (S / K, Math.E);
            d2 += -variance / 2 * DaysToExpire;
            d2 /= vol;
            d2 /= Math.Sqrt (DaysToExpire);

            if (this.Right == "P")
            {
                this.ProbITM = Phi.phi (-d2);
            }
            else
            {
                this.ProbITM = Phi.phi (d2);
            }
            //foreach (DataGridViewRow row in dgvAnalOption.Rows)
            //{
            //    if (row.Index == index)
            //    {
            //        dgvAnalOption.InvalidateCell (dgvAnalOption.Rows[row.Index].Cells[7]);
            //    }
            //}
        }

        /*************************************************************
         * 
         * Come up with a premium estimate
         * 
         * **********************************************************/

        internal double? ComputePremium ()
        {
            if (Ask == null || Bid == null)
            {
                if (Last != null)
                {
                    return Last;
                }
            }
            if (Ask != null && Bid != null)
            {
                return ((Ask + Bid) / 2.0);
            }
            if (Ask == null)
            {
                return Bid;
            }
            if (Bid == null)
            {
                return  Ask;
            }
            return null;
        }

        /************************************************************
         * 
         * Compute ROC and  BPR
         * 
         * *********************************************************/

        internal void ComputeROCandBPR ()
        {
            double? premium = this.ComputePremium ();
            double? bpr1 = (0.2 * this.UndPrice - Math.Abs (this.UndPrice - this.Strike) +  premium) * 100.0;
            double? bpr2 = (0.1 * this.UndPrice + premium) * 100.0;
            double? bpr3 = 50.0 + (premium * 100.0);

            if (bpr1 != null && bpr2 != null)
            {
                bpr1 = Math.Max ((double) bpr1, (double) bpr2);
            }
            if (bpr3 != null)
            {
                this.BPR = Math.Max ((double) bpr1, (double) bpr3);
            }

            this.ROC = string.Format ("{0:N2} %", premium * 100.0 * 100.0 / this.BPR);
        }
     }
}
