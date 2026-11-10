using opentuner.Utilities;
using System;
using System.Drawing;
using System.Windows.Forms;
using opentuner.MediaSources;
using Serilog;

namespace opentuner.ExtraFeatures.BATCWebchat
{
    public class BATCChat
    {
        private WebChatForm _form;
        private WebChatSettings wc_settings;
        SettingsManager<WebChatSettings> wc_settingsManager;

        public BATCChat(OTSource Source) {
            // webchat settings
            wc_settings = new WebChatSettings();
            wc_settingsManager = new SettingsManager<WebChatSettings>("qo100_webchat_settings");
            wc_settings = (wc_settingsManager.LoadSettings(wc_settings));

            _form = new WebChatForm(wc_settings, Source);
            ModernBatcChatEnhancements.Attach(_form, wc_settings);
            // Restore geometry before tracking user-driven changes. The previous
            // Resize handler overwrote saved dimensions during initial restore.
            // Never reopen across monitors or at dimensions too small for chat.
            Rectangle area = Screen.FromPoint(
                new Point(wc_settings.gui_chat_x, wc_settings.gui_chat_y)).WorkingArea;
            int maxWidth = Math.Max(640, Math.Min(1200, area.Width));
            int maxHeight = Math.Max(420, Math.Min(850, area.Height));
            int width = Math.Max(Math.Min(760, maxWidth),
                Math.Min(wc_settings.gui_chat_width > 0 ? wc_settings.gui_chat_width : 900, maxWidth));
            int height = Math.Max(Math.Min(480, maxHeight),
                Math.Min(wc_settings.gui_chat_height > 0 ? wc_settings.gui_chat_height : 650, maxHeight));
            int left = Math.Max(area.Left, Math.Min(wc_settings.gui_chat_x, area.Right - width));
            int top = Math.Max(area.Top, Math.Min(wc_settings.gui_chat_y, area.Bottom - height));
            _form.StartPosition = FormStartPosition.Manual;
            _form.Bounds = new Rectangle(left, top, width, height);
            _form.WindowState = wc_settings.gui_chat_windowstate == (int)FormWindowState.Maximized
                ? FormWindowState.Maximized : FormWindowState.Normal;
            wc_settings.gui_chat_width = width;
            wc_settings.gui_chat_height = height;
            wc_settings.gui_chat_x = left;
            wc_settings.gui_chat_y = top;
            Log.Information("[BATC-CHAT] Restored bounded window: {Width}x{Height} at {X},{Y}",
                width, height, left, top);

            _form.FormClosing += _form_FormClosing;
            _form.Resize += _form_Resize;
            _form.LocationChanged += _form_LocationChanged;
            if (wc_settings.gui_autostart)
            {
                _form.Show();
            }
        }

        private void _form_LocationChanged(object sender, EventArgs e)
        {
            if (_form.WindowState == FormWindowState.Normal)
            {
                wc_settings.gui_chat_x = _form.Left;
                wc_settings.gui_chat_y = _form.Top;
            }
        }

        private void _form_Resize(object sender, EventArgs e)
        {
            if (_form.WindowState == FormWindowState.Normal)
            {
                wc_settings.gui_chat_width = _form.Size.Width;
                wc_settings.gui_chat_height = _form.Size.Height;
            }
        }

        private void _form_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            wc_settings.gui_chat_windowstate = (int)_form.WindowState;
            if (_form.WindowState == FormWindowState.Normal)
            {
                wc_settings.gui_chat_width = _form.Size.Width;
                wc_settings.gui_chat_height = _form.Size.Height;
                wc_settings.gui_chat_x = _form.Left;
                wc_settings.gui_chat_y = _form.Top;
            }
            else if (_form.WindowState == FormWindowState.Minimized)
            {
                wc_settings.gui_chat_windowstate = (int)FormWindowState.Normal;
            }
            wc_settingsManager.SaveSettings(wc_settings);
        }

        public void Show()
        {
            _form.Show();
            _form.Focus();
        }

        public void Close()
        {
            wc_settingsManager.SaveSettings(wc_settings);
            _form.Close();
        }
    }
}
