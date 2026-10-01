using System;
using System.Windows.Forms;

namespace opentuner
{
    public partial class MainForm
    {
        private void EnforceModernDashboardChrome()
        {
            if (mdRoot == null)
                return;

            foreach (Control control in Controls)
            {
                if (!object.ReferenceEquals(control, mdRoot))
                    control.Visible = false;
            }

            mdRoot.Visible = true;
            mdRoot.Dock = DockStyle.Fill;
            mdRoot.BringToFront();

            if (mdSourceSettings == null && mdSource != null)
                InitialiseSourceSettingsButton();

            if (mdSourceSettings != null)
            {
                mdSourceSettings.Visible = true;
                mdSourceSettings.BringToFront();
            }
        }
    }
}
