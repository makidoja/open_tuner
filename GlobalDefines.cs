using System;
using System.Drawing;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    public static class GlobalDefines
    {
        public const int CircularBufferStartingCapacity = 250000;
        public const string Version = "0.B-modern";
    }

    /// <summary>
    /// Central visual language for the OpenTuner modern UI.
    /// The receiver, transport and media engines are deliberately untouched.
    /// </summary>
    public static class ModernTheme
    {
        public static readonly Color Background = Color.FromArgb(14, 18, 24);
        public static readonly Color Surface = Color.FromArgb(22, 28, 36);
        public static readonly Color SurfaceRaised = Color.FromArgb(29, 36, 46);
        public static readonly Color SurfaceHover = Color.FromArgb(37, 46, 58);
        public static readonly Color Border = Color.FromArgb(52, 63, 78);

        public static readonly Color TextPrimary = Color.FromArgb(239, 244, 249);
        public static readonly Color TextSecondary = Color.FromArgb(159, 171, 185);
        public static readonly Color TextMuted = Color.FromArgb(108, 121, 136);

        public static readonly Color Accent = Color.FromArgb(45, 140, 255);
        public static readonly Color AccentHover = Color.FromArgb(69, 157, 255);
        public static readonly Color Success = Color.FromArgb(48, 209, 88);
        public static readonly Color Warning = Color.FromArgb(255, 184, 0);
        public static readonly Color Danger = Color.FromArgb(255, 84, 89);

        public static readonly Font FontBody = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontBodySemibold = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontSection = new Font("Segoe UI Semibold", 11f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontTitle = new Font("Segoe UI Semibold", 16f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontStatus = new Font("Segoe UI Semibold", 10f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontFrequency = new Font("Segoe UI Semibold", 28f, FontStyle.Regular, GraphicsUnit.Point);

        public static void ApplyToForm(Form form)
        {
            if (form == null)
                return;

            form.BackColor = Background;
            form.ForeColor = TextPrimary;
            form.Font = FontBody;
            ApplyToControlTree(form);
        }

        public static void ApplyToControlTree(Control root)
        {
            if (root == null)
                return;

            ApplyToControl(root);

            foreach (Control child in root.Controls)
                ApplyToControlTree(child);
        }

        public static Button CreateButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = false;
            button.Height = 34;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = FontBodySemibold;
            button.Cursor = System.Windows.Forms.Cursors.Hand;
            button.ForeColor = TextPrimary;
            button.BackColor = primary ? Accent : SurfaceRaised;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : SurfaceHover;
            button.FlatAppearance.MouseDownBackColor = primary ? Accent : Surface;
            return button;
        }

        private static void ApplyToControl(Control control)
        {
            control.Font = FontBody;

            Label label = control as Label;
            if (label != null)
            {
                label.BackColor = Color.Transparent;
                label.ForeColor = label.Cursor == System.Windows.Forms.Cursors.Hand ? AccentHover : TextPrimary;
                return;
            }

            Button button = control as Button;
            if (button != null)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = SurfaceHover;
                button.BackColor = SurfaceRaised;
                button.ForeColor = TextPrimary;
                button.Font = FontBodySemibold;
                return;
            }

            TextBox textBox = control as TextBox;
            if (textBox != null)
            {
                textBox.BackColor = SurfaceRaised;
                textBox.ForeColor = TextPrimary;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            ComboBox comboBox = control as ComboBox;
            if (comboBox != null)
            {
                comboBox.BackColor = SurfaceRaised;
                comboBox.ForeColor = TextPrimary;
                comboBox.FlatStyle = FlatStyle.Flat;
                return;
            }

            CheckBox checkBox = control as CheckBox;
            if (checkBox != null)
            {
                checkBox.BackColor = Color.Transparent;
                checkBox.ForeColor = TextPrimary;
                return;
            }

            ListBox listBox = control as ListBox;
            if (listBox != null)
            {
                listBox.BackColor = Surface;
                listBox.ForeColor = TextPrimary;
                listBox.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            GroupBox groupBox = control as GroupBox;
            if (groupBox != null)
            {
                groupBox.BackColor = Background;
                groupBox.ForeColor = TextPrimary;
                groupBox.Font = FontBodySemibold;
                return;
            }

            TabPage tabPage = control as TabPage;
            if (tabPage != null)
            {
                tabPage.BackColor = Background;
                tabPage.ForeColor = TextPrimary;
                return;
            }

            TabControl tabControl = control as TabControl;
            if (tabControl != null)
            {
                tabControl.BackColor = Background;
                tabControl.ForeColor = TextPrimary;
                return;
            }

            MenuStrip menuStrip = control as MenuStrip;
            if (menuStrip != null)
            {
                menuStrip.BackColor = Surface;
                menuStrip.ForeColor = TextPrimary;
                menuStrip.RenderMode = ToolStripRenderMode.System;
                return;
            }

            SplitContainer split = control as SplitContainer;
            if (split != null)
            {
                split.BackColor = Border;
                split.Panel1.BackColor = Background;
                split.Panel2.BackColor = Background;
                return;
            }

            Panel panel = control as Panel;
            if (panel != null)
            {
                panel.BackColor = Background;
                panel.ForeColor = TextPrimary;
                return;
            }

            control.ForeColor = TextPrimary;
        }
    }

    /// <summary>
    /// Modern presentation layer applied on top of the existing MainForm.
    /// Existing receiver behaviour remains unchanged.
    /// </summary>
    public partial class MainForm
    {
        private Panel modernHeader;
        private Panel modernFooter;
        private FlowLayoutPanel modernReceiverStrip;
        private ComboBox modernSourceSelector;
        private Label modernConnectionStatus;
        private Label modernSourceStatus;
        private Timer modernUiTimer;
        private bool modernUiInitialised;
        private OTSource modernHookedSource;
        private Label[] modernTunerHeadings = new Label[4];
        private Label[] modernTunerFrequencies = new Label[4];
        private Label[] modernTunerDetails = new Label[4];
        private Label[] modernTunerStates = new Label[4];

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!modernUiInitialised)
            {
                modernUiInitialised = true;
                BuildModernShell();
            }
        }

        private void BuildModernShell()
        {
            SuspendLayout();

            ModernTheme.ApplyToForm(this);
            BackColor = ModernTheme.Background;
            ForeColor = ModernTheme.TextPrimary;
            MinimumSize = new Size(1040, 680);

            StyleMainMenu();
            CreateModernHeader();
            CreateModernReceiverStrip();
            CreateModernFooter();
            HookDynamicTheming(this);

            if (splitContainer1 != null)
            {
                splitContainer1.BackColor = ModernTheme.Border;
                splitContainer1.SplitterWidth = 4;
                if (splitContainer1.Width > 700)
                    splitContainer1.SplitterDistance = Math.Max(300, Math.Min(370, splitContainer1.Width / 3));
            }

            if (tabControl1 != null)
            {
                tabControl1.Font = ModernTheme.FontBodySemibold;
                tabControl1.Padding = new Point(12, 6);
            }

            if (btnSourceConnect != null)
            {
                btnSourceConnect.BackColor = ModernTheme.Accent;
                btnSourceConnect.FlatAppearance.BorderColor = ModernTheme.Accent;
                btnSourceConnect.FlatAppearance.MouseOverBackColor = ModernTheme.AccentHover;
            }

            if (sourceInfo != null)
            {
                sourceInfo.BackColor = ModernTheme.Surface;
                sourceInfo.ForeColor = ModernTheme.TextSecondary;
            }

            modernUiTimer = new Timer();
            modernUiTimer.Interval = 400;
            modernUiTimer.Tick += ModernUiTimer_Tick;
            modernUiTimer.Start();

            ResumeLayout(true);
            UpdateModernStatus();
        }

        private void StyleMainMenu()
        {
            if (menuStrip1 == null)
                return;

            menuStrip1.BackColor = ModernTheme.Surface;
            menuStrip1.ForeColor = ModernTheme.TextPrimary;
            menuStrip1.Font = ModernTheme.FontBody;
            menuStrip1.Padding = new Padding(8, 3, 0, 3);

            foreach (ToolStripItem item in menuStrip1.Items)
            {
                item.ForeColor = ModernTheme.TextPrimary;
                item.BackColor = ModernTheme.Surface;
            }
        }

        private void CreateModernHeader()
        {
            modernHeader = new Panel();
            modernHeader.Name = "modernHeader";
            modernHeader.Dock = DockStyle.Top;
            modernHeader.Height = 64;
            modernHeader.Padding = new Padding(18, 12, 18, 10);
            modernHeader.BackColor = ModernTheme.Surface;

            Label title = new Label();
            title.AutoSize = true;
            title.Text = "OpenTuner";
            title.Font = ModernTheme.FontTitle;
            title.ForeColor = ModernTheme.TextPrimary;
            title.Location = new Point(18, 17);

            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.Text = "DATV Receiver Console";
            subtitle.Font = ModernTheme.FontBody;
            subtitle.ForeColor = ModernTheme.TextSecondary;
            subtitle.Location = new Point(132, 23);

            modernConnectionStatus = new Label();
            modernConnectionStatus.AutoSize = false;
            modernConnectionStatus.Size = new Size(110, 30);
            modernConnectionStatus.TextAlign = ContentAlignment.MiddleCenter;
            modernConnectionStatus.Font = ModernTheme.FontStatus;
            modernConnectionStatus.BackColor = ModernTheme.SurfaceRaised;
            modernConnectionStatus.ForeColor = ModernTheme.TextSecondary;
            modernConnectionStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            modernSourceSelector = new ComboBox();
            modernSourceSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            modernSourceSelector.Font = ModernTheme.FontBody;
            modernSourceSelector.BackColor = ModernTheme.SurfaceRaised;
            modernSourceSelector.ForeColor = ModernTheme.TextPrimary;
            modernSourceSelector.FlatStyle = FlatStyle.Flat;
            modernSourceSelector.Width = 170;
            modernSourceSelector.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            if (comboAvailableSources != null)
            {
                foreach (object item in comboAvailableSources.Items)
                    modernSourceSelector.Items.Add(item);

                if (comboAvailableSources.SelectedIndex >= 0 && comboAvailableSources.SelectedIndex < modernSourceSelector.Items.Count)
                    modernSourceSelector.SelectedIndex = comboAvailableSources.SelectedIndex;

                modernSourceSelector.SelectedIndexChanged += delegate
                {
                    if (comboAvailableSources.SelectedIndex != modernSourceSelector.SelectedIndex)
                        comboAvailableSources.SelectedIndex = modernSourceSelector.SelectedIndex;
                };

                comboAvailableSources.SelectedIndexChanged += delegate
                {
                    if (modernSourceSelector.SelectedIndex != comboAvailableSources.SelectedIndex && comboAvailableSources.SelectedIndex < modernSourceSelector.Items.Count)
                        modernSourceSelector.SelectedIndex = comboAvailableSources.SelectedIndex;
                };
            }

            Button connect = ModernTheme.CreateButton("Connect", true);
            connect.Width = 92;
            connect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            connect.Click += delegate
            {
                if (btnSourceConnect != null)
                    btnSourceConnect.PerformClick();
            };

            Button presets = ModernTheme.CreateButton("Presets", false);
            presets.Width = 82;
            presets.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            presets.Click += delegate
            {
                if (menuManageFrequencyPresets != null)
                    menuManageFrequencyPresets.PerformClick();
            };

            Button settings = ModernTheme.CreateButton("Settings", false);
            settings.Width = 84;
            settings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            settings.Click += delegate
            {
                if (settingsToolStripMenuItem != null)
                    settingsToolStripMenuItem.PerformClick();
            };

            modernHeader.Controls.Add(title);
            modernHeader.Controls.Add(subtitle);
            modernHeader.Controls.Add(modernSourceSelector);
            modernHeader.Controls.Add(connect);
            modernHeader.Controls.Add(presets);
            modernHeader.Controls.Add(settings);
            modernHeader.Controls.Add(modernConnectionStatus);

            Controls.Add(modernHeader);
            modernHeader.BringToFront();

            EventHandler layoutHeader = delegate
            {
                int x = modernHeader.ClientSize.Width - 18;

                modernConnectionStatus.Location = new Point(x - modernConnectionStatus.Width, 16);
                x -= modernConnectionStatus.Width + 10;
                settings.Location = new Point(x - settings.Width, 14);
                x -= settings.Width + 8;
                presets.Location = new Point(x - presets.Width, 14);
                x -= presets.Width + 8;
                connect.Location = new Point(x - connect.Width, 14);
                x -= connect.Width + 10;
                modernSourceSelector.Location = new Point(x - modernSourceSelector.Width, 18);
            };

            modernHeader.Resize += layoutHeader;
            layoutHeader(modernHeader, EventArgs.Empty);
        }

        private void CreateModernReceiverStrip()
        {
            modernReceiverStrip = new FlowLayoutPanel();
            modernReceiverStrip.Name = "modernReceiverStrip";
            modernReceiverStrip.Dock = DockStyle.Top;
            modernReceiverStrip.Height = 92;
            modernReceiverStrip.Padding = new Padding(12, 7, 12, 7);
            modernReceiverStrip.BackColor = ModernTheme.Background;
            modernReceiverStrip.WrapContents = false;
            modernReceiverStrip.AutoScroll = true;

            for (int i = 0; i < 4; i++)
            {
                Panel card = new Panel();
                card.Width = 232;
                card.Height = 72;
                card.Margin = new Padding(4);
                card.Padding = new Padding(10, 7, 10, 6);
                card.BackColor = ModernTheme.Surface;

                modernTunerHeadings[i] = new Label();
                modernTunerHeadings[i].AutoSize = true;
                modernTunerHeadings[i].Text = "TUNER " + (i + 1).ToString();
                modernTunerHeadings[i].Font = ModernTheme.FontBodySemibold;
                modernTunerHeadings[i].ForeColor = ModernTheme.TextSecondary;
                modernTunerHeadings[i].Location = new Point(10, 7);

                modernTunerStates[i] = new Label();
                modernTunerStates[i].AutoSize = false;
                modernTunerStates[i].Size = new Size(74, 20);
                modernTunerStates[i].Location = new Point(146, 5);
                modernTunerStates[i].TextAlign = ContentAlignment.MiddleRight;
                modernTunerStates[i].Text = "IDLE";
                modernTunerStates[i].Font = ModernTheme.FontBodySemibold;
                modernTunerStates[i].ForeColor = ModernTheme.TextMuted;

                modernTunerFrequencies[i] = new Label();
                modernTunerFrequencies[i].AutoSize = true;
                modernTunerFrequencies[i].Text = "— MHz";
                modernTunerFrequencies[i].Font = ModernTheme.FontSection;
                modernTunerFrequencies[i].ForeColor = ModernTheme.TextPrimary;
                modernTunerFrequencies[i].Location = new Point(10, 29);

                modernTunerDetails[i] = new Label();
                modernTunerDetails[i].AutoSize = true;
                modernTunerDetails[i].Text = "Waiting for receiver data";
                modernTunerDetails[i].Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
                modernTunerDetails[i].ForeColor = ModernTheme.TextMuted;
                modernTunerDetails[i].Location = new Point(10, 51);

                card.Controls.Add(modernTunerHeadings[i]);
                card.Controls.Add(modernTunerStates[i]);
                card.Controls.Add(modernTunerFrequencies[i]);
                card.Controls.Add(modernTunerDetails[i]);
                modernReceiverStrip.Controls.Add(card);
            }

            Controls.Add(modernReceiverStrip);
            modernReceiverStrip.BringToFront();
            if (modernHeader != null)
                modernHeader.BringToFront();
        }

        private void CreateModernFooter()
        {
            modernFooter = new Panel();
            modernFooter.Name = "modernFooter";
            modernFooter.Dock = DockStyle.Bottom;
            modernFooter.Height = 34;
            modernFooter.Padding = new Padding(16, 7, 16, 5);
            modernFooter.BackColor = ModernTheme.Surface;

            modernSourceStatus = new Label();
            modernSourceStatus.Dock = DockStyle.Fill;
            modernSourceStatus.TextAlign = ContentAlignment.MiddleLeft;
            modernSourceStatus.Font = ModernTheme.FontBody;
            modernSourceStatus.ForeColor = ModernTheme.TextSecondary;

            Label build = new Label();
            build.Dock = DockStyle.Right;
            build.Width = 145;
            build.TextAlign = ContentAlignment.MiddleRight;
            build.Text = "modern-ui  •  " + GlobalDefines.Version;
            build.Font = ModernTheme.FontBody;
            build.ForeColor = ModernTheme.TextMuted;

            modernFooter.Controls.Add(modernSourceStatus);
            modernFooter.Controls.Add(build);
            Controls.Add(modernFooter);
            modernFooter.BringToFront();
        }

        private void ModernUiTimer_Tick(object sender, EventArgs e)
        {
            EnsureModernDataHook();
            UpdateModernStatus();
        }

        private void EnsureModernDataHook()
        {
            if (!source_connected || videoSource == null)
                return;

            if (modernHookedSource == videoSource)
                return;

            if (modernHookedSource != null)
                modernHookedSource.OnSourceData -= ModernSourceData;

            modernHookedSource = videoSource;
            modernHookedSource.OnSourceData += ModernSourceData;
        }

        private void ModernSourceData(int videoNumber, OTSourceData data, string description)
        {
            if (data == null || videoNumber < 0 || videoNumber >= modernTunerFrequencies.Length)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, OTSourceData, string>(ModernSourceData), videoNumber, data, description);
                return;
            }

            double mhz = data.frequency / 1000.0;
            modernTunerFrequencies[videoNumber].Text = mhz.ToString("0.000") + " MHz";
            modernTunerStates[videoNumber].Text = data.demod_locked ? "● LOCK" : "NO LOCK";
            modernTunerStates[videoNumber].ForeColor = data.demod_locked ? ModernTheme.Success : ModernTheme.Warning;

            string service = String.IsNullOrWhiteSpace(data.service_name) ? "No service" : data.service_name;
            string mode = String.IsNullOrWhiteSpace(data.modcode) ? "" : "  •  " + data.modcode;
            modernTunerDetails[videoNumber].Text = service + "  •  " + data.symbol_rate.ToString() + " kS  •  MER " + data.mer.ToString("0.0") + " dB" + mode;
            modernTunerHeadings[videoNumber].Text = String.IsNullOrWhiteSpace(description) ? "TUNER " + (videoNumber + 1).ToString() : description.ToUpperInvariant();
        }

        private void UpdateModernStatus()
        {
            if (modernConnectionStatus == null || modernSourceStatus == null)
                return;

            if (source_connected)
            {
                modernConnectionStatus.Text = "● CONNECTED";
                modernConnectionStatus.ForeColor = ModernTheme.Success;
                modernConnectionStatus.BackColor = Color.FromArgb(22, 48, 37);
            }
            else
            {
                modernConnectionStatus.Text = "● READY";
                modernConnectionStatus.ForeColor = ModernTheme.TextSecondary;
                modernConnectionStatus.BackColor = ModernTheme.SurfaceRaised;
            }

            string sourceName = "No source selected";
            if (comboAvailableSources != null && comboAvailableSources.SelectedItem != null)
                sourceName = comboAvailableSources.SelectedItem.ToString();

            modernSourceStatus.Text = source_connected
                ? sourceName + "  •  receiver active"
                : sourceName + "  •  select a source and press Connect";
        }

        private void HookDynamicTheming(Control parent)
        {
            if (parent == null)
                return;

            parent.ControlAdded += ModernControlAdded;

            foreach (Control child in parent.Controls)
                HookDynamicTheming(child);
        }

        private void ModernControlAdded(object sender, ControlEventArgs e)
        {
            if (e.Control == null || e.Control == modernHeader || e.Control == modernFooter || e.Control == modernReceiverStrip)
                return;

            ModernTheme.ApplyToControlTree(e.Control);
            HookDynamicTheming(e.Control);
        }
    }
}
