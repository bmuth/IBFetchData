using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using SWI.Controls;
using IBApi;
using System.Reflection;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;

namespace IBFetchData
{

    class StockInfoQueue : IDisposable
    {
        public List<PriceInfo> m_PriceInfoList;
        private Queue<StockPars> m_q;
        private int m_Current_ID;
        private LogCtl m_Log;
        //private AutoResetEvent m_Event;
        //private int m_OutstandingCalls;
        private object locker = new object ();
        private EWrapperImpl ib;

        private CancellationTokenSource cancellation;

        public delegate void NotifyNewHistoricalPriceHandler (PriceInfo pi, int NoLeft);
        //public delegate void NotifyFinishedHandler ();
        public event NotifyNewHistoricalPriceHandler NotifyNewHistoricalPrice;
        //public event NotifyFinishedHandler NotifyFinished;
         
        public async Task RepeatActionEvery (Func<bool> action, TimeSpan interval)
        {
            while (true)
            {
                if (!action ())
                {
                   // NotifyFinished ();
                    return;
                }
                Task task = Task.Delay (interval, cancellation.Token);
                try
                {
                    await task;
                }
                catch (TaskCanceledException)
                {
                    //NotifyFinished ();
                    return;
                }
            }
        }

        public StockInfoQueue (LogCtl log, EWrapperImpl Ib)
        {
            m_Log = log;
            ib = Ib;
            m_q = new Queue<StockPars> ();
            cancellation = new CancellationTokenSource ();
        }

        public Task run ()
        {
            /*            var datahandler = default (AxTWSLib._DTwsEvents_historicalDataEventHandler);
                        var errhandler = default (AxTWSLib._DTwsEvents_errMsgEventHandler);

                        errhandler = new AxTWSLib._DTwsEvents_errMsgEventHandler ((s, e) =>
                        {
                            m_axTws.historicalData -= datahandler;
                            m_axTws.errMsg -= errhandler;
                        });

                        datahandler = new AxTWSLib._DTwsEvents_historicalDataEventHandler ((s, e) =>
                        {
                            int index = e.reqId & 0xFFFF;

                            PriceInfo pi = m_PriceInfoList[index];

                            string format = "yyyyMMdd";
                            if (e.date.StartsWith ("finished"))
                            {
                                m_Log.Log (ErrorLevel.logINF, string.Format ("historicalData: Finished collecting for [{0}]", m_PriceInfoList[index].Ticker));
                                NotifyNewHistoricalPrice (pi, m_q.Count);
                                return;
                            }

                            if ((e.reqId & 0xFFFF0000) == Constants.HISTORICAL_PRICEHOURLY)
                            {
                                pi.PriceDate = Utils.FromUnixTime (long.Parse (e.date));
                            }
                            else
                            {
                                pi.PriceDate = DateTime.ParseExact (e.date, format, CultureInfo.InvariantCulture);
                            }
                            pi.Open = e.open;
                            pi.Close = e.close;
                            pi.High = e.high;
                            pi.Low = e.low;
                            pi.Volume = e.volume;
                            pi.WAP = e.wAP;

                            m_Log.Log (ErrorLevel.logINF, string.Format ("historicalData:[{0}] {1} open: {2} close: {3} high: {4} low: {7} vol: {5} WAP: {6}",
                               m_PriceInfoList[index].Ticker, pi.PriceDate.ToString ("yyyy-MM-dd"), e.open.ToString (), e.close.ToString (), e.high.ToString (), e.volume.ToString (), e.wAP.ToString (), e.low.ToString ()));

                            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
                            {
                                if ((e.reqId & 0xFFFF0000) != Constants.HISTORICAL_IV)
                                {
                                    dc.UpsertPriceHistory (pi.Ticker, pi.PriceDate, (decimal) pi.Close, (decimal) pi.Open, (decimal) pi.High, (decimal) pi.Low, pi.Volume, (decimal) pi.WAP);
                                }
                                else
                                {
                                    dc.UpsertIVHistory (pi.Ticker, pi.PriceDate, pi.Close, pi.Open, pi.High, pi.Low);
                                }
                            }
                        });

                        m_axTws.historicalData += datahandler;
                        m_axTws.errMsg += errhandler;

                        return RepeatActionEvery (FetchWork, new TimeSpan (0, 0, 11));*/
            return null;
        }

/*
        private bool FetchWork ()
        {
            if (m_q.Count <= 0)
            {
                return false;
            }
            else //if (m_q.Count > 0 && m_OutstandingCalls < 10)
            {
                StockPars ci;
                lock (m_q)
                {
                    ci = m_q.Dequeue ();
                }

              
                TWSLib.IContract contract = m_axTws.createContract ();

                //contract.tradingClass = "";
                contract.symbol = Utils.Massage (ci.Ticker);
//                contract.secType = "STK";
                contract.secType = ci.SecType;
//                        contract.localSymbol = "";
//                        contract.exchange = "SMART";
                contract.exchange = ci.Exchange;
                contract.primaryExchange = "";
                contract.currency = "USD";
                //contract.expiry = "";

                //contract.strike = 0;
                //contract.multiplier = "";
                m_Current_ID = ci.Id;

                try
                {
                    switch (ci.Id & 0xFFFF0000)
                    {
                        //case Constants.STOCK_DATA:
                        //    m_ibClient.ClientSocket.reqContractDetails (ci.Id, contract);
                        //    m_Log.Log (ErrorLevel.logINF, string.Format ("Fired off reqContractDetails for {0}", ci.Ticker));
                        //    break;
                        //case Constants.MARKET_DATA:
                        //    m_ibClient.ClientSocket.reqMktData (ci.Id, contract, "", true);
                        //    m_Log.Log (ErrorLevel.logINF, string.Format ("launched reqMktData for {0}", ci.Ticker));
                        //    break;
                        case Constants.HISTORICAL_PRICEDAILY:
                            {
                                string EndDate = ci.EndDate.ToString ("yyyyMMdd HH:mm:ss");
                                        
                                m_axTws.reqHistoricalDataEx (ci.Id, contract, EndDate, ci.TimeSpan, "1 day", "TRADES", 1, 1, null);
                                m_Log.Log (ErrorLevel.logINF, string.Format ("launched reqHistoricalData (price) for {0}", ci.Ticker));
                            }
                            break;
                        case Constants.HISTORICAL_PRICEHOURLY:
                            {
                                string EndDate = ci.EndDate.ToString ("yyyyMMdd HH:mm:ss");

                                m_axTws.reqHistoricalDataEx (ci.Id, contract, EndDate, ci.TimeSpan, "1 hour", "TRADES", 1, 2, null);
                                m_Log.Log (ErrorLevel.logINF, string.Format ("launched reqHistoricalData (price) for {0}", ci.Ticker));
                            }
                            break;
                        case Constants.HISTORICAL_IV:
                            {
                                string EndDate = ci.EndDate.ToString ("yyyyMMdd HH:mm:ss");

                                m_axTws.reqHistoricalDataEx (ci.Id, contract, EndDate, ci.TimeSpan, "1 day", "OPTION_IMPLIED_VOLATILITY", 1, 1, null);
                                m_Log.Log (ErrorLevel.logINF, string.Format ("launched reqHistoricalData (iv) for {0}", ci.Ticker));
                            }
                            break;
                        default:
                            throw new Exception (string.Format ("Invalid Request ID in CompanyInfoQueue. {0:X}", ci.Id));
                    }
                }
                catch (Exception e)
                {
                    m_Log.Log (ErrorLevel.logERR, string.Format ("CompanyInfoQueue launch failed. {0}", e.Message));
                }

                return true;
            }
        }
*/
        public void stop ()
        {
            if (!cancellation.IsCancellationRequested)
            {
                m_Log.Log (ErrorLevel.logINF, "cancelling token in StockInfoQueue");
//                m_axTws.cancelHistoricalData (m_Current_ID);
                cancellation.Cancel ();
            }
        }

        internal void Enqueue (StockPars ci)
        {
            lock (m_q)
            {
                m_q.Enqueue (ci);
            }
        }

        /*******************************************************
        * Internal routine for disposing our resource here
        * *****************************************************/

        private void Dispose (bool IfDisposing)
        {
            /* Dispose unmanaged resources here
             * -------------------------------- */

            if (IfDisposing)
            {
            }

            /* Dispose managed resources here
             * ------------------------------ */

//            if (m_thr.IsAlive)
            {
                stop ();
            }

            m_Log.Log (ErrorLevel.logINF, "CompanyInfoQueue.Dispose called.");
        }

        /*******************************************************
         * Dispose ()
         * 
         * *****************************************************/

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
            m_Log.Log (ErrorLevel.logINF, "CompanyInfoQueue.Dispose called.");
        }

    }
}
