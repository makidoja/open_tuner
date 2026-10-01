using System;
using System.Drawing;
using System.Windows.Forms;

namespace opentuner
{
    public partial class MainForm
    {
        private ComboBox mdTunerViewMode;
        private Label mdTunerViewCaption;
        private Timer mdTunerLayoutTimer;
        private bool mdTunerLayoutInitialised;
        private int mdVisibleTunerCount = 2;

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            if (mdRoot == null)
                BuildModernDashboard();

            if (!mdTunerLayoutInitialised && mdTunerSelector != null)
            {
                mdTunerLayoutInitialised = true;
                InitialiseTunerLayoutSelector();
            }

            if (!mdSourceSettingsInitialised && mdSource != null)
                InitialiseSourceSettingsButton();

            EnforceModernDashboardChrome();
        }

        private void InitialiseTunerLayoutSelector()
        {
            Control tuningPanel = mdTunerSelector.Parent;
            if (tuningPanel == null)
                return;

            mdTunerViewCaption = new Label
            {
                Text = "TUNER VIEW",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = ModernTheme.TextMuted,
                Location = new Point(18, 91)
            };

            mdTunerViewMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ModernTheme.SurfaceRaised,
                ForeColor = ModernTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBody,
                Location = new Point(126, 88),
                Size = new Size(137, 28)
            };
            mdTunerViewMode.Items.AddRange(new object[] { "AUTO", "2 TUNERS", "4 TUNERS" });
            mdTunerViewMode.SelectedIndex = 0;
            mdTunerViewMode.SelectedIndexChanged += delegate { ApplyTunerLayout(); };

            tuningPanel.Controls.Add(mdTunerViewCaption);
            tuningPanel.Controls.Add(mdTunerViewMode);
            mdTunerViewCaption.BringToFront();
            mdTunerViewMode.BringToFront();

            foreach (Control control in tuningPanel.Controls)
            {
                if (control == mdTunerViewCaption || control == mdTunerViewMode || control == mdTunerSelector)
                    continue;

                if (control.Top >= 102)
                    control.Top += 30;
            }

            mdTunerLayoutTimer = new Timer { Interval = 750 };
            mdTunerLayoutTimer.Tick += delegate { ApplyTunerLayout(); };
            mdTunerLayoutTimer.Start();

            ApplyTunerLayout();
        }

        private int GetRequestedTunerCount()
        {
            if (mdTunerViewMode != null)
            {
                if (mdTunerViewMode.SelectedIndex == 1)
                    return 2;
                if (mdTunerViewMode.SelectedIndex == 2)
                    return 4;
            }

            if (source_connected && videoSource != null)
            {
                try
                {
                    int count = videoSource.GetVideoSourceCount();
                    return count >= 4 ? 4 : 2;
                }
                catch
                {
                    return 2;
                }
            }

            return 2;
        }

        private void ApplyTunerLayout()
        {
            if (mdTunerButtons == null)
                return;

            int requested = GetRequestedTunerCount();
            if (requested != 2 && requested != 4)
                requested = 2;

            mdVisibleTunerCount = requested;

            for (int i = 0; i < mdTunerButtons.Length; i++)
            {
                if (mdTunerButtons[i] != null)
                    mdTunerButtons[i].Visible = i < mdVisibleTunerCount;
            }

            if (mdActiveTuner >= mdVisibleTunerCount)
                SelectDashboardTuner(0);

            if (mdTunerSelector != null)
                mdTunerSelector.Width = mdVisibleTunerCount == 2 ? 120 : 245;
        }
    }
}
