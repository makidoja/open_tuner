using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using opentuner.ExtraFeatures.BATCWebchat;

namespace opentuner
{
    internal static class ModernBatcChatEnhancements
    {
        public static void Attach(WebChatForm form, WebChatSettings settings)
        {
            if (form == null) return;

            form.Shown += delegate
            {
                try
                {
                    AddLoginControl(form, settings);
                }
                catch
                {
                    // Chat must still remain usable if the enhancement cannot be attached.
                }
            };
        }

        private static void AddLoginControl(WebChatForm form, WebChatSettings settings)
        {
            FieldInfo statusField = typeof(WebChatForm).GetField("statusStrip1", BindingFlags.Instance | BindingFlags.NonPublic);
            StatusStrip status = statusField == null ? null : statusField.GetValue(form) as StatusStrip;
            if (status == null || status.Items["ModernBatcLogin"] != null) return;

            ToolStripStatusLabel spacer = new ToolStripStatusLabel
            {
                Spring = true,
                Text = ""
            };

            ToolStripStatusLabel login = new ToolStripStatusLabel
            {
                Name = "ModernBatcLogin",
                IsLink = true,
                LinkColor = Color.FromArgb(28, 139, 253),
                ActiveLinkColor = Color.FromArgb(40, 222, 126),
                Font = new Font("Segoe UI Semibold", 9f),
                Text = LoginText(settings)
            };

            login.Click += delegate
            {
                ShowLoginDialog(form, settings, login);
            };

            status.Items.Add(spacer);
            status.Items.Add(login);
        }

        private static string LoginText(WebChatSettings settings)
        {
            string nick = settings == null ? null : settings.nickname;
            if (string.IsNullOrWhiteSpace(nick) || string.Equals(nick, "NONICK", StringComparison.OrdinalIgnoreCase))
                return "LOGIN TO BATC CHAT";

            return "LOGGED IN: " + nick + "  ·  CHANGE";
        }

        private static void ShowLoginDialog(WebChatForm form, WebChatSettings settings, ToolStripStatusLabel login)
        {
            FieldInfo nickField = typeof(WebChatForm).GetField("txtNick", BindingFlags.Instance | BindingFlags.NonPublic);
            ToolStripStatusLabel nickLabel = nickField == null ? null : nickField.GetValue(form) as ToolStripStatusLabel;
            MethodInfo setNick = typeof(WebChatForm).GetMethod("setNick", BindingFlags.Instance | BindingFlags.NonPublic);

            using (Form dialog = new Form())
            {
                dialog.Text = "BATC Chat Login";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(360, 132);
                dialog.BackColor = Color.FromArgb(13, 28, 45);
                dialog.ForeColor = Color.FromArgb(242, 247, 252);
                dialog.Font = new Font("Segoe UI", 9f);

                Label prompt = new Label
                {
                    Text = "Callsign / nickname",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    ForeColor = dialog.ForeColor
                };

                TextBox nick = new TextBox
                {
                    Location = new Point(18, 42),
                    Width = 324,
                    CharacterCasing = CharacterCasing.Upper,
                    BackColor = Color.FromArgb(18, 38, 60),
                    ForeColor = dialog.ForeColor,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = settings == null || string.Equals(settings.nickname, "NONICK", StringComparison.OrdinalIgnoreCase) ? "" : settings.nickname
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
                    BackColor = Color.FromArgb(18, 38, 60),
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
                if (nickLabel != null) nickLabel.Text = value;
                if (setNick != null) setNick.Invoke(form, null);

                login.Text = "LOGGED IN: " + value + "  ·  CHANGE";
            }
        }
    }
}
