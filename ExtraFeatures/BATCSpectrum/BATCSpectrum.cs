using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using static opentuner.signal;
using System.Windows.Forms;
using System.Xml.Linq;
using System.IO;
using System.Drawing.Drawing2D;
using Serilog;

namespace opentuner.ExtraFeatures.BATCSpectrum
{
    public class BATCSpectrum
    {
        public delegate void SignalSelected(int Receiver, uint Freq, uint SymbolRate);
        public event SignalSelected OnSignalSelected;

        private static readonly Object list_lock = new Object();
        static int bandplan_height = 30;

        Bitmap bmp;
        static Bitmap bmp2;
        Graphics tmp;
        Graphics tmp2;

        int[,] rx_blocks = new int[4, 3];
        double start_freq = 10490.5f;
        XElement bandplan;
        Rectangle[] channels;
        IList<XElement> indexedbandplan;
        string InfoText;
        string TX_Text;
        List<string> blocks = new List<string>();
        socket web_socket;
        signal sigs;
        int num_rxs_to_scan = 1;
        private PictureBox _spectrum;
        private int _tuners;
        Timer SpectrumTuneTimer;
        Timer websocketTimer;
        private int _autoTuneMode = 0;
        int connect_retries = 5;
        int connect_retry_count = 0;

        public void updateSignalCallsign(string callsign, double freq, float sr)
        {
            sigs.updateCurrentSignal(callsign, freq, sr);
        }

        public BATCSpectrum(PictureBox Spectrum, int Tuners)
        {
            _spectrum = Spectrum;
            _tuners = Tuners;

            _spectrum.Click += spectrum_Click;
            _spectrum.MouseLeave += spectrum_MouseLeave;
            _spectrum.MouseMove += spectrum_MouseMove;
            _spectrum.SizeChanged += spectrum_SizeChanged;

            RecreateDrawingSurfaces();

            try
            {
                bandplan = XElement.Load(Path.GetDirectoryName(Application.ExecutablePath) + @"\extra\bandplan.xml");
                drawspectrum_bandplan();
                indexedbandplan = bandplan.Elements().ToList();
                foreach (var channel in bandplan.Elements("channel"))
                {
                    if (!blocks.Contains(channel.Element("block").Value))
                        blocks.Add(channel.Element("block").Value);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            web_socket = new socket();
            sigs = new signal(list_lock);
            web_socket.callback += drawspectrum;
            web_socket.ConnectionStatusChanged += Web_socket_ConnectionStatusChanged;
            sigs.debug += debug;
            web_socket.start();
            sigs.set_num_rx_scan(num_rxs_to_scan);
            sigs.set_num_rx(1);
            sigs.set_avoidbeacon(true);

            SpectrumTuneTimer = new Timer { Enabled = false, Interval = 1500 };
            SpectrumTuneTimer.Tick += SpectrumTuneTimer_Tick;

            websocketTimer = new Timer { Interval = 2000, Enabled = true };
            websocketTimer.Tick += websocketTimer_Tick;

            draw_disconnect();
        }

        private bool ModernTheme
        {
            get { return _spectrum != null && _spectrum.BackColor != Color.Black; }
        }

        private int BandplanHeight
        {
            get { return ModernTheme ? Math.Max(14, Math.Min(22, _spectrum.Height / 6)) : bandplan_height; }
        }

        private int PlotBottom
        {
            get { return Math.Max(48, _spectrum.Height - 8); }
        }

        private float VerticalScale
        {
            get { return PlotBottom / 255.0f; }
        }

        private Font SpectrumFont(float legacySize)
        {
            return new Font(ModernTheme ? "Segoe UI" : "Tahoma", ModernTheme ? Math.Max(7.0f, legacySize - 2.0f) : legacySize);
        }

        private Brush SpectrumTextBrush
        {
            get { return new SolidBrush(ModernTheme ? Color.FromArgb(242, 247, 252) : Color.White); }
        }

        private void RecreateDrawingSurfaces()
        {
            if (_spectrum == null || _spectrum.Width < 2 || _spectrum.Height < 2) return;

            try { tmp?.Dispose(); } catch { }
            try { tmp2?.Dispose(); } catch { }
            try { bmp?.Dispose(); } catch { }
            try { bmp2?.Dispose(); } catch { }

            bmp2 = new Bitmap(_spectrum.Width, BandplanHeight);
            bmp = new Bitmap(_spectrum.Width, _spectrum.Height);
            tmp = Graphics.FromImage(bmp);
            tmp2 = Graphics.FromImage(bmp2);
            tmp.SmoothingMode = SmoothingMode.AntiAlias;
            tmp.InterpolationMode = InterpolationMode.HighQualityBicubic;
            tmp.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            tmp2.SmoothingMode = SmoothingMode.AntiAlias;
        }

        private void draw_disconnect()
        {
            if (tmp == null) return;
            tmp.Clear(_spectrum.BackColor);
            using (Font f = SpectrumFont(10))
            {
                tmp.DrawString("FFT Service Disconnected", f, Brushes.IndianRed, new PointF(10, 10));
                if (connect_retry_count < connect_retries)
                    tmp.DrawString("Retrying ...." + connect_retry_count + "/" + connect_retries, f, Brushes.IndianRed, new PointF(10, 30));
            }
            UpdateDrawing();
        }

        private void Web_socket_ConnectionStatusChanged(object sender, bool connection_status)
        {
            if (connection_status) connect_retry_count = 0;
            else
            {
                _autoTuneMode = 0;
                SpectrumTuneTimer.Enabled = false;
            }
        }

        public void Close()
        {
            websocketTimer?.Stop();
            websocketTimer?.Dispose();
            SpectrumTuneTimer?.Stop();
            SpectrumTuneTimer?.Dispose();
            web_socket?.stop();
        }

        private void websocketTimer_Tick(object sender, EventArgs e)
        {
            if (web_socket == null) return;

            TimeSpan t = DateTime.Now - web_socket.lastdata;
            if (t.Seconds > 2) web_socket.stop();

            if (!web_socket.connected)
            {
                draw_disconnect();
                connect_retry_count += 1;
                if (connect_retry_count < connect_retries)
                {
                    debug("Websocket Not Connected: Retrying " + connect_retry_count + "/" + connect_retries);
                    web_socket.start();
                }
            }
        }

        public void changeTuneMode(int mode)
        {
            _autoTuneMode = mode;
            if (mode == 0) SpectrumTuneTimer?.Stop();
            else SpectrumTuneTimer?.Start();
        }

        private void SpectrumTuneTimer_Tick(object sender, EventArgs e)
        {
            int mode = _autoTuneMode;
            float spectrum_wScale = _spectrum.Width / 922f;
            Tuple<signal.Sig, int> ret = sigs.tune(mode, 30, 0);
            if (ret.Item1.frequency > 0)
            {
                System.Threading.Thread.Sleep(100);
                selectSignal(Convert.ToInt32(ret.Item1.fft_centre * spectrum_wScale), 0);
                sigs.set_tuned(ret.Item1, 0);
                rx_blocks[0, 0] = Convert.ToInt16(ret.Item1.fft_centre);
                rx_blocks[0, 1] = Convert.ToInt16(ret.Item1.fft_stop - ret.Item1.fft_start);
            }
        }

        private void debug(string msg) { Log.Information(msg); }
        private void spectrum_MouseLeave(object sender, EventArgs e) { spectrumTunerHighlight = -1; }

        private void spectrum_SizeChanged(object sender, EventArgs e)
        {
            try
            {
                RecreateDrawingSurfaces();
                if (bandplan != null) drawspectrum_bandplan();
            }
            catch { }
        }

        private void drawspectrum_bandplan()
        {
            if (tmp2 == null || bandplan == null) return;
            tmp2.Clear(Color.Transparent);

            int span = 9;
            int count = 0;
            float spectrum_wScale = _spectrum.Width / 922f;
            List<string> localBlocks = new List<string>();

            foreach (var channel in bandplan.Elements("channel"))
            {
                count++;
                if (!localBlocks.Contains(channel.Element("block").Value))
                    localBlocks.Add(channel.Element("block").Value);
            }

            channels = new Rectangle[count];
            int n = 0;
            int bh = BandplanHeight;

            foreach (var channel in bandplan.Elements("channel"))
            {
                float freq = Convert.ToSingle(channel.Element("x-freq").Value, CultureInfo.InvariantCulture);
                int sr = Convert.ToInt32(channel.Element("sr").Value, CultureInfo.InvariantCulture);
                int pos = Convert.ToInt16((922.0 / span) * (freq - start_freq));
                int w = Convert.ToInt32(sr / (span * 1000.0) * 922 * 1.35f * spectrum_wScale);
                int split = Math.Max(2, bh / Math.Max(1, localBlocks.Count));
                int offset = 0;
                int b = localBlocks.Count;
                foreach (string blk in localBlocks)
                {
                    if (channel.Element("block").Value == blk) offset = b * split;
                    b--;
                }
                channels[n++] = new Rectangle(Convert.ToInt32(pos * spectrum_wScale) - (w / 2), offset - (split / 2) - 2, w, Math.Max(1, split - 2));
            }

            using (SolidBrush brush = new SolidBrush(ModernTheme ? Color.FromArgb(55, 92, 126, 160) : Color.FromArgb(180, 250, 250, 255)))
            {
                for (int i = 0; i < count; i++) tmp2.FillRectangle(brush, channels[i]);
            }
        }

        private void drawspectrum_signals(List<signal.Sig> signals)
        {
            float spectrum_wScale = _spectrum.Width / 922f;
            float vScale = VerticalScale;

            lock (list_lock)
            {
                using (Font f = SpectrumFont(10))
                using (Brush textBrush = SpectrumTextBrush)
                {
                    foreach (signal.Sig s in signals)
                    {
                        float y = (255 - Convert.ToSingle(s.fft_strength + 50)) * vScale;
                        y = Math.Max(2, Math.Min(PlotBottom - 34, y));
                        string label = s.callsign + "\n" + s.frequency.ToString("#.00") + "\n" + (s.sr * 1000).ToString("#Ks");
                        tmp.DrawString(label, f, textBrush, new PointF(Convert.ToSingle((s.fft_centre * spectrum_wScale) - (ModernTheme ? 18 : 25)), y));
                    }
                }
            }
            UpdateDrawing();
        }

        private void UpdateDrawing()
        {
            try
            {
                _spectrum.Parent?.Invoke(new MethodInvoker(delegate { _spectrum.Image = bmp; _spectrum.Update(); }));
            }
            catch { }
        }

        private void drawspectrum(UInt16[] fft_data)
        {
            if (tmp == null || fft_data == null || fft_data.Length < 4) return;

            tmp.Clear(_spectrum.BackColor);

            int bh = BandplanHeight;
            int plotBottom = PlotBottom;
            int spectrum_h = Math.Max(20, plotBottom - bh);
            float spectrum_w = _spectrum.Width;
            float spectrum_wScale = spectrum_w / 922f;
            float vScale = VerticalScale;

            PointF[] points = new PointF[fft_data.Length - 2];
            for (int i = 1; i < fft_data.Length - 3; i++)
            {
                float sourceY = 255 - fft_data[i] / 255.0f;
                points[i] = new PointF(i * spectrum_wScale, sourceY * vScale);
            }
            points[0] = new PointF(0, plotBottom);
            points[points.Length - 1] = new PointF(spectrum_w, plotBottom);

            if (spectrumTunerHighlight > -1)
            {
                int hy = spectrumTunerHighlight * (spectrum_h / _tuners);
                using (SolidBrush hb = new SolidBrush(ModernTheme ? Color.FromArgb(22, 28, 139, 253) : Color.FromArgb(50, Color.Blue)))
                    tmp.FillRectangle(hb, new RectangleF(0, hy, spectrum_w, spectrum_h / _tuners));
            }

            using (LinearGradientBrush fill = new LinearGradientBrush(
                new Point(0, 0), new Point(0, plotBottom),
                ModernTheme ? Color.FromArgb(205, 84, 163, 255) : Color.FromArgb(255, 255, 99, 132),
                ModernTheme ? Color.FromArgb(105, 28, 139, 253) : Color.FromArgb(255, 54, 162, 235)))
            {
                tmp.FillPolygon(fill, points);
            }

            if (bmp2 != null) tmp.DrawImage(bmp2, 0, Math.Max(0, plotBottom - bh));

            for (int tuner = 0; tuner < _tuners; tuner++)
            {
                int y = tuner * (spectrum_h / _tuners);
                if (rx_blocks[tuner, 0] > 0)
                {
                    using (SolidBrush sb = new SolidBrush(ModernTheme ? Color.FromArgb(65, 242, 247, 252) : Color.FromArgb(128, Color.Gray)))
                    {
                        tmp.FillRectangle(sb, new RectangleF(
                            rx_blocks[tuner, 0] * spectrum_wScale - ((rx_blocks[tuner, 1] * spectrum_wScale) / 2),
                            y,
                            rx_blocks[tuner, 1] * spectrum_wScale,
                            spectrum_h / _tuners));
                    }
                }
            }

            using (Font infoFont = SpectrumFont(15))
            using (Brush textBrush = SpectrumTextBrush)
            {
                if (!string.IsNullOrWhiteSpace(InfoText)) tmp.DrawString(InfoText, infoFont, textBrush, new PointF(8, 5));
                if (!string.IsNullOrWhiteSpace(TX_Text)) tmp.DrawString(TX_Text, infoFont, Brushes.IndianRed, new PointF(60, Math.Max(4, _spectrum.Height - 32)));
            }

            sigs.detect_signals(fft_data);

            foreach (var sig in sigs.signalsData)
            {
                if (!sig.overpower) continue;
                using (SolidBrush ob = new SolidBrush(Color.FromArgb(95, 255, 90, 90)))
                {
                    tmp.FillRectangle(ob,
                        Convert.ToInt16(sig.fft_centre * spectrum_wScale) - (Convert.ToInt16((sig.fft_stop - sig.fft_start) * spectrum_wScale) / 2),
                        1,
                        Convert.ToInt16((sig.fft_stop - sig.fft_start) * spectrum_wScale),
                        Math.Max(4, spectrum_h - 3));
                }
            }

            using (Pen gridPen = new Pen(ModernTheme ? Color.FromArgb(75, 142, 165, 190) : Color.FromArgb(200, 123, 123, 123)))
            using (Font rxFont = SpectrumFont(10))
            using (Brush textBrush = SpectrumTextBrush)
            {
                gridPen.DashCap = DashCap.Round;
                gridPen.DashPattern = new float[] { 4.0F, 4.0F };
                for (int i = 0; i < _tuners; i++)
                {
                    int y = i * (spectrum_h / _tuners);
                    tmp.DrawLine(gridPen, 8, y, spectrum_w, y);
                    tmp.DrawString("RX " + (i + 1), rxFont, textBrush, new PointF(4, y));
                }
            }

            drawspectrum_signals(sigs.signalsData);
        }

        private void spectrum_Click(object sender, EventArgs e)
        {
            float spectrum_wScale = _spectrum.Width / 922f;
            MouseEventArgs me = (MouseEventArgs)e;
            int X = me.X;
            int Y = me.Y;

            if (me.Button == MouseButtons.Right)
            {
                string tx_freq = get_bandplan_TX_freq(X, Y);
                debug("TX-Freq: " + tx_freq + " MHz");
                if (!string.IsNullOrEmpty(tx_freq))
                {
                    Clipboard.SetText(tx_freq);
                    TX_Text = " TX: " + tx_freq;
                }
            }
            else selectSignal(X, Y);
        }

        private int determine_rx(int pos)
        {
            int div = Math.Max(1, (PlotBottom - BandplanHeight) / _tuners);
            return Math.Max(0, Math.Min(_tuners - 1, pos / div));
        }

        private void selectSignal(int X, int Y)
        {
            float spectrum_wScale = _spectrum.Width / 922f;
            int rx = determine_rx(Y);
            debug("Select Signal - RX: " + rx);

            try
            {
                foreach (signal.Sig s in sigs.signals)
                {
                    if ((X / spectrum_wScale) > s.fft_start & (X / spectrum_wScale) < s.fft_stop)
                    {
                        sigs.set_tuned(s, rx);
                        rx_blocks[rx, 0] = Convert.ToInt16(s.fft_centre);
                        rx_blocks[rx, 1] = Convert.ToInt16(s.fft_stop - s.fft_start);
                        UInt32 freq = Convert.ToUInt32(s.frequency * 1000);
                        UInt32 sr = Convert.ToUInt32(s.sr * 1000.0);
                        debug("Freq: " + freq);
                        debug("SR: " + sr);
                        OnSignalSelected?.Invoke(rx, freq, sr);
                    }
                }
            }
            catch { }
        }

        int spectrumTunerHighlight = -1;

        public void spectrum_MouseMove(object sender, MouseEventArgs e)
        {
            get_bandplan_TX_freq(e.X, e.Y);
            spectrumTunerHighlight = determine_rx(e.Y);
        }

        private string get_bandplan_TX_freq(int x, int y)
        {
            int n = 0;
            string tx_freq_MHz = "";
            int bandTop = PlotBottom - BandplanHeight;

            if (y >= bandTop)
            {
                if (channels != null && indexedbandplan != null)
                {
                    foreach (Rectangle ch in channels)
                    {
                        if (x >= ch.Left & x <= ch.Right)
                        {
                            int localY = y - bandTop;
                            if (localY >= ch.Location.Y - (ch.Height / 2) + 3 & localY <= ch.Location.Y + (ch.Height / 2) + 3)
                            {
                                tx_freq_MHz = indexedbandplan[n].Element("s-freq").Value;
                                InfoText = " Dn: " + indexedbandplan[n].Element("x-freq").Value + "  SR: " + indexedbandplan[n].Element("name").Value + Environment.NewLine + " Up: " + tx_freq_MHz;
                            }
                        }
                        n++;
                    }
                }
            }
            else if (InfoText != "") InfoText = "";

            return tx_freq_MHz;
        }
    }
}
