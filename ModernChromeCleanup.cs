using System;
using System.Windows.Forms;

namespace opentuner
{
    public partial class MainForm
    {
        private Timer mdChromeGuardTimer;

        private void EnforceModernDashboardChrome()
        {
            if (mdRoot == null)
                return;

            // Anything added to MainForm by the earlier interim shell is not part of
            // the real modern dashboard. Keep only mdRoot visible while modern view
            // is active. This also catches controls created after OnActivated.
            foreach (Control control in Controls)
            {
                if (!object.ReferenceEquals(control, mdRoot))
                    control.Visible = false;
            }

            mdRoot.Visible = true;
            mdRoot.Dock = DockStyle.Fill;
            mdRoot.BringToFront();

            // Source Settings must always exist on the real dashboard header.
            // Reset the initialisation flag if an earlier/temporary header consumed it.
            if (mdSourceSettings == null && mdSource != null)
            {
                mdSourceSettingsInitialised = false;
                InitialiseSourceSettingsButton();
            }

            if (mdSourceSettings != null)
            {
                mdSourceSettings.Visible = true;
                mdSourceSettings.Enabled = true;
                mdSourceSettings.BringToFront();
            }

            // Some legacy/interim controls are created after the form first activates.
            // Guard the modern surface continuously, but stop interfering if the user
            // deliberately switches to Advanced / Legacy view (mdRoot is then hidden).
            if (mdChromeGuardTimer == null)
            {
                mdChromeGuardTimer = new Timer { Interval = 250 };
                mdChromeGuardTimer.Tick += delegate
                {
                    if (mdRoot == null || !mdRoot.Visible)
                        return;

                    foreach (Control control in Controls)
                    {
                        if (!object.ReferenceEquals(control, mdRoot) && control.Visible)
                            control.Visible = false;
                    }

                    if (mdSourceSettings == null && mdSource != null)
                    {
                        mdSourceSettingsInitialised = false;
                        InitialiseSourceSettingsButton();
                    }

                    if (mdSourceSettings != null)
                    {
                        mdSourceSettings.Visible = true;
                        mdSourceSettings.BringToFront();
                    }

                    mdRoot.BringToFront();
                };
                mdChromeGuardTimer.Start();
            }
        }
    }
}
