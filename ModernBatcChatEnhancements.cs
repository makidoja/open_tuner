using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using opentuner.ExtraFeatures.BATCWebchat;

namespace opentuner
{
    internal static class ModernBatcChatEnhancements
    {
        private static readonly Color Cyan = Color.FromArgb(77, 208, 255);
        private static readonly Color Green = Color.FromArgb(40, 222, 126);
        private static readonly Color Surface = Color.FromArgb(13, 28, 45);
        private static readonly Color Surface2 = Color.FromArgb(18, 38, 60);
        private static readonly Color Text = Color.FromArgb(242, 247, 252);

        public static void Attach(WebChatForm form, WebChatSettings settings)
        {
            if (form == null) return;

            form.Shown += delegate
            {
                try
                {
                    ImproveNickDisplay(form);
                    AddLoginControl(form);
                }
                catch
                {
                    // Chat must still remain usable if the enhancement cannot be attached.
                }
            };
        }

        private static ToolStripStatusLabel GetNickLabel(WebChatForm form)
        {
            FieldInfo nickField = typeof(WebChatForm).GetField("txtNick", BindingFlags.Instance | BindingFlags.NonPublic);
            return nickField == null ? null : nickField.GetValue(form) as ToolStripStatusLabel;
        }

        private static TextBox GetMessageBox(WebChatForm form)
        {
            FieldInfo field = typeof(WebChatForm).GetField("txtMessage", BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(form) as TextBox;
        }

        private static void ImproveNickDisplay(WebChatForm form)
        {
            FieldInfo statusField = typeof(WebChatForm).GetField("statusStrip1", BindingFlags.Instance | BindingFlags.NonPublic);
            StatusStrip status = statusField == null ? null : statusField.GetValue(form) as StatusStrip;
            if (status == null) return;

            status.AutoSize = false;
            status.Height = 30;
            status.BackColor = Surface;
            status.ForeColor = Text;
            status.SizingGrip = true;

            ToolStripStatusLabel nickLabel = GetNickLabel(form);
            if (nickLabel == null) return;

            string nick = nickLabel.Text;
            if (string.IsNullOrWhiteSpace(nick)) nick = "NONICK";
            nickLabel.Text = nick.Trim().ToUpperInvariant();

            if (status.Items["ModernBatcNickCaption"] == null)
            {
                ToolStripStatusLabel caption = new ToolStripStatusLabel
                {
                    Name = "ModernBatcNickCaption",
                    Text = "NICK:",
                    ForeColor = Text,
                    BackColor = Surface2,
                    Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                    Padding = new Padding(8, 3, 2, 3),
                    Margin = new Padding(2, 2, 0, 2)
                };

                int index = status.Items.IndexOf(nickLabel);
                if (index < 0) status.Items.Insert(0, caption);
                else status.Items.Insert(index, caption);
            }

            nickLabel.IsLink = true;
            nickLabel.LinkBehavior = LinkBehavior.NeverUnderline;
            nickLabel.LinkColor = Cyan;
            nickLabel.ActiveLinkColor = Green;
            nickLabel.VisitedLinkColor = Cyan;
            nickLabel.BackColor = Surface2;
            nickLabel.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            nickLabel.Padding = new Padding(2, 3, 10, 3);
            nickLabel.Margin = new Padding(0, 2, 4, 2);
            nickLabel.ToolTipText = "Click to set or change BATC chat nickname";
        }

        private static void AddLoginControl(WebChatForm form)
        {
            FieldInfo statusField = typeof(WebChatForm).GetField("statusStrip1", BindingFlags.Instance | BindingFlags.NonPublic);
            StatusStrip status = statusField == null ? null : statusField.GetValue(form) as StatusStrip;
            if (status == null || status.Items["ModernBatcLogin"] != null) return;

            ToolStripStatusLabel nickLabel = GetNickLabel(form);

            ToolStripStatusLabel spacer = new ToolStripStatusLabel
            {
                Spring = true,
                Text = ""
            };

            ToolStripStatusLabel login = new ToolStripStatusLabel
            {
                Name = "ModernBatcLogin",
                IsLink = true,
                LinkBehavior = LinkBehavior.NeverUnderline,
                LinkColor = Green,
                ActiveLinkColor = Cyan,
                VisitedLinkColor = Green,
                BackColor = Surface2,
                ForeColor = Text,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Padding = new Padding(10, 3, 10, 3),
                Margin = new Padding(4, 2, 4, 2),
                Text = "LOGIN TO BATC CHAT",
                ToolTipText = "Set or change the BATC chat nickname"
            };

            // Use the exact original nickname click path. This is the same working path as
            // clicking the blue NONICK/M0CKE label at the bottom-left of the chat window.
            login.Click += delegate
            {
                if (nickLabel != null && !nickLabel.IsDisposed)
                    nickLabel.PerformClick();
            };

            status.Items.Add(spacer);
            status.Items.Add(login);

            Timer stateTimer = new Timer { Interval = 300 };
            stateTimer.Tick += delegate
            {
                if (form.IsDisposed)
                {
                    stateTimer.Stop();
                    stateTimer.Dispose();
                    return;
                }

                TextBox message = GetMessageBox(form);
                bool loggedIn = message != null && message.Enabled && nickLabel != null &&
                                !string.IsNullOrWhiteSpace(nickLabel.Text) &&
                                !string.Equals(nickLabel.Text.Trim(), "NONICK", StringComparison.OrdinalIgnoreCase);

                login.Text = loggedIn ? "LOGGED IN  •  CHANGE NICK" : "LOGIN TO BATC CHAT";
            };
            stateTimer.Start();
        }
    }
}
