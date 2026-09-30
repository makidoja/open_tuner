using System;

namespace opentuner
{
    public partial class MainForm
    {
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Build the modern operating surface only after the original form has
            // completed all Load/Shown initialization. This guarantees that the
            // legacy designer cannot be brought back over the modern dashboard by
            // later startup code.
            if (mdRoot == null)
                BuildModernDashboard();
        }
    }
}
