using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IBApi;
using SWI.Controls;
using System.Globalization;
using System.Xml.Linq;
using Be.Timvw.Framework.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.IO;
using System.Xml.Serialization;
using Muth.Framework;

namespace IBFetchData
{
    public partial class Form1 : Form
    {

        const int bbcolPERCENTBB = 7;


        const int ID_MARKETSCANNER = 0x1;

        delegate void UpdateIVMessageDelegate (string text);
        delegate void UpdateBBMessageDelegate (string text);
        delegate void UpdateMsgDelegate (ErrorLevel level, string text);
        delegate void UpdateContractDetailsEndDelegate (int reqid);
        delegate void UpdateAnalOptionsDelegate (int reqid);
        delegate void NotifyHistoricalDataUpdatedDelegate (PriceInfo pi, int NoLeft);
        //delegate void NotifyOptionUpdateDelegate (int row, object value, string header);

        private System.Timers.Timer timerOptChain = new System.Timers.Timer ();
        private LogCtl m_Log;
        private bool bSettingWidthsCompanyInfo = false;
        private bool bSettingWidthsSuppressRecording = false;
        private bool m_IfConnected;
        private bool m_IfScanning;
        private bool m_IfCollectingHistorical;
        private bool m_bAnalFilterApplied = false;
        private SortableBindingList<OptionInfo> m_AnalOptionInfo;
        private SortableBindingList<OptionInfo> m_OptionOptionInfo;
        private List<CompanyInfo> m_ci;
        private List<ScannerData> m_DisplayedScannerData;
        private SortableBindingList<PriceInfo> m_ViewableHistory;
        private SortableBindingListView<StockAnal> m_ViewableAnalStocks;
        private SortableBindingList<TickerBB> m_ViewableTickerBB;
        private List<StockAnal> m_SelectedStocks;
        private StockInfoQueue m_StockInfoQueue;
        private System.Collections.Specialized.StringCollection CompanyInfoColumnWidths;
        private System.Collections.Specialized.StringCollection StockAnalColumnWidths;
        private System.Collections.Specialized.StringCollection OptionAnalColumnWidths;
        private System.Collections.Specialized.StringCollection BBColumnWidths;
        private System.Collections.Specialized.StringCollection HistoricalBBColumnWidths;
        private System.Collections.Specialized.StringCollection IVPercentileColumnWidths;
        private frmTrends m_frmTrends = null;
        private DateTime m_BollingerBandDate;
        private  bool m_bCancelScanning = false;

        public Form1 ()
        {
            m_Log = new LogCtl ("IBFetchData");
//            m_Log.LogFilename = "IBFetchData.log";
            m_Log.LogFilename = Properties.Settings.Default.LogDir + "IBFetchData.log";
            OptionInfo.m_Log = m_Log;
            m_IfConnected = false;
            m_IfScanning = false;
            m_IfCollectingHistorical = false;

            IBapi.m_Log = m_Log;

            InitializeComponent ();

            IBapi.ib = new EWrapperImpl (m_Log);

            RecomputePortfolioList ();
            m_StockInfoQueue = null;
            lbxLocationCode.SelectedIndex = 0;
            lbxStockFilterType.SelectedIndex = 0;

            dtpExpiry.Value = Utils.ComputeNextExpiryDate (DateTime.Now);
        }
        private void RecomputePortfolioList ()
        {
            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {

                var PortfolioList = (from r in dc.Portfolios
                                     select new PortfolioDesc { PortfolioName = r.PortfolioName, StockCount = 0 }).ToList ();

                foreach (var p in PortfolioList)
                {
                    var count = (from ps in dc.PortfolioStocks
                                 where p.PortfolioName == ps.PortfolioName
                                 select ps.Ticker).Count ();
                    p.StockCount = count;
                }

                lbxPortfolio.DataSource = PortfolioList;
                lbxPortfolio.DisplayMember = "PortfolioName";
            }
        }
        private void btnConnect_Click (object sender, EventArgs e)
        {
            if (m_IfConnected == false)
            {

                int port = 7496;
                if (!rbTWS.Checked)
                {
                    port = 4001;
                }

                int clientId = int.Parse (tbClientId.Text);

                string host = "127.0.0.1";
                try
                {
                    IBapi.ib.ClientSocket.eConnect (host, port, clientId);
                    m_IfConnected = true;
                }
                catch (Exception ex)
                {
                    AddMessage (ErrorLevel.logERR, string.Format ("Please check your connection attributes. {0}", ex.Message));
                }
                if (m_IfConnected)
                {
                    btnConnect.Text = "Disconnect";
                }
                else
                {
                    MessageBox.Show ("Failed to connect.");
                }

            }
            else
            {
                IBapi.ib.ClientSocket.eDisconnect ();
                m_IfConnected = false;
                btnConnect.Text = "Connect";
                AddMessage (ErrorLevel.logINF, "Disconnected.");
            }
        }

        public void AddMessage (ErrorLevel level, string text)
        {
            if (this.tbMsg.InvokeRequired)
            {
                UpdateMsgDelegate d = new UpdateMsgDelegate (AddMessage);
                this.Invoke (d, new object[] { text });
            }
            else
            {
                m_Log.Log (level, text);
                tbMsg.Text += (text + "\r\n");
                tbMsg.Select (tbMsg.Text.Length, 0);
                tbMsg.ScrollToCaret ();
            }
        }

        public void NotifyError (int id, int errorCode, string errorMsg)
        {
            if (id >= 0)
            {
                int index = id & 0xFFFF;
                //if (m_ci != null && m_ci[index] != null)
                //{
                //    AddMessage (string.Format ("Error. [{0}] code: {1} msg: {2}", m_ci[index].Ticker, errorCode, errorMsg));
                //}
                //m_ciq.DecrementOutstandingCall ();
                AddMessage (ErrorLevel.logERR, string.Format ("Error. Id: {0:X} Code: {1} Msg: {2}", id, errorCode, errorMsg));
                //if (m_StockInfoQueue != null)
                //{
                //    m_StockInfoQueue.DecrementOutstandingCall ();
                //}
 
            }
            else
            {
                if (errorCode == 502)
                {
                    m_IfConnected = false;
                }
                AddMessage (ErrorLevel.logERR, string.Format ("Error. Id: {0:X} Code: {1} Msg: {2}", id, errorCode, errorMsg));
            }
        }

        /****************************************************
         * 
         * Fetch the option chain
         * 
         * *************************************************/

        private void btnOptionChain_Click (object sender, EventArgs e)
        {
 /*           m_OptionOptionInfo = new SortableBindingList<OptionInfo> ();

            IContract contract = axTws.createContract ();

            contract.symbol = Utils.Massage (tbStock.Text);
            contract.secType = "OPT";
            contract.expiry = dtpExpiry.Value.ToString ("yyyyMMdd");
            contract.strike = 0.0;
            contract.right = rbPut.Checked ? "P" : "C";
            contract.multiplier = "";
            contract.exchange = "SMART";
            contract.primaryExchange = "";
            contract.currency = "USD";
            contract.localSymbol = "";
            contract.includeExpired = 0;

            AddMessage (ErrorLevel.logINF, string.Format ("Getting Chain for {0}", tbStock.Text));
            axTws.reqContractDetailsEx (Constants.OPTIONS_OPTIONS_DATA | 0, contract);*/
        }

/*
        private void axTws_contractDetailsEx (object sender, AxTWSLib._DTwsEvents_contractDetailsExEvent e)
        {
            IContract d = (IContract) e.contractDetails.summary;

            
            if ((e.reqId & 0xFFFF0000) == Constants.OPTIONS_OPTIONS_DATA)
            {
                if (d.multiplier != "100")
                {
                    m_Log.Log (ErrorLevel.logERR, "Skipped, due to wrong multiplier");
                    return;
                }

                m_Log.Log (ErrorLevel.logINF, string.Format ("contractDetailsEx: Local Symbol: {0}, Expires: {1}, Strike: {2} Multiplier: {3} {4}", d.localSymbol, d.expiry, d.strike.ToString ("0000.00"), d.multiplier, d.conId.ToString ()));
                m_OptionOptionInfo.Add (new OptionInfo (d.currency, d.exchange, d.expiry, d.strike, d.symbol, d.localSymbol, d.multiplier, d.secId, d.secIdType, d.secType, d.right, d.tradingClass));
            }
        }

         private void axTws_errMsg (object sender, AxTWSLib._DTwsEvents_errMsgEvent e)
        {
            NotifyError (e.id, e.errorCode, e.errorMsg);
        }

        private void axTws_contractDetailsEnd (object sender, AxTWSLib._DTwsEvents_contractDetailsEndEvent e)
        {
            if ((e.reqId & 0xFFFF0000) == Constants.OPTIONS_OPTIONS_DATA)
            {
                OptionsContractDetailsEnd (e.reqId);
            }
            else
            {
 //               AnalContractDetailsEnd (e.reqId);
            }
        }

        private void OptionsContractDetailsEnd (int reqid)
        {
            if (dgvOptionChain.InvokeRequired)
            {
                UpdateContractDetailsEndDelegate d = new UpdateContractDetailsEndDelegate (OptionsContractDetailsEnd);
                this.Invoke (d, new object[] { reqid });
            }
            else
            {
                //dgvOptionChain.AutoGenerateColumns = false;

                dgvOptionChain.DataSource = m_OptionOptionInfo;
            }
        }

        private void AnalContractDetailsEnd (int reqid)
        {
            {
            //if (dgvAnalOption.InvokeRequired)
            //{
            //    UpdateContractDetailsEndDelegate d = new UpdateContractDetailsEndDelegate (AnalContractDetailsEnd);
            //    this.Invoke (d, new object[] { reqid });
            //}
            //else
            //{
            //    var options = (from y in m_AnalOptionInfo
            //                   orderby y.Strike
            //                   select y).ToList ();

            //    /* Pick the best options to display
            //     * --------------------------------
            //     * first find ATM option */

            //    double diff = double.MaxValue;
            //    int best_opt = -1;
            //    for (int i = 0; i < options.Count; i++)
            //    {
            //        var opt = options[i];
            //        double new_diff = Math.Abs (opt.Strike - (double) m_SelectedStocks.LastDailyTrade);
            //        if (new_diff < diff)
            //        {
            //            diff = new_diff;
            //            best_opt = i;
            //        }
            //    }

            //    int no_strikes = 60; // int.Parse (tbAnalNoStrikes.Text);
            //    List<OptionInfo> DisplayedOptions = new List<OptionInfo> ();
            //    DisplayedOptions.Add (options[best_opt]);
            //    int j = 1;
            //    while (DisplayedOptions.Count < no_strikes && (2 * j + 1 < options.Count))
            //    {
            //        int low_index = best_opt - j;
            //        if (low_index > 0)
            //        {
            //            DisplayedOptions.Add (options[low_index]);
            //        }
            //        if (DisplayedOptions.Count >= no_strikes)
            //        {
            //            break;
            //        }
            //        int high_index = best_opt + j;
            //        if (high_index < options.Count)
            //        {
            //            DisplayedOptions.Add (options[high_index]);
            //        }
            //        j++;
            //    }

            //    DisplayedOptions.Sort ((pi1, pi2) => pi1.Strike.CompareTo (pi2.Strike)); 
            //    m_AnalOptionInfo = DisplayedOptions;

            //    dgvAnalOption.AutoGenerateColumns = false;

            //    dgvAnalOption.DataSource = m_AnalOptionInfo;

                /* Now pick up the option prices
                 * ----------------------------- */
/*
                FetchOptionPrices ();
            }

        }
*/
        /*****************************************************************************
         * 
         * FetchOptionPrices
         * 
         * **************************************************************************/

        private void FetchOptionPrices ()
        {
            foreach (DataGridViewRow r in dgvAnalOption.Rows)
            {
                //OptionInfo Option = m_AnalOptionInfo[r.Index];
                //r.DefaultCellStyle.BackColor = Color.LightCyan;

                //IContract contract = axTws.createContract ();

                //contract.symbol = "";
                //contract.secType = "OPT";
                //contract.exchange = "SMART";
                //contract.localSymbol = Option.LocalSymbol;
                //axTws.reqMarketDataType (ckbAnalUseFrozenData.Checked ? 2 : 1);
                
                //axTws.reqMktDataEx (r.Index | Constants.OPTIONS_MARKET_DATA, contract, "", 1);
            }
        }

        private void btnOptionMarketData_Click (object sender, EventArgs e)
        {
            //for (int i = 0; i < m_AnalOptionInfo.Count; i++)
            //{
            //    var Option = m_AnalOptionInfo[i];

            //    IContract contract = axTws.createContract ();

            //    contract.symbol = "";
            //    contract.secType = "OPT";
            //    contract.exchange = "SMART";
            //    contract.localSymbol = Option.LocalSymbol;
            //    axTws.reqMktDataEx (i | Constants.OPTIONS_MARKET_DATA, contract, "", 1);
            //}
        }

        /**************************************************************************
         * 
         * Start historical collection of data (prices, iv)
         * 
         * ***********************************************************************/

        private async void btnStartHistorical_Click (object sender, EventArgs e)
        {
/*            if (m_IfCollectingHistorical == false)
            {
                btnStartHistorical.Text = "Cancel";
                m_IfCollectingHistorical = true;

                Assembly _assembly = Assembly.GetExecutingAssembly ();
                Stream _imageStream = _assembly.GetManifestResourceStream ("IBFetchData.Images.spiffygif_30x30.gif");
                pbHistorical.Image = new Bitmap (_imageStream);

                m_StockInfoQueue = new StockInfoQueue (m_Log, axTws);

                StockInfoQueue.NotifyNewHistoricalPriceHandler hp = ((pi, count) =>
                {
                    NotifyHistoricaDataUpdated (pi, count);
                });
                //StockInfoQueue.NotifyFinishedHandler fin = null;
                
                //fin = (() =>
                //{
                //    pbHistorical.Image = null;
                //    m_StockInfoQueue.NotifyNewHistoricalPrice -= hp;
                //    m_StockInfoQueue.NotifyFinished -= fin;
                //    m_StockInfoQueue.Dispose ();
                //    m_StockInfoQueue = null;
                //    btnStartHistorical.Text = "Scan";
                //    m_IfCollectingHistorical = false;
                //});

                m_StockInfoQueue.NotifyNewHistoricalPrice += hp;
               // m_StockInfoQueue.NotifyFinished += fin;

                m_ci = new List<CompanyInfo> ();

                m_StockInfoQueue.m_PriceInfoList = new List<PriceInfo> ();
                m_ViewableHistory = new SortableBindingList<PriceInfo> ();

                PortfolioDesc d = (PortfolioDesc) lbxPortfolio.SelectedItem;

                using (dbOptionsDataContext dc = new dbOptionsDataContext ())
                {
                    var cif = (from ps in dc.PortfolioStocks
                                join s in dc.Stocks on ps.Ticker equals s.Ticker
                                where ps.PortfolioName == d.PortfolioName
                                select new CompanyInfo (s.Ticker, s.SecType, s.Exchange, s.PrimExchange)
                                        ).ToList ();

                    lbHistoricalLeftToProcess.Text = cif.Count.ToString ();

                    if (ckbFillMissingValues.Checked)
                    {
                        foreach (var CompInfo in cif)
                        {
                            var lastdate = (from sh in dc.PriceHistories
                                            where sh.Ticker == CompInfo.Ticker
                                            && sh.PriceDate == dtpEndDate.Value && sh.PriceTime == new TimeSpan (0, 0, 0)
                                            select sh
                                            ).ToArray ();
                            if (lastdate.Count () == 0)
                            {
                                m_ci.Add (CompInfo);
                            }
                        }
                    }
                    else
                    {
                        m_ci = cif;
                    }

                    foreach (var ci in m_ci)
                    {
                        PriceInfo pi = new PriceInfo ();
                        pi.Ticker = ci.Ticker;
                        pi.PriceDate = DateTime.MinValue;
                        m_StockInfoQueue.m_PriceInfoList.Add (pi);
                    }

                    int type = Constants.HISTORICAL_PRICEDAILY;
                    if (rbHistoricalIV.Checked)
                    {
                        type = Constants.HISTORICAL_IV;
                    }
                    else
                    {
                        if (rbHistoricalHourly.Checked)
                        {
                            type = Constants.HISTORICAL_PRICEHOURLY;
                        }
                    }

                    //for (int loop = 0; loop < 1; loop++)
                    for (int loop = 0; loop < m_ci.Count; loop++)
                    {
                        m_StockInfoQueue.Enqueue (new StockPars (m_ci[loop].Ticker, loop | type, tbTimeSpan.Text, dtpEndDate.Value, m_ci[loop].SecType, m_ci[loop].Exchange, m_ci[loop].PrimExchange));
                    }
                    dgvHistorical.DataSource = m_ViewableHistory;
                    await m_StockInfoQueue.run ();

                    pbHistorical.Image = null;
                    m_StockInfoQueue.NotifyNewHistoricalPrice -= hp;
                    //m_StockInfoQueue.NotifyFinished -= fin;
                    m_StockInfoQueue.Dispose ();
                    m_StockInfoQueue = null;
                    btnStartHistorical.Text = "Scan";
                    m_IfCollectingHistorical = false;
                }
            }
            else
            {
                m_StockInfoQueue.stop ();
            }*/
        }

        /********************************************************************
         * 
         * Collecting historial data
         * 
         * *****************************************************************/
/*
        private void axTws_historicalData (object sender, AxTWSLib._DTwsEvents_historicalDataEvent e)
        {
            int index = e.reqId & 0xFFFF;

            //PriceInfo pi = m_PriceInfoList[index];

            //string format = "yyyyMMdd";
            //if (e.date.StartsWith ("finished"))
            //{
            //    m_Log.Log (ErrorLevel.logINF, string.Format ("historicalData: Finished collecting for [{0}]", m_PriceInfoList[index].Ticker));
            //    NotifyHistoricaDataUpdated (pi, index);
            //    if (m_StockInfoQueue != null)
            //    {
            //        m_StockInfoQueue.DecrementOutstandingCall ();
            //    }
            //    return;
            //}

            //if ((e.reqId & 0xFFFF0000) == Constants.HISTORICAL_PRICEHOURLY)
            //{
            //    pi.PriceDate = Utils.FromUnixTime (long.Parse (e.date));
            //}
            //else
            //{
            //    pi.PriceDate = DateTime.ParseExact (e.date, format, CultureInfo.InvariantCulture);
            //}
            //pi.Open = e.open;
            //pi.Close = e.close;
            //pi.High = e.high;
            //pi.Low = e.low;
            //pi.Volume = e.volume;
            //pi.WAP = e.wAP;

            //m_Log.Log (ErrorLevel.logINF, string.Format ("historicalData:[{0}] {1} open: {2} close: {3} high: {4} low: {7} vol: {5} WAP: {6}",
            //   m_PriceInfoList[index].Ticker, pi.PriceDate.ToString ("yyyy-MM-dd"), e.open.ToString (), e.close.ToString (), e.high.ToString (), e.volume.ToString (), e.wAP.ToString (), e.low.ToString ()));

            //using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            //{
            //    if ((e.reqId & 0xFFFF0000) != Constants.HISTORICAL_IV)
            //    {
            //        dc.UpsertPriceHistory (pi.Ticker, pi.PriceDate, (decimal) pi.Close, (decimal) pi.Open, (decimal) pi.High, (decimal) pi.Low, pi.Volume, (decimal) pi.WAP);
            //    }
            //    else
            //    {
            //        dc.UpsertIVHistory (pi.Ticker, pi.PriceDate, pi.Close, pi.Open, pi.High, pi.Low);
            //    }
            //}
        }
*/
        /**************************************************************
         * 
         * IV or price on another equity has been obtained
         * so update the form
         * 
         * ************************************************************/

        private void NotifyHistoricaDataUpdated (PriceInfo pi, int NoLeft)
        {
            if (dgvHistorical.InvokeRequired)
            {
                NotifyHistoricalDataUpdatedDelegate d = new NotifyHistoricalDataUpdatedDelegate (NotifyHistoricaDataUpdated);
                this.Invoke (d, new object[] { pi });
            }
            else
            {
                m_ViewableHistory.Add (pi);
                lbHistoricalLeftToProcess.Text = (NoLeft).ToString ();
            }
        }

        private void btnShowIV_Click (object sender, EventArgs e)
        {
            dgvIV.Hide ();
            chart1.Show ();

            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {
                chart1.ChartAreas.Clear ();
                chart1.Series.Clear ();

                var y = (from ps in dc.PriceHistories
                         where ps.Ticker == tbStockIV.Text && ps.PriceTime == new TimeSpan (0, 0, 0)
                         orderby ps.PriceDate
                         select new { ps.PriceDate, ps.ClosingIV }
                                 ).ToList ();



                chart1.ChartAreas.Add ("area");
                chart1.ChartAreas["area"].AxisX.Minimum = 0;
                //                chart1.ChartAreas["area"].AxisX.Maximum = 365;
                //                chart1.ChartAreas["area"].AxisX.Interval = 1;

                chart1.ChartAreas["area"].AxisY.Minimum = 0;
                chart1.ChartAreas["area"].AxisY.Interval = 0.05;

                chart1.Series.Add ("iv");

                chart1.Series["iv"].Color = Color.BlueViolet;
                chart1.Series["iv"].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;

                DateTime OneYearAgo = DateTime.Now - new TimeSpan (365, 0, 0, 0);
                foreach (var p in y)
                {
                    //int iv = (int) (p.ClosingIV * 1000);
                    //Point pt = new Point ((p.ClosingDate - OneYearAgo).Days, iv);
                    if (p.ClosingIV != null)
                    {
                        chart1.Series["iv"].Points.AddXY ((p.PriceDate - OneYearAgo).Days, (double) p.ClosingIV);
                    }
                }


            }

        }

        private void btnTableIV_Click (object sender, EventArgs e)
        {
            chart1.Hide ();
            dgvIV.Show ();

            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {
                chart1.Show ();
                chart1.ChartAreas.Clear ();
                chart1.Series.Clear ();

                var y = (from ps in dc.PriceHistories
                         where ps.Ticker == tbStockIV.Text && ps.PriceTime == new TimeSpan (0, 0, 0)
                         orderby ps.PriceDate
                         select ps
                                 ).ToList ();

                dgvIV.DataSource = y;
            }
        }

        /**********************************************************
         * 
         * Compute IV Rank and IV Percentile
         * 
         * ********************************************************/
        private async void btnComputeIVPercentile_Click (object sender, EventArgs e)
        {
            try
            {
                tbIVMsg.Text = "";
                Assembly _assembly = Assembly.GetExecutingAssembly ();
                Stream _imageStream = _assembly.GetManifestResourceStream ("IBFetchData.Images.spiffygif_30x30.gif");
                pbIVPercentile.Image = new Bitmap (_imageStream);

                SortableBindingList<TickerBB> tiv;

                PortfolioDesc d = (PortfolioDesc) lbxPortfolio.SelectedItem;

                tiv = await ComputeIVPercentile (d);

                dgvIVPercentile.AutoGenerateColumns = false;
                dgvIVPercentile.DataSource = tiv;
                foreach (DataGridViewColumn column in dgvIVPercentile.Columns)
                {
                    dgvIVPercentile.Columns[column.Name].SortMode = DataGridViewColumnSortMode.Automatic;
                }
            }
            finally
            {
                pbIVPercentile.Image = null;
            }
        }

        /**********************************************************
         * 
         * Asynchronously Compute IV Rank and IV Percentile
         * 
         * ********************************************************/
        private Task<SortableBindingList<TickerBB>> ComputeIVPercentile (PortfolioDesc d)
        {
            return Task.Run (() =>
            {
                int TrendDays = 20; // hard-coded, to get the last 20 days of data
                SortableBindingList<TickerBB> tiv = new SortableBindingList<TickerBB> ();

                using (dbOptionsDataContext dc = new dbOptionsDataContext ())
                {
                    /* Determine date two weeks from last update
                        * ----------------------------------------- */

                    var dates = (from ps in dc.PriceHistories
                                    where ps.Ticker == "IBM"
                                    orderby ps.PriceDate descending
                                    select ps.PriceDate
                                ).Take (TrendDays + 1).ToList ();

                    if (dates.Count < TrendDays)
                    {
                        MessageBox.Show ("There isn't enough data to go back that far.");
                        return null;
                    }
                    DateTime TrendDate = dates[TrendDays];
                    DateTime CurrentDate = dates[0];

                    var tickers = (from ps in dc.PortfolioStocks
                                    join s in dc.Stocks on ps.Ticker equals s.Ticker
                                    where ps.PortfolioName == d.PortfolioName
                                    select s.Ticker
                                    ).ToList ();

                    /* Special case
                     * ------------ */

                    DateTime OneYearGo = DateTime.Now - new TimeSpan (365, 0, 0, 0);
                    //DateTime OneYearGo = DateTime.Now - new TimeSpan (10, 0, 0, 0); //  fails for Feb 29th.

                    foreach (var ticker in tickers)
                    {
                        m_Log.Log (ErrorLevel.logINF, string.Format ("ComputeIVPercentile: examining ticker {0}", ticker));


                        var ivs = (from ps in dc.PriceHistories
                                    where ps.Ticker == ticker
                                    && ps.PriceDate > OneYearGo && ps.PriceTime == new TimeSpan (0, 0, 0)
                                    orderby ps.PriceDate
                                    select new { ps.PriceDate, ps.PriceTime, ps.ClosingPrice, ps.ClosingIV }
                                    ).ToList ();

                        double? min_iv = (from iv in ivs select iv.ClosingIV).Min ();
                        double? max_iv = (from iv in ivs select iv.ClosingIV).Max ();

                        double? iv_rank = null;
                        double? iv_percentile = null;

                        /* Compute iv rank
                            * --------------- */

                        if (ivs.Count == 0)
                        {
                            continue;
                        }

                        var last_iv = ivs.Last ().ClosingIV;
                        /* if null, then chose the previous one
                            * ------------------------------------ */

                        if (last_iv == null && ivs.Count > 1)
                        {
                            last_iv = ivs[ivs.Count - 2].ClosingIV;

                        }
                        if (min_iv != null && max_iv != null && (max_iv - min_iv) != 0.0)
                        {
                            iv_rank = (last_iv - min_iv) * 100.0 / (max_iv - min_iv);
                        }

                        if (last_iv == null)
                        {
                            UpdateIVMessage (string.Format ("Missing iv value on {0}. Will skip.\r\n", ticker));
                            continue;
                        }

                        /* Compute iv percentile
                            * --------------------- */

                        int below = 0;
                        int above = 0;
                        foreach (var iv in ivs)
                        {
                            if (iv.ClosingIV != null && last_iv != null)
                            {
                                if (iv.ClosingIV < last_iv)
                                {
                                    below++;
                                }
                                else
                                {
                                    above++;
                                }
                            }
                        }
                        if (above + below != 0)
                        {
                            iv_percentile = 100.0 - above * 100.0 / (above + below);
                        }

                        /* Now compute the price changes
                         * ----------------------------- */

                        var query = (from s in dc.Stocks
                                     where ticker == s.Ticker
                                     select s).First ();

                        var prices = (from ps in dc.PriceHistories
                                        where ps.Ticker == ticker
                                        && ps.PriceDate >= TrendDate && ps.PriceTime == new TimeSpan (0, 0, 0)
                                        && ps.ClosingPrice != null
                                        orderby ps.PriceDate descending
                                        select new Tuple<double, DateTime> ((double) ps.ClosingPrice, ps.PriceDate)
                                ).ToList ();

                        double CurrentPrice = 0.0;

                        if (prices.Count > 0)
                        { 
                            CurrentPrice = prices.First ().Item1;

                            query.PriceChange5Day = GetPriceChange (prices, new TimeSpan (5, 0, 0, 0));
                            query.PriceChange10Day = GetPriceChange (prices, new TimeSpan (10, 0, 0, 0));
                            query.PriceChange15Day = GetPriceChange (prices, new TimeSpan (15, 0, 0, 0));

                            query.LastTrade = (decimal?) CurrentPrice; // this should always be the case
                        }
                        else
                        {
                            query.PriceChange5Day = null;
                            query.PriceChange10Day = null;
                            query.PriceChange15Day = null;
                            query.LastTrade = null;
                        }
                        query.IVPercentile = iv_percentile;
                        query.IVRank = iv_rank;

                        tiv.Add (new TickerBB (ticker, query.Company, CurrentDate, CurrentPrice, iv_rank, iv_percentile, query.PriceChange5Day, query.PriceChange10Day, query.PriceChange15Day, null));

                        dc.SubmitChanges ();
                    }

                    return tiv;
                }
            });
        }

        /*************************************************************
         * 
         * UpdateIVMessage from worker thread
         * 
         * **********************************************************/

        private void UpdateIVMessage (string text)
        {
            if (tbIVMsg.InvokeRequired)
            {
                UpdateIVMessageDelegate d = new UpdateIVMessageDelegate (UpdateIVMessage);
                this.Invoke (d, new object[] { text });
            }
            else
            {
                tbIVMsg.Text += text;
            }
        }
        /*************************************************************
         * 
         * UpdateBBMessage from worker thread
         * 
         * **********************************************************/

        private void UpdateBBMessage (string text)
        {
            if (tbBBMsg.InvokeRequired)
            {
                UpdateBBMessageDelegate d = new UpdateBBMessageDelegate (UpdateBBMessage);
                this.Invoke (d, new object[] { text });
            }
            else
            {
                tbBBMsg.Text += text;
            }
        }

        /********************************************************
         * 
         * Get Price Change as a percent
         * 
         * *****************************************************/

        private double GetPriceChange (List<Tuple<double, DateTime>> prices, TimeSpan timeSpan)
        {
            DateTime lastday = prices.First ().Item2;
            double lastprice = prices.First ().Item1;

            foreach (var t in prices)
            {
                if (lastday - t.Item2 >= timeSpan)
                {
                    /* Price should never be zero, but sometimes it does!
                     * --------------------------------------------------
                     */

                    if (t.Item1 != 0)
                    {
                        return (lastprice - t.Item1) / t.Item1 * 100.0;
                    }
                    return 0.0;
                }
            }
            return 0.0;
        }

        /****************************************************************
         * 
         * Retrieve Company Info
         * 
         * *************************************************************/

        private void btnCompanyInfoFetch_Click (object sender, EventArgs e)
        {
            PortfolioDesc d = (PortfolioDesc) lbxPortfolio.SelectedItem;

            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {
                var q = (from ps in dc.PortfolioStocks
                         join s in dc.Stocks on ps.Ticker equals s.Ticker
                         where ps.PortfolioName == d.PortfolioName
                         select new
                         {
                             s.Ticker,
                             s.Company,
                             s.Sector,
                             s.Industry,
                             s.LastTrade,
                             s.MarketCap,
                             s.DailyVolume,
                             s.Ex_DividendDate,
                             s.NextEarnings,
                             s.IVPercentile,
                             s.PriceChange5Day,
                             s.PriceChange10Day,
                             s.PriceChange15Day
                         });

                dgvCompanyInfo.AutoGenerateColumns = false;
                dgvCompanyInfo.DataSource = q;
                int i = 0;
                bSettingWidthsCompanyInfo = true;
                foreach (DataGridViewColumn col in dgvCompanyInfo.Columns)
                {
                    int col_width;
                    if (i < CompanyInfoColumnWidths.Count)
                    {
                        if (int.TryParse (CompanyInfoColumnWidths[i++], out col_width))
                        {
                            col.Width = col_width;
                        }
                    }
                }
                bSettingWidthsCompanyInfo = false;
            }
        }

        private void dgvCompanyInfo_ColumnWidthChanged (object sender, DataGridViewColumnEventArgs e)
        {
            if (!bSettingWidthsCompanyInfo)
            {
                if (CompanyInfoColumnWidths != null)
                {
                    CompanyInfoColumnWidths = new System.Collections.Specialized.StringCollection ();

                    foreach (DataGridViewColumn col in dgvCompanyInfo.Columns)
                    {
                        CompanyInfoColumnWidths.Add (col.Width.ToString ());
                    }
                }
            }
        }

        /***********************************************************
         * 
         * Form1_Load
         * 
         * ********************************************************/
        
        private void Form1_Load (object sender, EventArgs e)
        {
            CompanyInfoColumnWidths = Properties.Settings.Default.CompanyInfoColumnWidths;
            StockAnalColumnWidths = Properties.Settings.Default.StockAnalColumnWidths;
            OptionAnalColumnWidths = Properties.Settings.Default.OptionAnalColumnWidths;
            BBColumnWidths = Properties.Settings.Default.BBColumnWidths;
            HistoricalBBColumnWidths = Properties.Settings.Default.HistoricalBBColumnWidths;
            IVPercentileColumnWidths = Properties.Settings.Default.IVPercentileColumnWidths;
        }

        /***********************************************************
         * 
         * Form1_Shown
         * 
         * ********************************************************/
        
        private void Form1_Shown (object sender, EventArgs e)
        {
            bSettingWidthsSuppressRecording = true;

            int col_no = 0;
            foreach (DataGridViewColumn col in dgvAnalOption.Columns)
            {
                if (col_no < OptionAnalColumnWidths.Count)
                {
                    int col_width;
                    if (int.TryParse (OptionAnalColumnWidths[col_no++], out col_width))
                    {
                        col.Width = col_width;
                    }
                }
            }

            bSettingWidthsSuppressRecording = false;

            tbAnalMinPrice.Text = Properties.Settings.Default.AnalMinPrice.ToString ();
            tbAnalMinVolume.Text = Properties.Settings.Default.AnalMinVolume.ToString ();
            tbMinIVPercentile.Text = Properties.Settings.Default.AnalMinIVPercentile.ToString ("N0");
            dtpAnalNixEarnings.Value = Properties.Settings.Default.AnalNixEarnings;
            rbTWS.Checked = Properties.Settings.Default.ConnectTWS;
            tbClientId.Text = Properties.Settings.Default.ClientID.ToString ();
            lbxPortfolio.SelectedIndex = Properties.Settings.Default.PortfolioIndex;

            ckbAnalOptionPuts.Checked = Properties.Settings.Default.bAnalOptPuts;
            ckbAnalOptionCalls.Checked = Properties.Settings.Default.bAnalOptCalls;
            ckbStrangleThreshold.Checked = Properties.Settings.Default.bAnalOptStrangleThreshold;
            ckbOIThreshold.Checked = Properties.Settings.Default.bAnalOptOIThreshold;
            ckbROCThreshold.Checked = Properties.Settings.Default.bAnalOptROCThreshold;
            ckbMinPremium.Checked = Properties.Settings.Default.bAnalOptMinPremium;
            tbITMThreshold.Text = Properties.Settings.Default.AnalOptITMThreshold;
            tbOIThreshold.Text = Properties.Settings.Default.AnalOptOIThreshold.ToString ();
            tbROCThreshold.Text = Properties.Settings.Default.AnalOptROCThreshold;
            tbMinPremium.Text = Properties.Settings.Default.AnalOptMinPremium;
        }

        private void Form1_FormClosing (object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.CompanyInfoColumnWidths = CompanyInfoColumnWidths;
            Properties.Settings.Default.StockAnalColumnWidths = StockAnalColumnWidths;
            Properties.Settings.Default.OptionAnalColumnWidths = OptionAnalColumnWidths;
            Properties.Settings.Default.BBColumnWidths = BBColumnWidths;
            Properties.Settings.Default.HistoricalBBColumnWidths = HistoricalBBColumnWidths;
            Properties.Settings.Default.IVPercentileColumnWidths = IVPercentileColumnWidths;
            Properties.Settings.Default.Save ();

            if (m_StockInfoQueue != null)
            {
                m_StockInfoQueue.Dispose ();
            }
            m_Log.Dispose ();
            if (m_IfConnected)
            {
                IBapi.ib.ClientSocket.eDisconnect (true);
            }
        }

        private void dgvCompanyInfo_CellFormatting (object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvCompanyInfo.Columns[e.ColumnIndex].Name == "PriceDirection")
            {
                if (e.Value != null)
                {
                    e.Value = ((double) e.Value).ToString ("N2") + " %";
                }
            }
        }

 
        /*******************************************************************
         * 
         * Compute the next few expiry dates
         * 
         * ****************************************************************/

        static List<DateTime> ComputeExpiryDates ()
        {
            DateTime d = DateTime.Now;

            List<DateTime> dates = new List<DateTime> ();

            for (int i = 0; i < 3; i++)
            {
                d = Utils.ComputeNextExpiryDate (d);
                dates.Add (d);
                d += new TimeSpan (1, 0, 0, 0);
            }
            return dates;
        }

        /*******************************************************************
         * 
         * Tab Index changed
         * 
         * ****************************************************************/

        private void tabControl1_SelectedIndexChanged (object sender, EventArgs e)
        {
            if (tabControl1.TabPages[tabControl1.SelectedIndex].Name == "tabPercentile")
            {
                 bSettingWidthsSuppressRecording = true;

                int col_no = 0;
 
                foreach (DataGridViewColumn col in dgvIVPercentile.Columns)
                {
                    if (col_no < IVPercentileColumnWidths.Count)
                    {
                        int col_width;
                        if (int.TryParse (IVPercentileColumnWidths[col_no++], out col_width))
                        {
                            col.Width = col_width;
                        }
                    }
                }

                bSettingWidthsSuppressRecording = false;
            }

            if (tabControl1.TabPages[tabControl1.SelectedIndex].Name == "tabBollingerBands")
            {
                m_BollingerBandDate = DateTime.Today;
                if (!m_BollingerBandDate.IsTodayBusinessDay ())
                {
                    m_BollingerBandDate = m_BollingerBandDate.PreviousBusinessDay ();
                }

                btnBBCompute.Text = "Compute BB for " + m_BollingerBandDate.ToString ("dd-MMM-yyyy");

                bSettingWidthsSuppressRecording = true;

                int col_no = 0;
 
                foreach (DataGridViewColumn col in dgvTickerBB.Columns)
                {
                    if (col_no < BBColumnWidths.Count)
                    {
                        int col_width;
                        if (int.TryParse (BBColumnWidths[col_no++], out col_width))
                        {
                            col.Width = col_width;
                        }
                    }
                }

                col_no = 0;
                foreach (DataGridViewColumn col in dgvHistoricalBB.Columns)
                {
                    if (col_no < HistoricalBBColumnWidths.Count)
                    {
                        int col_width;
                        if (int.TryParse (HistoricalBBColumnWidths[col_no++], out col_width))
                        {
                            col.Width = col_width;
                        }
                    }
                }

                bSettingWidthsSuppressRecording = false;
            }

            if (tabControl1.TabPages[tabControl1.SelectedIndex].Name == "tabScanner")
            {
                if (lbxDefinedScans.DataSource == null)
                {
                    if (m_IfConnected)
                    {
                        EnumerateScannerParameters ();
                    }
                }
            }

            if (tabControl1.TabPages[tabControl1.SelectedIndex].Name == "tabAnalyze")
            {
                if (cbAnalExpires.DataSource == null)
                {
                    cbAnalExpires.DataSource = ComputeExpiryDates ();
                }
            }
            
            if (tabControl1.TabPages[tabControl1.SelectedIndex].Name == "tabHistorical")
            {
                btnStartHistorical.Enabled = m_IfConnected;

                TimeSpan Interval = new TimeSpan (5, 0, 0, 0); // 5 days

                using (dbOptionsDataContext dc = new dbOptionsDataContext ())
                {
                    DateTime LastPriceDate = DateTime.MinValue;
                    DateTime LastIVDate = DateTime.MinValue;

                    while (true)
                    {

                        var q = (from ph in dc.PriceHistories
                                 where (ph.PriceDate >= DateTime.Now - Interval && ph.Ticker == "SPY")
                                 orderby ph.PriceDate descending
                                 select new
                                       {
                                           ph.PriceDate,
                                           ph.ClosingIV,
                                           ph.ClosingPrice
                                       }
                                 ).ToList ();

                        foreach (var p in q)
                        {
                            if (LastPriceDate == DateTime.MinValue && p.ClosingPrice != null)
                            {
                                LastPriceDate = p.PriceDate;
                            }
                            if (LastIVDate == DateTime.MinValue && p.ClosingIV != null)
                            {
                                LastIVDate = p.PriceDate;
                            }
                            if (LastPriceDate != DateTime.MinValue && LastIVDate != DateTime.MinValue)
                            {
                                break;
                            }
                        }
                        if (LastPriceDate != DateTime.MinValue && LastIVDate != DateTime.MinValue)
                        {
                            lbLastIVDate.Text = LastIVDate.ToString ("D");
                            lbLastPriceDate.Text = LastPriceDate.ToString ("D");
                            return;
                        }
                        Interval += new TimeSpan (5, 0, 0, 0); // increase interval by another5 days
                    }

                }
            }
        }

        /*********************************************************************
         * 
         * Enumerate Scanner Parameters
         * 
         * ******************************************************************/

        private Task EnumerateScannerParameters ()
        {
            /*            return Task.Run (() =>
                        {

                            var scanparametershandler = default (AxTWSLib._DTwsEvents_scannerParametersEventHandler);
                            var errhandler = default (AxTWSLib._DTwsEvents_errMsgEventHandler);

                            errhandler = new AxTWSLib._DTwsEvents_errMsgEventHandler ((s, e) =>
                            {
                                axTws.scannerParameters -= scanparametershandler;
                                axTws.errMsg -= errhandler;
                            });

                            scanparametershandler = new AxTWSLib._DTwsEvents_scannerParametersEventHandler ((s, e) =>
                            {
                                string xml = e.xml;
                                XDocument xdocument = XDocument.Parse (xml);

                                IEnumerable<XElement> ScanCodes = xdocument.Descendants ("ScanType");

                                List<scantypes> DefinedScans = new List<scantypes> ();

                                foreach (var scancode in ScanCodes)
                                {
                                    if (scancode.Element ("displayName") != null)
                                    {
                                        if (scancode.Element ("instruments").Value.Contains ("STK"))
                                        {
                                            DefinedScans.Add (new scantypes (scancode.Element ("displayName").Value, scancode.Element ("scanCode").Value));

                                        }
                                        string x = scancode.Element ("displayName").Value;
                                    }
                                }

                                lbxDefinedScans.DisplayMember = "DisplayName";
                                lbxDefinedScans.DataSource = DefinedScans;

                                axTws.scannerParameters -= scanparametershandler;
                                axTws.errMsg -= errhandler;
                            });

                            axTws.errMsg += errhandler;
                            axTws.scannerParameters += scanparametershandler;

                            axTws.reqScannerParameters ();
                        });*/
            return null;
        }

        /***********************************************************************
         * 
         * start scanning
         * 
         * ********************************************************************/

        private void btnScan_Click (object sender, EventArgs ev)
        {
            if (m_IfScanning)
            {
                m_Log.Log (ErrorLevel.logINF, "request scanning to be canceled");
                m_bCancelScanning = true;
                //axTws.cancelScannerSubscription (ID_MARKETSCANNER);
                btnScan.Text = "Scan";
            }
            else
            {
                if (lbxDefinedScans.SelectedItem == null)
                {
                    MessageBox.Show ("What kind of scan do you want to run?");
                    return;
                } 
                
                string locations = string.Join (",", (from string sc in lbxLocationCode.SelectedItems
                                                  select sc
                                                   )
                                           );
                string ScanCode = ((scantypes) lbxDefinedScans.SelectedItem).ScanCode;
                string StockTypeFilter = lbxStockFilterType.SelectedItem.ToString ();

                double PriceAbove = 0;
                if (!string.IsNullOrWhiteSpace (tbPriceAbove.Text))
                {
                    PriceAbove = double.Parse (tbPriceAbove.Text);
                }
                int Volume = 0;
                if (!string.IsNullOrWhiteSpace (tbVolumeAbove.Text))
                {
                    Volume = int.Parse (tbVolumeAbove.Text, NumberStyles.AllowThousands);
                }
                double MarketCap = 0;
                if (!string.IsNullOrWhiteSpace (tbMarketCapAbove.Text))
                {
                    MarketCap = double.Parse (tbMarketCapAbove.Text, NumberStyles.AllowThousands
                                                                    | NumberStyles.AllowDecimalPoint
                                                                    | NumberStyles.AllowLeadingWhite
                                                                    | NumberStyles.AllowTrailingWhite);
                }
                int OptionVol = 0;
                if (!string.IsNullOrWhiteSpace (tbOptionsVolAbove.Text))
                {
                    OptionVol = int.Parse (tbOptionsVolAbove.Text);
                }
                int NoEquities = 0;
                if (!string.IsNullOrWhiteSpace (tbNoEquities.Text))
                {
                    NoEquities = int.Parse (tbNoEquities.Text);
                }
                btnScan.Text = "Stop Scan";
                ScanData (locations, 
                          ScanCode, 
                          StockTypeFilter,
                          ckbPriceAbove.Checked, PriceAbove, 
                          ckbVolumeAbove.Checked, Volume, 
                          ckbMarketCap.Checked, MarketCap,
                          ckbOptionsVolAbove.Checked, OptionVol,
                          ckbNoEquities.Checked, NoEquities);
            }
        }

        private Task ScanData (string SelectedLocations, 
                               string ScanCode, 
                               string StockTypeFilter,                   
                               bool bIfPriceAbove, 
                               double Price, 
                               bool bIfVolumeAbove, 
                               int Volume, 
                               bool bIfMarketCapAbove, 
                               double MarketCap,
                               bool bIfOptionsVolAbove,
                               int OptionsVolume,
                               bool bIfNoEquities,
                               int NoEquities)
        {
            /*           return Task.Run (() =>
                       {
                           m_bCancelScanning = false;
                           List<ScannerData> ScannerData = new List<ScannerData> ();

                           var scannerdatahandler = default (AxTWSLib._DTwsEvents_scannerDataExEventHandler);
                           var errhandler = default (AxTWSLib._DTwsEvents_errMsgEventHandler);
                           var endhandler = default (AxTWSLib._DTwsEvents_scannerDataEndEventHandler);

                           errhandler = new AxTWSLib._DTwsEvents_errMsgEventHandler ((s, e) =>
                           {
                               axTws.scannerDataEx -= scannerdatahandler;
                               axTws.errMsg -= errhandler;
                               axTws.scannerDataEnd -= endhandler;

                               axTws.cancelScannerSubscription (ID_MARKETSCANNER);
                               m_Log.Log (ErrorLevel.logINF, "Scanning canceled");
                           });

                           endhandler = new AxTWSLib._DTwsEvents_scannerDataEndEventHandler ((s, e) =>
                           {
                               m_DisplayedScannerData = ScannerData;
                               SortableBindingList<ScannerData> bl = new SortableBindingList<ScannerData> (m_DisplayedScannerData);
                               dgvScanner.DataSource = bl;
                               ScannerData = new List<ScannerData> ();

                               if (ckbScanOnceOnly.Checked)
                               {
                                   axTws.cancelScannerSubscription (ID_MARKETSCANNER);
                                   m_IfScanning = false;
                                   btnScan.Text = "Scan";

                                   axTws.scannerDataEx -= scannerdatahandler;
                                   axTws.errMsg -= errhandler;
                                   axTws.scannerDataEnd -= endhandler;

                                   axTws.cancelScannerSubscription (ID_MARKETSCANNER);
                                   m_Log.Log (ErrorLevel.logINF, "Scanning canceled");
                               }
                           });

                           scannerdatahandler = new AxTWSLib._DTwsEvents_scannerDataExEventHandler ((s, e) =>
                           {
                               if (m_bCancelScanning)
                               {
                                   axTws.cancelScannerSubscription (ID_MARKETSCANNER);
                                   m_IfScanning = false;
                                   btnScan.Text = "Scan";

                                   axTws.scannerDataEx -= scannerdatahandler;
                                   axTws.errMsg -= errhandler;
                                   axTws.scannerDataEnd -= endhandler;

                                   axTws.cancelScannerSubscription (ID_MARKETSCANNER);
                                   m_Log.Log (ErrorLevel.logINF, "Scanning canceled");
                               }

                               ScannerData sd = new ScannerData ();

                               object o = e.contractDetails.summary;

                               Type objType = o.GetType ();
                               sd.symbol = (string) objType.InvokeMember ("symbol", BindingFlags.GetProperty, null, o, null);
                               sd.sectype = (string) objType.InvokeMember ("secType", BindingFlags.GetProperty, null, o, null);
                               sd.expiry = (string) objType.InvokeMember ("expiry", BindingFlags.GetProperty, null, o, null);
                               sd.strike = (double) objType.InvokeMember ("Strike", BindingFlags.GetProperty, null, o, null);
                               sd.right = (string) objType.InvokeMember ("Right", BindingFlags.GetProperty, null, o, null);
                               sd.exchange = (string) objType.InvokeMember ("exchange", BindingFlags.GetProperty, null, o, null);
                               sd.currency = (string) objType.InvokeMember ("currency", BindingFlags.GetProperty, null, o, null);
                               sd.localSymbol = (string) objType.InvokeMember ("localSymbol", BindingFlags.GetProperty, null, o, null);
                               sd.tradingClass = (string) objType.InvokeMember ("tradingClass", BindingFlags.GetProperty, null, o, null);
                               sd.marketName = sd.marketName;
                               sd.distance = sd.distance;
                               sd.benchmark = sd.benchmark;
                               sd.projection = sd.projection;
                               sd.legStr = sd.legStr;

                               ScannerData.Add (sd);
                           });

                           IScannerSubscription ss = axTws.createScannerSubscription ();

                           ss.scanCode = ScanCode;
                           ss.instrument = "STK";
                           ss.locations = SelectedLocations;

                           ss.stockTypeFilter = StockTypeFilter;
                           if (bIfPriceAbove)
                           {
                               ss.priceAbove = Price;
                           }
                           if (bIfVolumeAbove)
                           {
                               ss.volumeAbove = Volume;
                           }
                           if (bIfMarketCapAbove)
                           {
                               ss.marketCapAbove = MarketCap;
                           }
                           if (bIfOptionsVolAbove)
                           {
                               ss.averageOptionVolumeAbove = OptionsVolume;
                           }

                           if (bIfNoEquities)
                           {
                               ss.numberOfRows = NoEquities;
                           }

                           axTws.reqMarketDataType (ckbUseFrozenData.Checked ? 2 : 1);

                           ScannerData = new List<ScannerData> ();

                           axTws.scannerDataEx += scannerdatahandler;
                           axTws.errMsg += errhandler;
                           axTws.scannerDataEnd += endhandler;

                           m_Log.Log (ErrorLevel.logINF, "Scanning started");
                           axTws.reqScannerSubscriptionEx (ID_MARKETSCANNER, ss, null);
                           m_IfScanning = true;
                       });*/
            return null;
        }


        private class scantypes
        {
            public string DisplayName { get; set; }
            public string ScanCode { get; set; }

            public scantypes (string display, string scan)
            {
                DisplayName = display;
                ScanCode = scan;
            }
        };

        /***********************************************************************
         * 
         * receive Scanner parameters
         * 
         * ********************************************************************/
        //private void axTws_scannerParameters (object sender, AxTWSLib._DTwsEvents_scannerParametersEvent e)
        //{
        //    string xml = e.xml;
        //    XDocument xdocument = XDocument.Parse (xml);

        //    IEnumerable<XElement> ScanCodes = xdocument.Descendants ("ScanType");

        //    List<scantypes> DefinedScans = new List<scantypes> ();

        //    foreach (var scancode in ScanCodes)
        //    {
        //        if (scancode.Element ("displayName") != null)
        //        {
        //            if (scancode.Element ("instruments").Value.Contains ("STK"))
        //            {
        //                DefinedScans.Add (new scantypes (scancode.Element ("displayName").Value, scancode.Element ("scanCode").Value));

        //            }
        //            string x = scancode.Element ("displayName").Value;
        //        }
        //    }

        //    lbxDefinedScans.DisplayMember = "DisplayName";
        //    lbxDefinedScans.DataSource = DefinedScans;
        //}

        private class ScannerData
        {
            public string symbol { get; set; }
            public double implied_vol { get; set; }
            public string sectype { get; set; }
            public string expiry { get; set; }
            public double strike { get; set; }
            public string right { get; set; }
            public string exchange { get; set; }
            public string currency { get; set; }
            public string localSymbol { get; set; }
            public string marketName { get; set; }
            public string tradingClass { get; set; }
            public string distance { get; set; }
            public string benchmark { get; set; }
            public string projection { get; set; }
            public string legStr { get; set; }

        }

        /*********************************************************
         * 
         * Receiving scanner data
         * 
         * *******************************************************/
        //private void axTws_scannerDataEx (object sender, AxTWSLib._DTwsEvents_scannerDataExEvent e)
        //{
        //    ScannerData sd = new ScannerData ();

        //    object o = e.contractDetails.summary;

        //    Type objType = o.GetType ();
        //    sd.symbol = (string) objType.InvokeMember ("symbol", BindingFlags.GetProperty, null, o, null);
        //    sd.sectype = (string) objType.InvokeMember ("secType", BindingFlags.GetProperty, null, o, null);
        //    sd.expiry = (string) objType.InvokeMember ("expiry", BindingFlags.GetProperty, null, o, null);
        //    sd.strike = (double) objType.InvokeMember ("Strike", BindingFlags.GetProperty, null, o, null);
        //    sd.right = (string) objType.InvokeMember ("Right", BindingFlags.GetProperty, null, o, null);
        //    sd.exchange = (string) objType.InvokeMember ("exchange", BindingFlags.GetProperty, null, o, null);
        //    sd.currency = (string) objType.InvokeMember ("currency", BindingFlags.GetProperty, null, o, null);
        //    sd.localSymbol = (string) objType.InvokeMember ("localSymbol", BindingFlags.GetProperty, null, o, null);
        //    sd.tradingClass = (string) objType.InvokeMember ("tradingClass", BindingFlags.GetProperty, null, o, null);
        //    sd.marketName = sd.marketName;
        //    sd.distance = sd.distance;
        //    sd.benchmark = sd.benchmark;
        //    sd.projection = sd.projection;
        //    sd.legStr = sd.legStr;

        //    m_ScannerData.Add (sd);
        //}

        /***********************************************************
         * 
         * Scanner data has ended
         * 
         * *********************************************************/

        //private void axTws_scannerDataEnd (object sender, AxTWSLib._DTwsEvents_scannerDataEndEvent e)
        //{
        //    FilterAgainstPortfolio ();

        //    if (ckbScanOnceOnly.Checked)
        //    {
        //        axTws.cancelScannerSubscription (ID_MARKETSCANNER);
        //        m_IfScanning = false;
        //        btnScan.Text = "Scan";
        //    }
        //}

        /***********************************************************
         * 
         * Fetch implied volatility -- doesn't work. Only gets option bid / ask vol, etc.
         * 
         * ********************************************************/
        private void btnFetchIV_Click (object sender, EventArgs e)
        {
/*            IContract contract = axTws.createContract ();

            for (int i = 0; i < m_DisplayedScannerData.Count; i++)
            {
                if (ckbSnapshot.Checked)
                {
                    var scanner = m_DisplayedScannerData[i];
                    contract.symbol = scanner.symbol;
                    contract.secType = scanner.sectype;
                    contract.expiry = scanner.expiry;
                    contract.strike = scanner.strike;
                    contract.right = scanner.right;
                    contract.multiplier = "";
                    contract.exchange = scanner.exchange;
                    contract.primaryExchange = "";
                    contract.currency = scanner.currency;
                    contract.localSymbol = scanner.localSymbol;
                    contract.includeExpired = 0;

                    axTws.reqMktDataEx (Constants.SCANNER_MARKET_DATA | i, contract, "", 1, null);
                }
                else
                {
                    var scanner = m_DisplayedScannerData[i];
                    contract.symbol = scanner.symbol;
                    contract.secType = scanner.sectype;
                    contract.expiry = scanner.expiry;
                    contract.strike = scanner.strike;
                    contract.right = scanner.right;
                    contract.multiplier = "";
                    contract.exchange = scanner.exchange;
                    contract.primaryExchange = "";
                    contract.currency = scanner.currency;
                    contract.localSymbol = "";
                    contract.includeExpired = 0;
                    axTws.reqMktDataEx (Constants.SCANNER_MARKET_DATA | i, contract, "100,101,104,106", 0, null);
                }
                break;
            }*/
        }

        /***********************************************************
         * 
         * Filter again portfolio
         * 
         * *********************************************************/

        private void btnFilterAgainstPortfolio_Click (object sender, EventArgs e)
        {
            FilterAgainstPortfolio ();
        }

        private void FilterAgainstPortfolio ()
        {
            PortfolioDesc d = (PortfolioDesc) lbxPortfolio.SelectedItem;

            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {
                var TickerList = (from ps in dc.PortfolioStocks
                                  join s in dc.Stocks on ps.Ticker equals s.Ticker
                                  where ps.PortfolioName == d.PortfolioName
                                  select s.Ticker
                                 ).ToList ();


                var NewScannerList = (from tl in TickerList
                                      join s in m_DisplayedScannerData on tl equals s.symbol
                                      select s).ToList ();

                m_DisplayedScannerData = NewScannerList;
                SortableBindingList<ScannerData> bl = new SortableBindingList<ScannerData> (m_DisplayedScannerData);
                dgvScanner.DataSource = bl;
            }
        }

        /*************************************************************************
         * 
         * Retrieve portfolio stocks for analysis
         * 
         * **********************************************************************/

        private void btnPortfolioAnal_Click (object sender, EventArgs e)
        {
            PortfolioDesc d = (PortfolioDesc) lbxPortfolio.SelectedItem;

            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {
                var q = (from ps in dc.PortfolioStocks
                         join s in dc.Stocks on ps.Ticker equals s.Ticker
                         where ps.PortfolioName == d.PortfolioName
                         select new StockAnal (s.Ticker,
                                               s.Company,
                                               s.Sector,
                                               s.Industry,
                                               s.LastTrade,
                                               s.MarketCap,
                                               s.DailyVolume,
                                               s.Ex_DividendDate,
                                               s.NextEarnings,
                                               s.AnalystsRating,
                                               s.IVRank,
                                               s.IVPercentile,
                                               s.PriceChange5Day,
                                               s.PriceChange10Day,
                                               s.PriceChange15Day,
                                               s.PercentBB,
                                               s.SecType,
                                               s.Exchange)
                        );
                
                m_ViewableAnalStocks = new SortableBindingListView<StockAnal> (q);
                dgvAnalStock.AutoGenerateColumns = false;
                BindingSource bs = new BindingSource ();
                bs.DataSource = m_ViewableAnalStocks;
                dgvAnalStock.DataSource = bs;
                lbAnalStocksNo.Text = m_ViewableAnalStocks.Count.ToString ();

                bSettingWidthsSuppressRecording = true;
                int i = 0;
                foreach (DataGridViewColumn col in dgvAnalStock.Columns)
                {
                    int col_width;
                    if (i < StockAnalColumnWidths.Count)
                    {
                        if (int.TryParse (StockAnalColumnWidths[i++], out col_width))
                        {
                            col.Width = col_width;
                        }
                    }
                }
                bSettingWidthsSuppressRecording = false;
            }
        }

        /************************************************************
         * 
         * ColumnWidthChanged in StockAnalyze
         * 
         * *********************************************************/

        private void dgvStockAnalyze_ColumnWidthChanged (object sender, DataGridViewColumnEventArgs e)
        {
            if (!bSettingWidthsSuppressRecording)
            {
                if (StockAnalColumnWidths != null)
                {
                    StockAnalColumnWidths = new System.Collections.Specialized.StringCollection ();

                    foreach (DataGridViewColumn col in dgvAnalStock.Columns)
                    {
                        StockAnalColumnWidths.Add (col.Width.ToString ());
                    }
                }
            }
        }

        /************************************************************
         * 
         * ColumnWidthChanged in Option Chain Analyze
         * 
         * *********************************************************/

        private void dgvAnalOption_ColumnWidthChanged (object sender, DataGridViewColumnEventArgs e)
        {
            if (!bSettingWidthsSuppressRecording)
            {
                if (OptionAnalColumnWidths != null)
                {
                    OptionAnalColumnWidths = new System.Collections.Specialized.StringCollection ();

                    foreach (DataGridViewColumn col in dgvAnalOption.Columns)
                    {
                        OptionAnalColumnWidths.Add (col.Width.ToString ());
                    }
                }
            }
        }

        /************************************************************
         * 
         * Apply a filter to the stock data on the analysis tab
         * 
         * *********************************************************/

        private void btnAnalApply_Click (object sender, EventArgs e)
        {
            if (m_bAnalFilterApplied)
            {
                BindingSource bs = new BindingSource ();
                bs.DataSource = m_ViewableAnalStocks;
                dgvAnalStock.DataSource = bs;
                if (m_ViewableAnalStocks != null)
                {
                    lbAnalStocksNo.Text = m_ViewableAnalStocks.Count.ToString ();
                }
                m_bAnalFilterApplied = false;
                btnAnalApply.Text = "Apply";
                return;
            }

            m_bAnalFilterApplied = true;
            btnAnalApply.Text = "Undo";

            m_ViewableAnalStocks = ((BindingSource) dgvAnalStock.DataSource).DataSource as SortableBindingListView<StockAnal>;
            //m_ViewableAnalStocks = dgvAnalStock.DataSource as SortableBindingList<StockAnal>;

            if (m_ViewableAnalStocks == null)
            {
                return;
            }

            SortableBindingListView<StockAnal> sa = new SortableBindingListView<StockAnal> ();

            List<string> PortfolioList = null;

            if (ckbAnalIntersectPortfolio.Checked)
            {
               PortfolioDesc d = (PortfolioDesc) lbxPortfolio.SelectedItem;

                using (dbOptionsDataContext dc = new dbOptionsDataContext ())
                {
                    PortfolioList = (from ps in dc.PortfolioStocks
                                         join s in dc.Stocks on ps.Ticker equals s.Ticker
                                         where ps.PortfolioName == d.PortfolioName
                                         select s.Ticker
                                         ).ToList ();
                }
            }

            double minBB;
            double maxBB;

            if (!double.TryParse (tbMinBB.Text, out minBB))
            {
                MessageBox.Show ("Invalid minimum BB specified.");
                return;
            }
            if (!double.TryParse (tbMaxBB.Text, out maxBB))
            {
                MessageBox.Show ("Invalid maximum BB specified.");
                return;
            }

            foreach (var s in m_ViewableAnalStocks)
            {
                if (ckbAnalMinPrice.Checked)
                {
                    double t;
                    if (!double.TryParse (tbAnalMinPrice.Text, out t))
                    {
                        MessageBox.Show ("Invalid Min Price.");
                        break;
                    }
                    if (s.LastDailyTrade == null)
                    {
                        continue;
                    }
                    if ((double) s.LastDailyTrade < t)
                    {
                        continue;
                    }
                }
                if (ckbAnalMinVolume.Checked)
                {
                    int t;
                    if (!int.TryParse (tbAnalMinVolume.Text, out t))
                    {
                        MessageBox.Show ("Invalid Min Volume.");
                        break;
                    }
                    if (s.DailyVol == null)
                    {
                        continue;
                    }
                    if (s.DailyVol < t)
                    {
                        continue;
                    }
                }
                if (ckbAnalMinIVPercentile.Checked)
                {
                    double t;
                    if (!double.TryParse (tbMinIVPercentile.Text, out t))
                    {
                        MessageBox.Show ("Invalid Min IV  Percentile.");
                        continue;
                    }
                    if (s.IVPercentile == null)
                    {
                        continue;
                    }
                    if (s.IVPercentile < t)
                    {
                        continue;
                    }
                }
                if (ckbAnalMaxIVPercentile.Checked)
                {
                    double t;
                    if (!double.TryParse (tbMaxIVPercentile.Text, out t))
                    {
                        MessageBox.Show ("Invalid Max IV  Percentile.");
                        continue;
                    }
                    if (s.IVPercentile == null)
                    {
                        continue;
                    }
                    if (s.IVPercentile > t)
                    {
                        continue;
                    }
                }
                if (ckbAnalNixEarnings.Checked)
                {
                    if (!string.IsNullOrEmpty (s.NextEarnings))
                    {
                        DateTime dt;
                        if (DateTime.TryParseExact (s.NextEarnings.Substring (0, 8), "yy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        {
                            if (dt < dtpAnalNixEarnings.Value && dt >= DateTime.Now)
                            {
                                continue;
                            }
                        }
                    }
                }
                if (ckbAnalNixDividends.Checked)
                {
                    if (s.Ex_DividendDate != null)
                    {
                        if (s.Ex_DividendDate < dtpAnalDividends.Value)
                        {
                            continue;
                        }
                    }
                }
                if (ckbAnalIntersectPortfolio.Checked)
                {
                    if (!PortfolioList.Contains (s.Ticker))
                    {
                        continue;
                    }
                }
                if (ckbAnalMinBB.Checked)
                {
                    if (s.PercentBB < minBB)
                    {
                        continue;
                    }
                }
                if (ckbAnalMaxBB.Checked)
                {
                    if (s.PercentBB > maxBB)
                    {
                        continue;
                    }
                }

                sa.Add (s);
            }

            BindingSource b = new BindingSource ();
            b.DataSource = sa;
            dgvAnalStock.DataSource = b;
            lbAnalStocksNo.Text = sa.Count.ToString ();
        }

        /****************************************************************
         * 
         * Fetch option chain of selected stock
         * 
         * *************************************************************/

        private async void btnAnalOptionChain_Click (object sender, EventArgs e)
        {
            try
            {
                Assembly _assembly = Assembly.GetExecutingAssembly ();
                Stream _imageStream = _assembly.GetManifestResourceStream ("IBFetchData.Images.spiffygif_30x30.gif");
                pbAnalOption.Image = new Bitmap (_imageStream);
            }
            catch
            {
                MessageBox.Show ("Error accessing resources!");
                return;
            }
/*
            try
            {

                dgvAnalOption.DataSource = null;

                m_AnalOptionInfo = new SortableBindingList<OptionInfo> ();
                dgvAnalOption.AutoGenerateColumns = false;
                dgvAnalOption.DataSource = m_AnalOptionInfo;

                if (dgvAnalStock.SelectedRows.Count < 1)
                {
                    MessageBox.Show ("Select one or more equities for option chain analysis.");
                    return;
                }

                m_SelectedStocks = new List<StockAnal> ();
                foreach (DataGridViewRow row in dgvAnalStock.SelectedRows)
                {
                    m_SelectedStocks.Add ((StockAnal) row.DataBoundItem);
                }

                axTws.reqMarketDataType (ckbAnalUseFrozenData.Checked ? 2 : 1);

                for (int stock_no = 0; stock_no < m_SelectedStocks.Count; stock_no++)
                {
                    try
                    {
                        string right = "";
                        if (ckbAnalOptionCalls.Checked && !ckbAnalOptionPuts.Checked)
                        {
                            right = "C";
                        }
                        if (!ckbAnalOptionCalls.Checked && ckbAnalOptionPuts.Checked)
                        {
                            right = "P";
                        }
                        if (!ckbAnalOptionCalls.Checked && !ckbAnalOptionPuts.Checked)
                        {
                            MessageBox.Show ("Must pick either puts or calls.");
                            return;
                        }
                        List<OptionInfo> optionchain = await (FetchOptionChain (stock_no, right));

                        double LastPrice = (double) m_SelectedStocks[stock_no].LastDailyTrade;

                        /* sort and keep the 100 options closest to the strike
                         * --------------------------------------------------- */
/*
                        optionchain.Sort (Comparer<OptionInfo>.Create ((o1, o2) =>
                            {
                                double diff = Math.Abs (o1.Strike - LastPrice) - Math.Abs (o2.Strike - LastPrice);
                                if (diff < 0)
                                {
                                    return -1;
                                }
                                else if (diff > 0)
                                {
                                    return 1;
                                }
                                else
                                {
                                    return 0;
                                }
                            }));

                        m_SelectedStocks[stock_no].OptionChain = optionchain.Take (90).ToList ();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show (string.Format ("Exception encountered retrieving option chain for {0}. {1}", m_SelectedStocks[stock_no].Ticker, ex.Message));
                        return;
                    }

                    try
                    {
                        await (FetchOptionChainMarketData (stock_no));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show (string.Format ("Exception encountered retrieving market data for option chain for {0}. {1}", m_SelectedStocks[stock_no].Ticker, ex.Message));
                        return;
                    }

                    /* Update the ProbITM field
                     * ------------------------ */
/*
                    foreach (var s in m_SelectedStocks[stock_no].OptionChain)
                    {
                        s.ComputeProbITM ();
                        s.ThetaVegaRatio = s.Theta / s.Vega;
                        s.ComputeROCandBPR ();
                    }

                    LoadAdditionalAnalOption (m_SelectedStocks[stock_no].OptionChain);
                }
            }
            finally
            {
                pbAnalOption.Image = null;
            }*/
        }

        /***********************************************************************
         * 
         * LoadAdditionalAnalOption
         * 
         * ********************************************************************/

        private void LoadAdditionalAnalOption (List<OptionInfo> optionchain)
        {
            List<OptionInfo> optlist = new List<OptionInfo> ();

            double StrangleThreshold = Utils.PercentageParse (tbITMThreshold.Text);
            int OpenInterestThreshold = int.Parse (tbOIThreshold.Text);
            double MinPremium = Utils.CurrencyParse (tbMinPremium.Text);
            double ROCThreshold = Utils.PercentageParse (tbROCThreshold.Text);

            string ticker = "";
            if (optionchain.Count > 0)
            {
                ticker = optionchain[0].Symbol;
            }

            //foreach (var stock in m_SelectedStocks)
            {
                if (ckbStrangleThreshold.Checked)
                {
                    List<OptionInfo> c = FilterForStrangle (optionchain, StrangleThreshold);

                    /* Filter Open Interest
                     * -------------------- */

                    if (ckbOIThreshold.Checked)
                    {
                        c = FilterOpenInterest (c, OpenInterestThreshold);
                    }

                    /* Filter Min Premium
                     * ------------------ */

                    if (ckbMinPremium.Checked)
                    {
                        double? premium = 0;
                        foreach (var opt in c)
                        {
                            premium += opt.ComputePremium ();
                        }
                        if (premium == null)
                        {
                            m_Log.Log (ErrorLevel.logINF, string.Format ("Unable to compute premium for {0}.", ticker));
                            c = null;
                        }
                        else
                        {
                            if ((double) premium < MinPremium)
                            {
                                m_Log.Log (ErrorLevel.logINF, string.Format ("Premium too low for {0}. Premium={1:F3}", ticker, (double) premium));
                                c = null;
                            }
                        }
                    }

                    /* Filter ROC Threshold
                     * -------------------- */

                    if (ckbROCThreshold.Checked)
                    {
                        c = FilterROCThreshold (c, ROCThreshold);
                    }

                    if (c != null)
                    {
                        optlist.AddRange (c);
                    }
                }
                else
                {
                    if (ckbOIThreshold.Checked)
                    {
                        optlist.AddRange (FilterOpenInterest (optionchain, OpenInterestThreshold));
                    }
                    else
                    {
                        optlist.AddRange (optionchain);
                    }
                }

            }

            foreach (var opt in optlist)
            {
                m_AnalOptionInfo.Add (opt);
            }
        }

        /******************************************************************************************
         * 
         * Filter for open interest
         * 
         * ***************************************************************************************/

        private List<OptionInfo> FilterOpenInterest (List<OptionInfo> optlist, int OpenInterestThreshold)
        {
            if (optlist == null)
            {
                return null;
            }

            List<OptionInfo> newlist = new List<OptionInfo> ();
            foreach (var opt in optlist)
            {
                if (opt.OpenInterest >= OpenInterestThreshold)
                {
                    newlist.Add (opt);
                }
                else
                {
                    m_Log.Log (ErrorLevel.logINF, string.Format ("INSUFFICIENT OPEN INTEREST. Eliminating {0} {1} strike {2:N2}", opt.Symbol, opt.Right, opt.Strike));
                }
            }
            return newlist;
        }

        /******************************************************************************************
         * 
         * Filter for ROC Threshold
         * 
         * ***************************************************************************************/

        private List<OptionInfo> FilterROCThreshold (List<OptionInfo> optlist, double ROCInterest)
        {
            if (optlist == null)
            {
                return null;
            }

            List<OptionInfo> newlist = new List<OptionInfo> ();
            foreach (var opt in optlist)
            {
                string Roc = opt.ROC.Replace ('%', ' ');
                double roc;
                if (double.TryParse (Roc, out roc))
                {
                    if (ROCInterest <= roc / 100.0)
                    {
                        newlist.Add (opt);
                    }
                    else
                    {
                        m_Log.Log (ErrorLevel.logINF, string.Format ("INSUFFICIENT ROC on {0} {1} strike {2:F2}. ROC = {3:F2}", opt.Symbol, opt.Right, opt.Strike, ROCInterest));
                    }
                }
            }
            return newlist;
        }

        /*********************************************************************************************
         * 
         * Filter for Strangle
         * 
         * skip any option with OI = 0 
         * 
         * ******************************************************************************************/

        private List<OptionInfo> FilterForStrangle (List<OptionInfo> optlist, double StrangleThreshold)
        {
            if (optlist == null)
            {
                return null;
            }

            OptionInfo BestPut = null;
            OptionInfo BestCall = null;
            foreach (var opt in optlist)
            {
                if ((opt.OpenInterest != null) && (((int) opt.OpenInterest) == 0))
                {
                    m_Log.Log (ErrorLevel.logINF, string.Format ("Since OI is zero, skipping {0} {1} {2}", opt.Symbol, opt.Right, opt.Strike.ToString ()));
                    continue;
                }

                if (opt.Right == "C")
                {
                    if (opt.ProbITM < StrangleThreshold)
                    {
                        if (BestCall == null)
                        {
                            BestCall = opt;
                        }
                        else
                        {
                            if (BestCall.ProbITM < opt.ProbITM)
                            {
                                BestCall = opt;
                            }
                        }
                    }
                }
                else
                {
                    if (opt.ProbITM < StrangleThreshold)
                    {
                        if (BestPut == null)
                        {
                            BestPut = opt;
                        }
                        else
                        {
                            if (BestPut.ProbITM < opt.ProbITM)
                            {
                                BestPut = opt;
                            }
                        }
                    }
                }
            }

            List<OptionInfo> l = new List<OptionInfo> ();
            if (BestCall != null)
            {
                l.Add (BestCall);
            }
            if (BestPut != null)
            {
                l.Add (BestPut);
            }
            return l;
        }

        /*****************************************************************************
         * 
         * Fetch the Option Chain
         * 
         * **************************************************************************/

        public Task<List<OptionInfo>> FetchOptionChain (int reqid, string right)
        {
            /*            List<OptionInfo> optionchain = new List<OptionInfo> ();

                        StockAnal stock = m_SelectedStocks[reqid];

                        var tcs = new TaskCompletionSource<List<OptionInfo>> ();
                        TWSLib.IContract contract = axTws.createContract ();

                        contract.symbol = Utils.Massage (stock.Ticker);

                        contract.secType = "OPT";
                        contract.expiry = (DateTime.Parse (cbAnalExpires.Text)).ToString ("yyyyMMdd");
                        contract.strike = 0.0;
                        contract.right = right;
                        contract.multiplier = "100";  // skips around the mini's in SPY, for example
            //            contract.multiplier = "";
                        contract.exchange = stock.Exchange;
                        contract.primaryExchange = "";
                        contract.currency = "USD";
                        contract.localSymbol = "";
                        contract.includeExpired = 0;

                        m_Log.Log (ErrorLevel.logDEB, string.Format ("Getting option for {0}", stock.Ticker));

                        var errhandler = default (AxTWSLib._DTwsEvents_errMsgEventHandler);
                        var datahandler = default (AxTWSLib._DTwsEvents_contractDetailsExEventHandler);
                        var endhandler = default (AxTWSLib._DTwsEvents_contractDetailsEndEventHandler);

                        errhandler = new AxTWSLib._DTwsEvents_errMsgEventHandler ((s, e) =>
                        {
                            tcs.TrySetException (new Exception (e.errorMsg));

                            axTws.contractDetailsEx -= datahandler;
                            axTws.errMsg -= errhandler;
                            axTws.contractDetailsEnd -= endhandler;
                        });

                        endhandler = new AxTWSLib._DTwsEvents_contractDetailsEndEventHandler ((s, e) =>
                        {
                            if (e.reqId == (Constants.ANALYZE_OPTIONS_DATA | reqid))
                            {
                                try
                                {
                                    tcs.TrySetResult (optionchain);
                                }
                                finally
                                {
                                    axTws.contractDetailsEx -= datahandler;
                                    axTws.errMsg -= errhandler;
                                    axTws.contractDetailsEnd -= endhandler;
                                }
                            }
                        });

                        datahandler = new AxTWSLib._DTwsEvents_contractDetailsExEventHandler ((s, e) =>
                        {
                            TWSLib.IContractDetails c = e.contractDetails;
                            TWSLib.IContract d = (TWSLib.IContract) c.summary;
                            if ((Constants.ANALYZE_OPTIONS_DATA | reqid) == e.reqId)
                            {
                                m_Log.Log (ErrorLevel.logINF, string.Format ("New opt sym {0} localsym {1} mult: {2}, strike {3}", d.symbol, d.localSymbol, d.multiplier, d.strike));
                                optionchain.Add (new OptionInfo (d.currency, d.exchange, d.expiry, d.strike, d.symbol, d.localSymbol, d.multiplier, d.secId, d.secIdType, d.secType, d.right, d.tradingClass));
                            }
                        });

                        axTws.contractDetailsEx += datahandler;
                        axTws.errMsg += errhandler;
                        axTws.contractDetailsEnd += endhandler;

                        axTws.reqContractDetailsEx (Constants.ANALYZE_OPTIONS_DATA | reqid, contract);
                        return tcs.Task;*/
            return null;
        }

        /********************************************************
         * 
         * short-hand for displaying stock, option for a particular request id
         * 
         * *****************************************************/

        private string OptIdDisplay (int id)
        {
            id &= 0xFFFF;
            int stock_no = id >> 8;
            int opt_no = id & 0xFF;
            StockAnal s = m_SelectedStocks[stock_no];
            OptionInfo opt = s.OptionChain[opt_no];
            return string.Format ("stock: {0} opt strike: {1} {2}", s.Ticker, opt.Strike, opt.Right == "C" ? "Call" : "Put");
        }

        /*****************************************************************************
         * 
         * Fetch the Market Data for the given Option Chain
         * 
         * **************************************************************************/

        public Task<int> FetchOptionChainMarketData (int stock_no)
        {
            /*            timerOptChain.AutoReset = false;
                        timerOptChain.Interval = 5000;

                        List<OptionInfo> optionchain = m_SelectedStocks[stock_no].OptionChain;

                        var tcs = new TaskCompletionSource<int> ();

                        var errhandler = default (AxTWSLib._DTwsEvents_errMsgEventHandler);
                        var endhandler = default (AxTWSLib._DTwsEvents_tickSnapshotEndEventHandler);
                        var pricehandler = default (AxTWSLib._DTwsEvents_tickPriceEventHandler);
                        var optioncomputehandler = default (AxTWSLib._DTwsEvents_tickOptionComputationEventHandler);
                        var generichandler = default (AxTWSLib._DTwsEvents_tickGenericEventHandler);
                        var sizehander = default (AxTWSLib._DTwsEvents_tickSizeEventHandler);
                        var timerhandler = default (System.Timers.ElapsedEventHandler);

                        errhandler = new AxTWSLib._DTwsEvents_errMsgEventHandler ((s, e) =>
                        {
                            if (e.id != -1)
                            {
                                m_Log.Log (ErrorLevel.logERR, string.Format ("FetchOptionChainMarketData: error {0} {1}", OptIdDisplay (e.id), e.errorMsg));

                                if ((e.id & 0xFFFF0000) != Constants.ANALYZE_OPTIONS_MARKET_DATA)
                                {
                                    return;
                                }

                                axTws.errMsg -= errhandler;
                                axTws.tickGeneric -= generichandler;
                                axTws.tickPrice -= pricehandler;
                                axTws.tickOptionComputation -= optioncomputehandler;
                                axTws.tickSnapshotEnd -= endhandler;
                                axTws.tickSize -= sizehander;
                                timerOptChain.Elapsed -= timerhandler;

                                for (int option_no = 0; option_no < optionchain.Count; option_no++)
                                {
                                    axTws.cancelMktData (Constants.ANALYZE_OPTIONS_MARKET_DATA | ((stock_no << 8) + option_no));
                                }

                                tcs.TrySetException (new Exception (e.errorMsg));
                            }
                            else
                            {
                                m_Log.Log (ErrorLevel.logERR, string.Format ("FetchOptionChainMarketData: error {0:x} {1}", e.id, e.errorMsg));
                            }
                        });

                        pricehandler = new AxTWSLib._DTwsEvents_tickPriceEventHandler ((s, e) =>
                        {
                            if ((e.id & 0xFFFF0000) != Constants.ANALYZE_OPTIONS_MARKET_DATA)
                            {
                                return;
                            }

                            m_Log.Log (ErrorLevel.logERR, string.Format ("FetchOptionChainMarketData: axTws_tickPrice for {0} tickType:{1} {2} value: {3}", OptIdDisplay (e.id), e.tickType, TickType.Display (e.tickType), e.price));

                            e.id &= 0xFFFF;
                            int s_no = e.id >> 8;
                            int opt_no = e.id & 0xFF;
                            StockAnal st = m_SelectedStocks[s_no];
                            OptionInfo opt = st.OptionChain[opt_no];

                            switch (e.tickType)
                            {
                                case TickType.CLOSE:
                                    opt.Last = e.price;
                                    break;

                                case TickType.BID:
                                    opt.Bid = e.price;
                                    break;

                                case TickType.ASK:
                                    opt.Ask = e.price;
                                    break;

                                default:
                                    break;
                            }
                        });

                        sizehander = new AxTWSLib._DTwsEvents_tickSizeEventHandler ((s, e) =>
                        {
                            m_Log.Log (ErrorLevel.logINF, string.Format ("FetchOptionChainMarketData: axTws_tickSize for {0} tickType: {1} {2} value: {3}", OptIdDisplay (e.id), e.tickType, TickType.Display (e.tickType), e.size));
                            switch (e.tickType)
                            {
                                case TickType.OPTION_PUT_OPEN_INTEREST:
                                    {
                                        e.id &= 0xFFFF;
                                        int s_no = e.id >> 8;
                                        int opt_no = e.id & 0xFF;
                                        StockAnal st = m_SelectedStocks[s_no];
                                        OptionInfo option = st.OptionChain[opt_no];
                                        if (option.Right == "P")
                                        {
                                            option.OpenInterest = e.size;
                                        }
                                    }
                                    break;
                                case TickType.OPTION_CALL_OPEN_INTEREST:
                                    {
                                        e.id &= 0xFFFF;
                                        int s_no = e.id >> 8;
                                        int opt_no = e.id & 0xFF;
                                        StockAnal st = m_SelectedStocks[s_no];
                                        OptionInfo option = st.OptionChain[opt_no];
                                        if (option.Right == "C")
                                        {
                                            option.OpenInterest = e.size;
                                        }
                                    }
                                    break;

                                default:
                                    break;
                            }
                        });

                        generichandler = new AxTWSLib._DTwsEvents_tickGenericEventHandler ((s, e) =>
                        {
                            m_Log.Log (ErrorLevel.logINF, string.Format ("FetchOptionChainMarketData: axTws_tickGeneric for {0} tickType: {1} {2} value: {3}", OptIdDisplay (e.id), e.tickType, TickType.Display (e.tickType), e.value));
                        });

                        optioncomputehandler = new AxTWSLib._DTwsEvents_tickOptionComputationEventHandler ((s, e) =>
                        {
                            m_Log.Log (ErrorLevel.logINF, string.Format ("FetchOptionChainMarketData: axTws_tickOptionComputation for {0} ticktype: {1} {2} value: {3} optPrice {3:F5} undPrice {4:F5}", OptIdDisplay (e.id), e.tickType, TickType.Display (e.tickType), e.optPrice, e.undPrice));
                            OptionComputeHandler (e);
                        });

                        endhandler = new AxTWSLib._DTwsEvents_tickSnapshotEndEventHandler ((s, e) =>
                        {
                            m_Log.Log (ErrorLevel.logINF, string.Format ("FetchOptionChainMarketData: axTws_tickSnapshotEnd for {0}", OptIdDisplay (e.reqId)));

                            axTws.errMsg -= errhandler;
                            axTws.tickGeneric -= generichandler;
                            axTws.tickPrice -= pricehandler;
                            axTws.tickOptionComputation -= optioncomputehandler;
                            axTws.tickSnapshotEnd -= endhandler;
                            axTws.tickSize -= sizehander;
                            timerOptChain.Elapsed -= timerhandler;

                            for (int option_no = 0; option_no < optionchain.Count; option_no++)
                            {
                                axTws.cancelMktData (Constants.ANALYZE_OPTIONS_MARKET_DATA | ((stock_no << 8) + option_no));
                            }

                            tcs.TrySetResult (0);
                        });

                        timerhandler = new System.Timers.ElapsedEventHandler ((s, e) =>
                        {
                            m_Log.Log (ErrorLevel.logINF, string.Format ("FetchOptionChainMarketData: timer.ElapsedEventHandler"));

                            axTws.errMsg -= errhandler;
                            axTws.tickGeneric -= generichandler;
                            axTws.tickPrice -= pricehandler;
                            axTws.tickOptionComputation -= optioncomputehandler;
                            axTws.tickSnapshotEnd -= endhandler;
                            axTws.tickSize -= sizehander;
                            timerOptChain.Elapsed -= timerhandler;

                            for (int option_no = 0; option_no < optionchain.Count; option_no++)
                            {
                                axTws.cancelMktData (Constants.ANALYZE_OPTIONS_MARKET_DATA | ((stock_no << 8) + option_no));
                            }

                            tcs.TrySetResult (0);
                        });

                        axTws.errMsg += errhandler;
                        axTws.tickGeneric += generichandler;
                        axTws.tickPrice += pricehandler;
                        axTws.tickOptionComputation += optioncomputehandler;
                        axTws.tickSnapshotEnd += endhandler;
                        timerOptChain.Elapsed += timerhandler;
                        axTws.tickSize += sizehander;

                        timerOptChain.Start ();

                        //for (int option_no = 0; option_no < 10; option_no++)
                        for (int option_no = 0; option_no < optionchain.Count; option_no++)
                        {
                            OptionInfo oi = optionchain[option_no];

                            IContract contract = axTws.createContract ();

                            contract.symbol = "";
                            contract.secType = "OPT";
                            contract.exchange = "SMART";
                            contract.localSymbol = oi.LocalSymbol;

                            int x = Constants.ANALYZE_OPTIONS_MARKET_DATA | ((stock_no << 8) + option_no);
                            //axTws.reqMktDataEx (Constants.ANALYZE_OPTIONS_MARKET_DATA | (stock_no << 8 + option_no), contract, "", 1);
                            axTws.reqMktDataEx (Constants.ANALYZE_OPTIONS_MARKET_DATA | ((stock_no << 8) + option_no), contract, "100, 101, 104, 106", 0, null);
                        }
                        return tcs.Task;*/
            return null;
        }

 /*       private void OptionComputeHandler (AxTWSLib._DTwsEvents_tickOptionComputationEvent e)
        {
            if ((e.id & 0xFFFF0000) != Constants.ANALYZE_OPTIONS_MARKET_DATA)
            {
                return;
            }

            e.id &= 0xFFFF;
            int s_no = e.id >> 8;
            int opt_no = e.id & 0xFF;
            StockAnal st = m_SelectedStocks[s_no];
            OptionInfo option = st.OptionChain[opt_no];

            double? price = null;
            //     1.79769e+308;
            // 1.79769313486232E+308
            m_Log.Log (ErrorLevel.logINF, string.Format ("OptionComputeHandler axTws_tickOptionComputation for {0} ticktype: {1} {2} value: {3} optPrice {3:F5} undPrice {4:F5}", OptIdDisplay (e.id), e.tickType, TickType.Display (e.tickType), e.optPrice, e.undPrice));
            if (e.optPrice < double.MaxValue)
            {
                price = e.optPrice;
            }
            else
            {
                m_Log.Log (ErrorLevel.logERR, "OptionComputeHandler axTws_tickOptionComputation price set to nil");
            }
            if (e.delta < double.MaxValue)
            {
                option.Delta = e.delta;
            }
            else
            {
                m_Log.Log (ErrorLevel.logERR, string.Format ("OptionComputeHandler axTws_tickOptionComputation for {0} Bad delta. {1}.", OptIdDisplay (e.id), e.delta.ToString ()));
            }
            if (e.gamma < double.MaxValue)
            {
                option.Gamma = e.gamma;
            }
            if (e.theta < double.MaxValue)
            {
                option.Theta = e.theta;
            }
            if (e.vega < double.MaxValue)
            {
                option.Vega = e.vega;
            }
            switch (e.tickType)
            {
                case TickType.BID_OPTION:
                    option.Bid = price;
                    break;
                case TickType.ASK_OPTION:
                    option.Ask = price;
                    break;
                case TickType.LAST_OPTION:
                    option.Last = price;
                    break;
                case TickType.MODEL_OPTION:
                    option.UndPrice = e.undPrice;
                    option.Price = e.optPrice;
                    m_Log.Log (ErrorLevel.logINF, "OptionComputeHandler axTws_tickOptionComputation case 13");
                    break;

                default:
                    m_Log.Log (ErrorLevel.logSEV, "OptionComputerHandler. Unknown tick type. Investigate.");
                    break;
            }
            if (e.undPrice < double.MaxValue)
            {
                option.UndPrice = e.undPrice;
            }
            if (e.impliedVol < double.MaxValue)
            {
                option.ImpliedVolatility = e.impliedVol;
            }
        }
*/
        private void dgvAnalOption_CellFormatting (object sender, DataGridViewCellFormattingEventArgs e)
        {
            //if (e.ColumnIndex == 6)
            //{
            //    if (e.Value != null)
            //    {
            //        e.Value = ((double) e.Value).ToString ("P3");
            //    }
            //    else
            //    {
            //        e.Value = "nil";
            //    }
            //}
        }

        /***************************************************************
         * 
         * Validate Expiry Date 
         * 
         * ************************************************************/

        private void cbAnalExpires_Validating (object sender, CancelEventArgs e)
        {
            DateTime dt;

            if (!DateTime.TryParse (cbAnalExpires.Text, out dt))
            {
                MessageBox.Show ("Invalid date specified.");
                e.Cancel = true;
            }
        }

        /****************************************************************
         * 
         * Save listed options
         * 
         * **************************************************************/

        private void btnOptionSave_Click (object sender, EventArgs ev)
        {
            XmlSerializer x = new XmlSerializer (typeof (SortableBindingList<OptionInfo>));

            StringBuilder Filename = new StringBuilder (Properties.Settings.Default.OptionFolder);
            Filename.Append ("Option Strangles");
            Filename.Append (DateTime.Now.ToString ().Replace (':', '_'));
            Filename.Append (".xml");

            using (TextWriter writer = new StreamWriter (Filename.ToString ()))
            {
                try
                {
                    x.Serialize (writer, m_AnalOptionInfo);
                }
                catch (Exception e)
                {
                    Exception ex = e.InnerException;
                    MessageBox.Show (string.Format ("Message: {0}\r\nException Type: {1}\r\nSource: {2}\r\nStackTrace: {3}\r\nTargetSite: {4}", ex.Message, ex.GetType ().FullName, ex.Source, ex.StackTrace, ex.TargetSite));
                }
                writer.Close ();
            }
        }

        private void btnOptionLoad_Click (object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog1.Filter = "XML Files (.xml)|*.xml|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.InitialDirectory = Properties.Settings.Default.OptionFolder;

            if (openFileDialog1.ShowDialog () == DialogResult.OK)
            {
                dgvAnalOption.DataSource = null;

                using (StreamReader rdr = new StreamReader (openFileDialog1.FileName))
                {
                    XmlSerializer x = new XmlSerializer (typeof (SortableBindingList<OptionInfo>));

                    m_AnalOptionInfo = (SortableBindingList<OptionInfo>) x.Deserialize (rdr);
                    dgvAnalOption.AutoGenerateColumns = false;
                    dgvAnalOption.DataSource = m_AnalOptionInfo;
                }
            }
        }

        private void tbAnalMinPrice_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.AnalMinPrice = int.Parse (tbAnalMinPrice.Text);
        }

        private void tbAnalMinVolume_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.AnalMinVolume = int.Parse (tbAnalMinVolume.Text);
        }

        private void tbMinIVPercentile_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.AnalMinIVPercentile = double.Parse (tbMinIVPercentile.Text);
        }

        private void dtpAnalEarnings_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.AnalNixEarnings = dtpAnalNixEarnings.Value;
        }

        private void rbTWS_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.ConnectTWS = rbTWS.Checked;
        }

        private void tbClientId_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.ClientID = int.Parse (tbClientId.Text);
         }

        private void lbxPortfolio_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.PortfolioIndex = lbxPortfolio.SelectedIndex;
            Properties.Settings.Default.Save ();
        }
 
        /***********************************************
         * 
         * Show/Hide Trends form
         * 
         * *********************************************/

        private void cbShowTrends_CheckedChanged (object sender, EventArgs e)
        {
            if (m_frmTrends == null)
            {
                m_frmTrends = new frmTrends ();
                m_frmTrends.FormClosed += (o, ex) => 
                {
                    m_frmTrends = null;
                    cbShowTrends.Checked = false;
                };
            }
            if (cbShowTrends.Checked)
            {
                m_frmTrends.Show ();
                dgvAnalStock_SelectionChanged (null, null);
            }
            else
            {
                m_frmTrends.Hide ();
            }
        }

        /************************************************
         * 
         * dgvAnalStock selection has changed
         * 
         * *********************************************/

        private async void dgvAnalStock_SelectionChanged (object sender, EventArgs e)
        {
            if (dgvAnalStock.SelectedCells.Count > 0)
            {
                StockAnal s = (StockAnal) dgvAnalStock.Rows[dgvAnalStock.SelectedCells[0].RowIndex].DataBoundItem;
                if (s != null)
                {
                    if (m_frmTrends != null)
                    {
                        List<TickerBB> tbb = await ComputeOneYearBollingerBands (s.Ticker);
                        m_frmTrends.Graph (tbb);
                    }
                }
            }
        }

        private void dgvAnalStock_RowEnter (object sender, DataGridViewCellEventArgs e)
        {
            if (m_frmTrends != null)
            {
                m_frmTrends.BringToFront ();
            }
        }

        /*********************************************************
         * 
         * Bollinger Band Portfolio Load
         * 
         * ******************************************************/

        private void btnBBPortfolio_Click (object sender, EventArgs e)
        {
            PortfolioDesc d = (PortfolioDesc) lbxPortfolio.SelectedItem;

            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {
                var q = (from ps in dc.PortfolioStocks
                         join s in dc.Stocks on ps.Ticker equals s.Ticker
                         where ps.PortfolioName == d.PortfolioName
                         select new TickerBB (s.Ticker,
                                               s.Company,
                                               s.IVRank,
                                               s.IVPercentile,
                                               s.PriceChange5Day,
                                               s.PriceChange10Day,
                                               s.PriceChange15Day,
                                               s.PercentBB)
                        );

                m_ViewableTickerBB = new SortableBindingList<TickerBB> (q);
                dgvTickerBB.AutoGenerateColumns = false;
                dgvTickerBB.DataSource = m_ViewableTickerBB;
            }
        }

        /***********************************************************************
         * 
         * TickerBB Column Width has changed
         * 
         * ********************************************************************/

        private void dgvTickerBB_ColumnWidthChanged (object sender, DataGridViewColumnEventArgs e)
        {
            if (!bSettingWidthsSuppressRecording)
            {
                if (BBColumnWidths != null)
                {
                    BBColumnWidths = new System.Collections.Specialized.StringCollection ();

                    foreach (DataGridViewColumn col in dgvTickerBB.Columns)
                    {
                        BBColumnWidths.Add (col.Width.ToString ());
                    }
                }
            }
        }

        /*********************************************************************
         * 
         * Compute the Bollinger Bands for each stock in the shown portfolio
         * 
         * *******************************************************************/

        private async void btnBBCompute_Click (object sender, EventArgs e)
        {
            try
            {
                tbIVMsg.Text = "";
                Assembly _assembly = Assembly.GetExecutingAssembly ();
                Stream _imageStream = _assembly.GetManifestResourceStream ("IBFetchData.Images.spiffygif_30x30.gif");
                pbBB.Image = new Bitmap (_imageStream);

                await ComputeBollingerBands ();

            }
            finally
            {
                pbBB.Image = null;
            }
        }

        private Task ComputeBollingerBands ()
        {
            return Task.Run (() =>
            {

                DateTime StartData = m_BollingerBandDate;
                DateTime EndData = m_BollingerBandDate;

                /* May not have today's prices yet, so grab at least 25 days of data
                 * ----------------------------------------------------------------- */

                for (int i = 0; i < 25; i++ )  
                {
                    StartData = StartData.PreviousBusinessDay ();
                }

                foreach (DataGridViewRow row in dgvTickerBB.Rows)
                {
                    TickerBB tiv = (TickerBB) row.DataBoundItem;

                    using (dbOptionsDataContext dc = new dbOptionsDataContext ())
                    {
                        var ph = (from ps in dc.PriceHistories
                                 where tiv.ticker == ps.Ticker && StartData <= ps.PriceDate && EndData >= ps.PriceDate && ps.ClosingPrice != null
                                 orderby ps.PriceDate
                                 select new {ps.Ticker, ps.PriceDate, ps.ClosingPrice, ps.ClosingIV}).ToList();

                        /* Make sure list has unique dates
                         * ------------------------------- */

                        ph = ph.GroupBy (t => t.PriceDate).Select (y => y.Last ()).ToList ();

                        /* Pick only the last 20
                         * --------------------- */

                        int Current = ph.Count - 20;
                        double percentBB = 0.0;

                        /* We should have exactly 20 data points
                         * ------------------------------------- */

                        if (Current >= 0)
                        //while (Current + 19 < ph.Count)
                        {
                            double sum = 0.0;
                            double sumsquared = 0.0;

                            try
                            {
                                for (int i = 0; i < 20; i++)
                                {
                                    double price = (double) ph[Current + i].ClosingPrice;
                                    sum += price;
                                    sumsquared += price * price;
                                }
                            }
                            catch (Exception)
                            {
                                UpdateBBMessage (string.Format ("Unable to compute sma for {0}. Missing price..\r\n", ph[Current].Ticker));

                                //DialogResult dr = MessageBox.Show (string.Format ("Unable to compute sma for {0}. Missing price. Continue?", ph[Current].Ticker), "Oops", MessageBoxButtons.YesNo);
                                //if (dr == System.Windows.Forms.DialogResult.No)
                                //{
                                //    return;
                                //}
                                Current++;
                                continue;
                            }

                            sum /= 20.0;
                            sumsquared /= 20;

                            /* if all the prices are the same
                             * ------------------------------
                             * from roundoff error, sumsquared may be a tiny bit smaller than sum squared */

                            double diff = sumsquared - sum * sum;
                            if (diff < 0)
                            {
                                diff = 0.0;
                            }
                            double sigma = Math.Sqrt (diff);

                            double bw = sigma + sigma;
                            double lbb = sum - bw;
                            double ubb = sum + bw;

                            if (bw != 0)
                            {
                                percentBB = ((double) ph[Current + 19].ClosingPrice - lbb) / (bw * 2.0);
                            }

                            m_Log.Log (ErrorLevel.logINF, string.Format ("BB computation for {0} {1} low: {3:N2} high {4:N2} mid: {5:N2} %BB {2:N2}", tiv.ticker, ph[Current + 19].PriceDate, percentBB, lbb, ubb, sum));

                            //dc.UpdatePercentBB (ph[Current].Ticker, ph[Current].PriceDate, (decimal) percentBB);
                            Current++;
                        }

                        /* The last one is the one viewed in the DataGridView
                         * -------------------------------------------------- */

                        tiv.PercentBB = percentBB;
                        dgvTickerBB.InvalidateCell (row.Cells[bbcolPERCENTBB]);

                        /* Update Stock with the latest % BB
                         * --------------------------------- */


                        var query = (from s in dc.Stocks
                                            where s.Ticker == tiv.ticker
                                            select s).FirstOrDefault ();
                        query.PercentBB = percentBB;
                        dc.SubmitChanges ();
                    }
                }
            });
        }

        /*********************************************************************
         * 
         * Row selection changed on Ticker BB list
         * 
         * ******************************************************************/

        private async void dgvTickerBB_SelectionChanged (object sender, EventArgs e)
        {
            if (dgvTickerBB.SelectedRows.Count >= 1)
            {
                DataGridViewRow r = dgvTickerBB.SelectedRows[0];
                if (r.IsNewRow)
                {
                    return;
                }
                TickerBB tiv = (TickerBB) r.DataBoundItem;
                if (tiv == null)
                {
                    return;
                }

                List<TickerBB> tbb = await ComputeOneYearBollingerBands (tiv.ticker);
                dgvHistoricalBB.AutoGenerateColumns = false;
                dgvHistoricalBB.DataSource = tbb;
                PlotBB (tbb);
           }

        }

        /***********************************************************************
         * 
         * PlotBB - plot Bollinger Bands
         * 
         * ********************************************************************/

        private void PlotBB (List<TickerBB> tbb)
        {
            chart2.ChartAreas[0].AxisX.Title = tbb[0].ticker;
            chart2.ChartAreas[0].AxisX.TitleFont = new Font ("Verdana", 11, FontStyle.Bold);
            chart2.ChartAreas[0].BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            chart2.ChartAreas[0].BorderWidth = 2;

            System.Windows.Forms.DataVisualization.Charting.Series s = chart2.Series[0];

            chart2.DataSource = tbb;
            s.XValueMember = "PriceDate";
            s.YValueMembers = "LowerBB";

            s = chart2.Series[1];
            s.XValueMember = "PriceDate";
            s.YValueMembers = "SMA";

            s = chart2.Series[2];
            s.XValueMember = "PriceDate";
            s.YValueMembers = "UpperBB";

            s = chart2.Series[3];
            s.XValueMember = "PriceDate";
            s.YValueMembers = "Price";

            double min = double.MaxValue;
            double max = double.MinValue;

            foreach (var t in tbb)
            {
                if (min > t.LowerBB)
                {
                    min = t.LowerBB;
                }
                if (min > t.Price)
                {
                    min = t.Price;
                }
                if (max < t.UpperBB)
                {
                    max = t.UpperBB;
                }
                if (max < t.Price)
                {
                    max = t.Price;
                }

            }

            RoundAxis (ref min, ref max);
            
            chart2.ChartAreas[0].AxisY.Maximum = max;
            chart2.ChartAreas[0].AxisY.Minimum = min;
            chart2.ChartAreas[0].AxisX.LabelStyle.Format = "yy-MMM-dd";
            chart2.Series[0].XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Date;
            chart2.ChartAreas[0].AxisY.LabelStyle.Format = "{0:N2}";




            chart2.DataBind ();
        }

        private void RoundAxis (ref double min, ref double max)
        {
            int rnd = 1;
            if (max - min > 10)
            {
                rnd = 2;
            }
            if (max - min > 50)
            {
                rnd = 10;
            }
            if (max - min > 100)
            {
                rnd = 20;
            }

            min = ((int) min) / rnd * rnd;
            max = (((int) max) + rnd) / rnd * rnd;
        }

        /***********************************************************************
         * 
         * Compute the BB for the past year for specified ticker
         * 
         * ********************************************************************/

        private Task<List<TickerBB>> ComputeOneYearBollingerBands (string ticker)
        {
            return Task.Run (() => 
            {
                List<TickerBB> tbb = new List<TickerBB> ();

                DateTime EndData = DateTime.Today;
                DateTime StartData = EndData - new TimeSpan (365 + 19, 0, 0, 0, 0);

                using (dbOptionsDataContext dc = new dbOptionsDataContext ())
                {
                    var ph = (from ps in dc.PriceHistories
                                where ticker == ps.Ticker && StartData <= ps.PriceDate && EndData >= ps.PriceDate && ps.ClosingPrice != null 
                                orderby ps.PriceDate
                                select new { ps.Ticker, ps.PriceDate, ps.ClosingPrice, ps.ClosingIV}).ToList ();

                    /* Make sure list has unique dates
                        * ------------------------------- */

                    ph = ph.GroupBy (t => t.PriceDate).Select (y => y.Last ()).ToList ();

                    int Current = 0;
                    double percentBB = 0.0;

                    while (Current + 19 < ph.Count)
                    {
                        double sum = 0.0;
                        double sumsquared = 0.0;

                        try
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                if (ph[Current + i].ClosingPrice == null)
                                {
                                    m_Log.Log (ErrorLevel.logERR, string.Format ("Missing closing price {0} {1}", ph[Current + i].Ticker, ph[Current + i].PriceDate.ToLongDateString ()));
                                }
                                double price = (double) ph[Current + i].ClosingPrice;
                                sum += price;
                                sumsquared += price * price;
                            }
                        }
                        catch (Exception)
                        {
                            //MessageBox.Show (string.Format ("Unable to compute sma for {0}. Missing price or iv.", ph[Current].Ticker));
                            m_Log.Log (ErrorLevel.logERR, string.Format ("Unable to compute sma for {0}. Missing price around {1} or up to 20 days later", ph[Current].Ticker, ph[Current].PriceDate.ToLongDateString ()));
                            Current++;
                            continue;
                        }

                        sum /= 20.0;
                        sumsquared /= 20;

                        double sigma = Math.Sqrt (sumsquared - sum * sum);

                        double bw = sigma + sigma;
                        double lbb = sum - bw;
                        double ubb = sum + bw;

                        percentBB = ((double) ph[Current + 19].ClosingPrice - lbb) / (bw * 2.0);

                        //m_Log.Log (ErrorLevel.logINF, string.Format ("BB computation for {0} {1} low: {3:N2} high {4:N2} mid: {5:N2} %BB {2:N2}", tiv.ticker, ph[Current + 19].PriceDate, percentBB, lbb, ubb, sum));

                        tbb.Add (new TickerBB (ticker, ph[Current + 19].PriceDate, (double) ph[Current + 19].ClosingPrice, ph[Current + 19].ClosingIV, lbb, ubb, sum, percentBB));
                        Current++;
                    }

                    return tbb;
                }
            });
        }

        /******************************************************************************
         * 
         * Historical BB column widths changed
         * 
         * ***************************************************************************/

        private void dgvHistoricalBB_ColumnWidthChanged (object sender, DataGridViewColumnEventArgs e)
        {
            if (!bSettingWidthsSuppressRecording)
            {
                if (HistoricalBBColumnWidths != null)
                {
                    HistoricalBBColumnWidths = new System.Collections.Specialized.StringCollection ();

                    foreach (DataGridViewColumn col in dgvHistoricalBB.Columns)
                    {
                        HistoricalBBColumnWidths.Add (col.Width.ToString ());
                    }
                }
            }
        }

        private void ckbStrangleThreshold_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.bAnalOptStrangleThreshold = ckbStrangleThreshold.Checked;
            Properties.Settings.Default.Save ();
        }

        private void ckbOIThreshold_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.bAnalOptOIThreshold = ckbOIThreshold.Checked;
            Properties.Settings.Default.Save ();
        }

        private void ckbROCThreshold_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.bAnalOptROCThreshold = ckbROCThreshold.Checked;
            Properties.Settings.Default.Save ();
        }

        private void ckbMinPremium_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.bAnalOptMinPremium = ckbMinPremium.Checked;
            Properties.Settings.Default.Save ();
        }

        private void tbITMThreshold_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.AnalOptITMThreshold = tbITMThreshold.Text;
            Properties.Settings.Default.Save ();
        }

        private void tbOIThreshold_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.AnalOptOIThreshold = int.Parse (tbOIThreshold.Text);
            Properties.Settings.Default.Save ();
        }

        private void tbROCThreshold_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.AnalOptROCThreshold = tbROCThreshold.Text;
            Properties.Settings.Default.Save ();
        }

        private void tbMinPremium_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.AnalOptMinPremium = tbMinPremium.Text;
            Properties.Settings.Default.Save ();
        }

        private void ckbAnalOptionPuts_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.bAnalOptPuts = ckbAnalOptionPuts.Checked;
            Properties.Settings.Default.Save ();
        }

        private void ckbAnalOptionCalls_Leave (object sender, EventArgs e)
        {
            Properties.Settings.Default.bAnalOptCalls = ckbAnalOptionCalls.Checked;
            Properties.Settings.Default.Save ();
        }

        /******************************************************************************
        * 
        * IV Percentile column widths changed
        * 
        * ***************************************************************************/

        private void dgvIVPercentile_ColumnWidthChanged (object sender, DataGridViewColumnEventArgs e)
        {
            if (!bSettingWidthsSuppressRecording)
            {
                if (IVPercentileColumnWidths != null)
                {
                    IVPercentileColumnWidths = new System.Collections.Specialized.StringCollection ();

                    foreach (DataGridViewColumn col in dgvIVPercentile.Columns)
                    {
                        IVPercentileColumnWidths.Add (col.Width.ToString ());
                    }
                }
            }
        }

        /*****************************************************************************
         * 
         * Load Scanned Equities
         * 
         * **************************************************************************/

        private void btnScannedAnal_Click (object sender, EventArgs e)
        {
            PortfolioDesc d = (PortfolioDesc) lbxPortfolio.SelectedItem;

            using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            {
                List<StockAnal> sl = new List<StockAnal> ();

                foreach (var t in m_DisplayedScannerData)
                {
                    var q = (from s in dc.Stocks
                             where s.Ticker == t.symbol
                             select new StockAnal (s.Ticker,
                                                   s.Company,
                                                   s.Sector,
                                                   s.Industry,
                                                   s.LastTrade,
                                                   s.MarketCap,
                                                   s.DailyVolume,
                                                   s.Ex_DividendDate,
                                                   s.NextEarnings,
                                                   s.AnalystsRating,
                                                   s.IVRank,
                                                   s.IVPercentile,
                                                   s.PriceChange5Day,
                                                   s.PriceChange10Day,
                                                   s.PriceChange15Day,
                                                   s.PercentBB,
                                                   s.SecType,
                                                   s.Exchange)
                                ).SingleOrDefault ();

                    if (q != null)
                    {
                        sl.Add (q);
                    }
                }

                m_ViewableAnalStocks = new SortableBindingListView<StockAnal> (sl);
                dgvAnalStock.AutoGenerateColumns = false;
                BindingSource bs = new BindingSource ();
                bs.DataSource = m_ViewableAnalStocks;
                dgvAnalStock.DataSource = bs;
                lbAnalStocksNo.Text = m_ViewableAnalStocks.Count.ToString ();

                bSettingWidthsSuppressRecording = true;
                int i = 0;
                foreach (DataGridViewColumn col in dgvAnalStock.Columns)
                {
                    int col_width;
                    if (i < StockAnalColumnWidths.Count)
                    {
                        if (int.TryParse (StockAnalColumnWidths[i++], out col_width))
                        {
                            col.Width = col_width;
                        }
                    }
                }
                bSettingWidthsSuppressRecording = false;
            }
        }


    }
}
