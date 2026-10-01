using System;
using System.Drawing;
using System.Windows.Forms;

namespace opentuner
{
    public partial class MainForm
    {
        private Button mdSourceSettings;
        private bool mdSourceSettingsInitialised;

        private void InitialiseSourceSettingsButton()
        {
            if (mdSourceSettingsInitialised || mdSource == null || mdSource.Parent == null)
                return;

            mdSourceSettingsInitialised = true;
            Control header = mdSource.Parent;

            mdSourceSettings = ModernTheme.CreateButton("SOURCE SETTINGS", false);
            mdSourceSettings.Width = 126;
            mdSourceSettings.Height = 32;
            mdSourceSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            mdSourceSettings.Click += delegate { ShowSelectedSourceSettings(); };
            header.Controls.Add(mdSourceSettings);
            mdSourceSettings.BringToFront();

            EventHandler positionButton = delegate
            {
                mdSourceSettings.Location = new Point(
                    Math.Max(280, mdSource.Left - mdSourceSettings.Width - 8),
                    13);
            };

            header.Resize += positionButton;
            positionButton(header, EventArgs.Empty);
        }

        private void ShowSelectedSourceSettings()
        {
            int selectedIndex = -1;

            if (mdSource != null)
                selectedIndex = mdSource.SelectedIndex;
            if (selectedIndex < 0 && comboAvailableSources != null)
                selectedIndex = comboAvailableSources.SelectedIndex;

            if (selectedIndex < 0 || selectedIndex >= _availableSources.Count)
            {
                MessageBox.Show(
                    "Select a receiver source first.",
                    "OpenTuner - Source Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                _availableSources[selectedIndex].ShowSettings();

                // Source settings can affect the description and connection method.
                // Keep the legacy selector in sync with the modern selector.
                if (comboAvailableSources != null && comboAvailableSources.SelectedIndex != selectedIndex)
                    comboAvailableSources.SelectedIndex = selectedIndex;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open settings for " + _availableSources[selectedIndex].GetName() + ".\r\n\r\n" + ex.Message,
                    "OpenTuner - Source Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
