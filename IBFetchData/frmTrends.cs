using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace IBFetchData
{
    public partial class frmTrends : Form
    {
        public frmTrends ()
        {
            InitializeComponent ();
        }

        public void Graph (List<TickerBB> tbb)
        {
            //DateTime beginning = DateTime.Now - new TimeSpan (365, 0, 0, 0);
            //using (dbOptionsDataContext dc = new dbOptionsDataContext ())
            //{
            //    var s = (from p in dc.PriceHistories
            //             where p.Ticker == ticker && p.ClosingPrice != null && p.ClosingIV != null && p.PriceDate >= beginning
            //             select new
            //             {
            //                 PriceDate = p.PriceDate,
            //                 Price = (double) p.ClosingPrice,
            //                 IV = 100.0 * (double) p.ClosingIV
            //             });

            //    var prices = (from p in s select p.Price).ToList ();
            //    var dates = (from p in s select p.PriceDate).ToList ();
            //    var iv = (from p in s select p.IV).ToList ();

            this.Text = tbb[0].ticker;
            //chartPrice.ChartAreas[0].AxisX.Title = tbb[0].ticker;
            //chartPrice.ChartAreas[0].AxisX.TitleFont = new Font ("Verdana", 11, FontStyle.Bold);
            //chartPrice.ChartAreas[0].BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            //chartPrice.ChartAreas[0].BorderWidth = 2;


            chartPrice.DataSource = tbb;

            System.Windows.Forms.DataVisualization.Charting.Series s = chartPrice.Series[0];
            s.XValueMember = "PriceDate";
            s.YValueMembers = "LowerBB";

            s = chartPrice.Series[1];
            s.XValueMember = "PriceDate";
            s.YValueMembers = "SMA";

            s = chartPrice.Series[2];
            s.XValueMember = "PriceDate";
            s.YValueMembers = "UpperBB";

            s = chartPrice.Series[3];
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

            chartPrice.ChartAreas[0].AxisY.Maximum = max;
            chartPrice.ChartAreas[0].AxisY.Minimum = min;
            chartPrice.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MMM-yy";
            chartPrice.Series[0].XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Date;
            chartPrice.ChartAreas[0].AxisY.LabelStyle.Format = "{0:N2}";
            //chartPrice.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.DarkGray;
            //chartPrice.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.DarkGray;
            chartPrice.DataBind ();

            chartIV.DataSource = tbb;

            System.Windows.Forms.DataVisualization.Charting.Series s2 = chartIV.Series[0];
            s2.XValueMember = "PriceDate";
            s2.YValueMembers = "IVpercentile";
            chartIV.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MMM-yy";
            chartIV.Series[0].XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Date;
            chartIV.ChartAreas[0].AxisY.LabelStyle.Format = "{0:N1}%";

            chartIV.DataBind ();

            //chartPrice.Series["Price"].Points.DataBindXY (dates, prices);
            //chartPrice.Titles["Title1"].Text = string.Format ("{0} price", ticker);
            //chartPrice.Invalidate ();

            //chartIV.Series["Implied Vol"].Points.DataBindXY (dates, iv);
            //chartIV.Titles["Title1"].Text = string.Format ("{0} implied volatility", ticker);
            //chartIV.Invalidate ();
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

        private void frmTrends_Resize (object sender, EventArgs e)
        {
            int ht = this.ClientSize.Height >> 1;
            chartIV.Top = this.ClientRectangle.Top + ht;
            chartPrice.Height = ht;
            chartIV.Height = ht;
        }

    }
}
