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

            FieldInfo nickField = typeof(WebChatForm).GetField("txtNick", BindingFlags.Instance | BindingFlags.NonPublic);
            ToolStripStatusLabel nickLabel = nickField == null ? null : nickField.GetValue(form) as ToolStripStatusLabel;
            if (nickLabel == null) return;

            // IMPORTANT: txtNick is not just display text. The original setNick()
            // method reads txtNick.Text and sends that exact value to BATC. Keep this
            // field as the raw callsign/nickname and use a separate caption for styling.
            string nick = settings == null ? nickLabel.Text : settings.nickname;
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

            FieldInfo nickField = typeof(WebChatForm).GetField("txtNick", BindingFlags.Instance | BindingFlags.NonPublic);
            ToolStripStatusLabel nickLabel = nickField == null ? null : nickField.GetValue(form) as ToolStripStatusLabel;

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
                Text = LoginText(settings),
                ToolTipText = "Set or change the BATC chat nickname"
            };

            EventHandler openLogin = delegate
            {
                ShowLoginDialog(form, settings, login, nickLabel);
            };

            login.Click += openLogin;
            if (nickLabel != null)
                nickLabel.Click += openLogin;

            status.Items.Add(spacer);
            status.Items.Add(login);
        }

        private static string LoginText(WebChatSettings settings)
        {
            string nick = settings == null ? null : settings.nickname;
            if (string.IsNullOrWhiteSpace(nick) || string.Equals(nick, "NONICK", StringComparison.OrdinalIgnoreCase))
                return "LOGIN TO BATC CHAT";

            return "LOGGED IN  •  CHANGE NICK";
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

                string current = settings == null ? "" : settings.nickname;
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

                if (dialog.ShowDialog(form) != DialogResult.OK) return;

                string value = (nick.Text ?? "").Trim().ToUpperInvariant();
                if (value.Length == 0) return;

                if (settings != null) settings.nickname = value;

                // setNick() reads this exact field, so write only the raw nickname.
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
            FieldInfo clientField = typeof(WebChatForm).GetField("client", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setNick == null)
            {
                login.Text = "LOGIN TO BATC CHAT";
                return;
            }

            Action tryLogin = null;
            Timer retry = new Timer { Interval = 250 };
            int attempts = 0;

            tryLogin = delegate
            {
                if (form.IsDisposed)
                {
                    retry.Stop();
                    retry.Dispose();
                    return;
                }

                attempts++;
                bool connected = false;

                try
                {
                    object client = clientField == null ? null : clientField.GetValue(form);
                    PropertyInfo connectedProperty = client == null ? null : client.GetType().GetProperty("Connected", BindingFlags.Instance | BindingFlags.Public);
                    connected = connectedProperty != null && Convert.ToBoolean(connectedProperty.GetValue(client, null));
                }
                catch { }

                if (connected)
                {
                    try
                    {
                        if (nickLabel != null) nickLabel.Text = value;
                        setNick.Invoke(form, null);
                        login.Text = "LOGGED IN  •  CHANGE NICK";
                    }
                    catch
                    {
                        login.Text = "LOGIN TO BATC CHAT";
                    }

                    retry.Stop();
                    retry.Dispose();
                    return;
                }

                if (attempts >= 40) // about 10 seconds
                {
                    login.Text = "LOGIN TO BATC CHAT";
                    retry.Stop();
                    retry.Dispose();
                }
            };

            retry.Tick += delegate { tryLogin(); };
            retry.Start();
            tryLogin();
        }
    }
}
