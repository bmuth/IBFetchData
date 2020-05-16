namespace IBFetchData
{
    partial class frmTrends
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose (bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose ();
            }
            base.Dispose (disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent ()
        {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea3 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend3 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series6 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series7 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series8 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series9 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Title title3 = new System.Windows.Forms.DataVisualization.Charting.Title();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea4 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend4 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series10 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Title title4 = new System.Windows.Forms.DataVisualization.Charting.Title();
            this.chartPrice = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.chartIV = new System.Windows.Forms.DataVisualization.Charting.Chart();
            ((System.ComponentModel.ISupportInitialize)(this.chartPrice)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartIV)).BeginInit();
            this.SuspendLayout();
            // 
            // chartPrice
            // 
            this.chartPrice.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chartPrice.BackColor = System.Drawing.Color.Black;
            chartArea3.AxisX.IsStartedFromZero = false;
            chartArea3.AxisX.LabelStyle.ForeColor = System.Drawing.Color.LightGray;
            chartArea3.AxisX.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            chartArea3.AxisX.MajorGrid.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dot;
            chartArea3.AxisX.MajorTickMark.LineColor = System.Drawing.Color.DimGray;
            chartArea3.AxisX.TitleForeColor = System.Drawing.Color.White;
            chartArea3.AxisY.IsStartedFromZero = false;
            chartArea3.AxisY.LabelStyle.ForeColor = System.Drawing.Color.LightGray;
            chartArea3.AxisY.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            chartArea3.AxisY.MajorGrid.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dot;
            chartArea3.AxisY.MajorTickMark.LineColor = System.Drawing.Color.DimGray;
            chartArea3.BackColor = System.Drawing.Color.Black;
            chartArea3.BorderColor = System.Drawing.Color.DimGray;
            chartArea3.Name = "ChartArea1";
            this.chartPrice.ChartAreas.Add(chartArea3);
            legend3.BackColor = System.Drawing.Color.Black;
            legend3.ForeColor = System.Drawing.Color.LightGray;
            legend3.Name = "Legend1";
            this.chartPrice.Legends.Add(legend3);
            this.chartPrice.Location = new System.Drawing.Point(-1, -1);
            this.chartPrice.Name = "chartPrice";
            series6.ChartArea = "ChartArea1";
            series6.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series6.Legend = "Legend1";
            series6.Name = "Lower BB";
            series7.ChartArea = "ChartArea1";
            series7.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series7.Legend = "Legend1";
            series7.Name = "SMA";
            series8.ChartArea = "ChartArea1";
            series8.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series8.Legend = "Legend1";
            series8.Name = "Upper BB";
            series9.ChartArea = "ChartArea1";
            series9.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series9.Color = System.Drawing.Color.White;
            series9.Legend = "Legend1";
            series9.Name = "Price";
            series9.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Date;
            series9.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            this.chartPrice.Series.Add(series6);
            this.chartPrice.Series.Add(series7);
            this.chartPrice.Series.Add(series8);
            this.chartPrice.Series.Add(series9);
            this.chartPrice.Size = new System.Drawing.Size(587, 176);
            this.chartPrice.TabIndex = 0;
            this.chartPrice.Text = "chartPrice";
            title3.Docking = System.Windows.Forms.DataVisualization.Charting.Docking.Bottom;
            title3.ForeColor = System.Drawing.Color.Gray;
            title3.Name = "Title1";
            this.chartPrice.Titles.Add(title3);
            // 
            // chartIV
            // 
            this.chartIV.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chartIV.BackColor = System.Drawing.Color.Black;
            chartArea4.AxisX.LabelStyle.ForeColor = System.Drawing.Color.DarkGray;
            chartArea4.AxisX.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            chartArea4.AxisX.MajorGrid.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dot;
            chartArea4.AxisX.MajorTickMark.LineColor = System.Drawing.Color.DimGray;
            chartArea4.AxisY.LabelStyle.ForeColor = System.Drawing.Color.DarkGray;
            chartArea4.AxisY.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            chartArea4.AxisY.MajorGrid.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dot;
            chartArea4.AxisY.MajorTickMark.LineColor = System.Drawing.Color.DimGray;
            chartArea4.BackColor = System.Drawing.Color.Black;
            chartArea4.Name = "ChartArea1";
            this.chartIV.ChartAreas.Add(chartArea4);
            legend4.BackColor = System.Drawing.Color.Black;
            legend4.ForeColor = System.Drawing.Color.LightGray;
            legend4.Name = "Legend1";
            this.chartIV.Legends.Add(legend4);
            this.chartIV.Location = new System.Drawing.Point(-1, 181);
            this.chartIV.Name = "chartIV";
            series10.ChartArea = "ChartArea1";
            series10.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series10.Legend = "Legend1";
            series10.Name = "Implied Vol";
            series10.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Date;
            series10.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            this.chartIV.Series.Add(series10);
            this.chartIV.Size = new System.Drawing.Size(587, 173);
            this.chartIV.TabIndex = 1;
            this.chartIV.Text = "chartIV";
            title4.Docking = System.Windows.Forms.DataVisualization.Charting.Docking.Bottom;
            title4.ForeColor = System.Drawing.Color.Gray;
            title4.Name = "Title1";
            this.chartIV.Titles.Add(title4);
            // 
            // frmTrends
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.WindowFrame;
            this.ClientSize = new System.Drawing.Size(587, 354);
            this.Controls.Add(this.chartIV);
            this.Controls.Add(this.chartPrice);
            this.Name = "frmTrends";
            this.Text = "Trends";
            this.Resize += new System.EventHandler(this.frmTrends_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.chartPrice)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartIV)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataVisualization.Charting.Chart chartPrice;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartIV;
    }
}