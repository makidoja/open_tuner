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
                    ImproveNickDisplay(form, settings);
                    AddLoginControl(form, settings);
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

        private static void ImproveNickDisplay(WebChatForm form, WebChatSettings settings)
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

        private static void AddLoginControl(WebChatForm form, WebChatSettings settings)
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

            login.Click += delegate { ShowLoginDialog(form, settings, login, nickLabel); };

            status.Items.Add(spacer);
            status.Items.Add(login);

            Timer stateTimer = new Timer { Interval = 500 };
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

                if (!string.Equals(login.Text, "LOGGING IN...", StringComparison.OrdinalIgnoreCase))
                    login.Text = loggedIn ? "LOGGED IN  •  CHANGE NICK" : "LOGIN TO BATC CHAT";
            };
            stateTimer.Start();
        }

        private static void ShowLoginDialog(WebChatForm form, WebChatSettings settings, ToolStripStatusLabel login, ToolStripStatusLabel nickLabel)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "BATC Chat Login";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(360, 132);
                dialog.BackColor = Surface;
                dialog.ForeColor = Text;
                dialog.Font = new Font("Segoe UI", 9f);

                Label prompt = new Label
                {
                    Text = "Callsign / nickname",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    ForeColor = dialog.ForeColor
                };

                string current = nickLabel == null ? "" : nickLabel.Text;
                if (string.Equals(current, "NONICK", StringComparison.OrdinalIgnoreCase)) current = "";

                TextBox nick = new TextBox
                {
                    Location = new Point(18, 42),
                    Width = 324,
                    CharacterCasing = CharacterCasing.Upper,
                    BackColor = Surface2,
                    ForeColor = dialog.ForeColor,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Segoe UI Semibold", 11f),
                    Text = current
                };

                Button ok = new Button
                {
                    Text = "LOGIN",
                    DialogResult = DialogResult.OK,
                    Location = new Point(182, 82),
                    Size = new Size(76, 30),
                    BackColor = Color.FromArgb(28, 139, 253),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                Button cancel = new Button
                {
                    Text = "CANCEL",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(266, 82),
                    Size = new Size(76, 30),
                    BackColor = Surface2,
                    ForeColor = dialog.ForeColor,
                    FlatStyle = FlatStyle.Flat
                };

                dialog.Controls.Add(prompt);
                dialog.Controls.Add(nick);
                dialog.Controls.Add(ok);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                ModernWindowTheme.Apply(dialog);

                if (dialog.ShowDialog(form) != DialogResult.OK) return;

                string value = (nick.Text ?? "").Trim().ToUpperInvariant();
                if (value.Length == 0) return;

                if (settings != null) settings.nickname = value;
                if (nickLabel != null)
                {
                    nickLabel.Text = value;
                    nickLabel.LinkColor = Cyan;
                }

                login.Text = "LOGGING IN...";
                LoginWhenSocketReady(form, value, login, nickLabel);
            }
        }

        private static void LoginWhenSocketReady(WebChatForm form, string value, ToolStripStatusLabel login, ToolStripStatusLabel nickLabel)
        {
            MethodInfo setNick = typeof(WebChatForm).GetMethod("setNick", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setNick == null)
            {
                login.Text = "LOGIN TO BATC CHAT";
                return;
            }

            Timer retry = new Timer { Interval = 250 };
            int attempts = 0;

            EventHandler tryLogin = null;
            tryLogin = delegate
            {
                if (form.IsDisposed)
                {
                    retry.Stop();
                    retry.Dispose();
                    return;
                }

                attempts++;
                try
                {
                    // Use the original WebChatForm login routine directly. setNick() already
                    // checks client.Connected itself, so there is no need to reflect the
                    // SocketIO Connected property (which was the unreliable part here).
                    if (nickLabel != null) nickLabel.Text = value;
                    setNick.Invoke(form, null);

                    TextBox message = GetMessageBox(form);
                    if (message != null && message.Enabled)
                    {
                        login.Text = "LOGGED IN  •  CHANGE NICK";
                        retry.Stop();
                        retry.Dispose();
                        return;
                    }
                }
                catch
                {
                    // Retry while the socket/form finishes initialising.
                }

                if (attempts >= 40)
                {
                    login.Text = "LOGIN TO BATC CHAT";
                    retry.Stop();
                    retry.Dispose();
                }
            };

            retry.Tick += tryLogin;
            retry.Start();
            tryLogin(null, EventArgs.Empty);
        }
    }
}
