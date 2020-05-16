using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TWSLib;
using SWI.Controls;

namespace IBFetchData
{
    class IBapi
    {
        public static AxTWSLib.AxTws axtws;
        public static LogCtl m_Log;
        private static Random m_rnd = new Random ();


        /********************************************************************************
         * Feb 21, 2016
         * 
         * DetermineLocalSymbol - untested
         * 
         ********************************************************************************/

        static public Task<string> DetermineLocalSymbol (string ticker, string exchange)
        {
            List<string> LocalSymbolList = new List<string> ();
            int reqid = m_rnd.Next (1, short.MaxValue);

            var tcs = new TaskCompletionSource<string> ();
            TWSLib.IContract contract = axtws.createContract ();

            contract.symbol = Utils.Massage (ticker);
            contract.secType = "STK";
            contract.exchange = "";
            contract.currency = "USD";

            m_Log.Log (ErrorLevel.logINF, string.Format ("DetermineLocalSymbol: Getting local symbol for {0}", ticker));

            var errhandler = default (AxTWSLib._DTwsEvents_errMsgEventHandler);
            var datahandler = default (AxTWSLib._DTwsEvents_contractDetailsExEventHandler);
            var endhandler = default (AxTWSLib._DTwsEvents_contractDetailsEndEventHandler);

            errhandler = new AxTWSLib._DTwsEvents_errMsgEventHandler ((s, e) =>
            {
                tcs.TrySetException (new Exception (e.errorMsg));

                axtws.contractDetailsEx -= datahandler;
                axtws.errMsg -= errhandler;
                axtws.contractDetailsEnd -= endhandler;
            });

            endhandler = new AxTWSLib._DTwsEvents_contractDetailsEndEventHandler ((s, e) =>
            {
                if (e.reqId == (Constants.PRIMARY_EXCHANGE | reqid))
                {
                    try
                    {
                        if (LocalSymbolList.Count > 1)
                        {
                            tcs.TrySetException (new Exception (string.Format ("Multiple contract descriptions for {0}", ticker)));
                        }
                        else if (LocalSymbolList.Count == 0)
                        {
                            tcs.TrySetResult (LocalSymbolList[0]);
                        }
                    }
                    finally
                    {
                        axtws.contractDetailsEx -= datahandler;
                        axtws.errMsg -= errhandler;
                        axtws.contractDetailsEnd -= endhandler;
                    }
                }
            });

            datahandler = new AxTWSLib._DTwsEvents_contractDetailsExEventHandler ((s, e) =>
            {
                TWSLib.IContractDetails c = e.contractDetails;
                TWSLib.IContract d = (TWSLib.IContract) c.summary;
                if ((Constants.PRIMARY_EXCHANGE | reqid) == e.reqId)
                {
                    LocalSymbolList.Add (d.localSymbol);
                    m_Log.Log (ErrorLevel.logINF, string.Format ("DetermineLocalSymbol: symbol: {0} localsym {1} conid: {2}", d.symbol, d.localSymbol, d.conId));
                }
            });

            axtws.contractDetailsEx += datahandler;
            axtws.errMsg += errhandler;
            axtws.contractDetailsEnd += endhandler;

            axtws.reqContractDetailsEx (Constants.PRIMARY_EXCHANGE | reqid, contract);
            return tcs.Task;
        }
    }
}
