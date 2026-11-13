using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace opentuner.MediaSources.WinterHill
{
    public partial class WinterHillSettingsForm : Form
    {
        private WinterHillSettings _settings;
        private CheckBox chkSecondPico;
        private TextBox txtSecondPicoIp;
        private TextBox txtSecondPicoBasePort;
        private GroupBox secondPicoGroup;

        public WinterHillSettingsForm(WinterHillSettings Settings)
        {
            InitializeComponent();

            _settings = Settings;

            comboDefaultInterface.SelectedIndex = _settings.DefaultInterface;
            txtWHWSIp.Text = _settings.WinterHillWSHost;
            txtWHWSPort.Text = _settings.WinterHillWSPort.ToString();
            txtWHWSBaseUdp.Text = _settings.WinterHillWSUdpBasePort.ToString();

            txtUDPBasePort.Text = _settings.WinterHillUdpBasePort.ToString();
            txtUDPIP.Text = _settings.WinterHillUdpHost.ToString();
            BuildSecondPicoSettings();
            groupBox2.Height = 78;
            RefreshInterfaceSettingsLayout();

            Shown += delegate
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    ModernSelectorFixPass.StyleDialogSelectors(this);
                });
            };
        }

        // Two PicoTuner units provide four independently controlled receivers.
        private void BuildSecondPicoSettings()
        {
            AutoSize = false;
            groupBox3.Text = "PicoTuner 1 (Ethernet / WinterHill)";
            GroupBox second = new GroupBox
            {
                Name = "groupSecondPicoTuner", Text = "PicoTuner 2 (optional)",
                Location = new Point(12, 314), Size = new Size(471, 154)
            };
            chkSecondPico = new CheckBox
            {
                Name = "chkSecondPicoTuner",
                Text = "Enable second PicoTuner (four receivers)",
                AutoSize = true, Location = new Point(24, 25),
                Checked = _settings.SecondPicoTunerEnabled
            };
            second.Controls.Add(chkSecondPico);
            second.Controls.Add(new Label { Text = "IP Address:", AutoSize = true, Location = new Point(24, 58) });
            txtSecondPicoIp = new TextBox
            {
                Name = "txtSecondPicoIp", Location = new Point(191, 55),
                Size = new Size(206, 22), Text = _settings.SecondPicoTunerUdpHost ?? ""
            };
            second.Controls.Add(txtSecondPicoIp);
            second.Controls.Add(new Label { Text = "UDP Base Port:", AutoSize = true, Location = new Point(24, 87) });
            txtSecondPicoBasePort = new TextBox
            {
                Name = "txtSecondPicoBasePort", Location = new Point(191, 84),
                Size = new Size(113, 22), Text = _settings.SecondPicoTunerUdpBasePort.ToString()
            };
            second.Controls.Add(txtSecondPicoBasePort);
            second.Controls.Add(new Label
            {
                Text = "PicoTuner 1: RX A/B   |   PicoTuner 2: RX C/D",
                Location = new Point(24, 117), Size = new Size(430, 25),
                ForeColor = Color.DimGray
            });
            chkSecondPico.CheckedChanged += delegate
            {
                txtSecondPicoIp.Enabled = chkSecondPico.Checked;
                txtSecondPicoBasePort.Enabled = chkSecondPico.Checked;
            };
            txtSecondPicoIp.Enabled = chkSecondPico.Checked;
            txtSecondPicoBasePort.Enabled = chkSecondPico.Checked;
            groupBox1.Top = second.Bottom + 8;
            btnCancel.Top = groupBox1.Bottom + 16;
            btnSave.Top = btnCancel.Top;
            btnBroadcastListener.Top = btnCancel.Top;
            ClientSize = new Size(501, btnSave.Bottom + 14);
            Controls.Add(second);
            secondPicoGroup = second;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            int wsport = 0;
            int baseport = 0;
            int udpbaseport = 0;

            if (comboDefaultInterface.SelectedIndex == 1 &&
                !int.TryParse(txtWHWSPort.Text, out wsport))
            {
                MessageBox.Show("WinterHill WS Port is not valid.");
                return;
            }

            if (comboDefaultInterface.SelectedIndex == 1 &&
                !int.TryParse(txtWHWSBaseUdp.Text, out baseport))
            {
                MessageBox.Show("WinterHill WS Base Port is not valid");
                return;
            }

            if ((comboDefaultInterface.SelectedIndex == 2 ||
                 comboDefaultInterface.SelectedIndex == 3) &&
                !int.TryParse(txtUDPBasePort.Text, out udpbaseport))
            {
                MessageBox.Show("WinterHill UDP Base Port is not valid");
                return;
            }

            if (comboDefaultInterface.SelectedIndex == 1)
            {
                _settings.WinterHillWSHost = txtWHWSIp.Text;
                _settings.WinterHillWSPort = wsport;
                _settings.WinterHillWSUdpBasePort = baseport;
            }

            if (comboDefaultInterface.SelectedIndex == 2 && chkSecondPico.Checked)
            {
                IPAddress firstAddress, secondAddress;
                if (!IPAddress.TryParse(txtSecondPicoIp.Text.Trim(), out secondAddress) ||
                    secondAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    MessageBox.Show("Enter a valid IPv4 address for PicoTuner 2.");
                    return;
                }
                if (IPAddress.TryParse(txtUDPIP.Text.Trim(), out firstAddress) &&
                    firstAddress.Equals(secondAddress))
                {
                    MessageBox.Show("PicoTuner 2 must have a different IP from PicoTuner 1.");
                    return;
                }
                int secondPort;
                if (!int.TryParse(txtSecondPicoBasePort.Text, out secondPort) ||
                    secondPort < 1024 || secondPort > 65490 ||
                    secondPort % 100 != 0 ||
                    Math.Abs(secondPort - udpbaseport) < 100)
                {
                    MessageBox.Show("PicoTuner 2 needs a separate UDP base port, a multiple of 100 (e.g. 10000).");
                    return;
                }
                _settings.SecondPicoTunerUdpHost = secondAddress.ToString();
                _settings.SecondPicoTunerUdpBasePort = secondPort;
            }
            if (comboDefaultInterface.SelectedIndex == 2)
                _settings.SecondPicoTunerEnabled = chkSecondPico.Checked;
            if (comboDefaultInterface.SelectedIndex == 2 ||
                comboDefaultInterface.SelectedIndex == 3)
            {
                _settings.WinterHillUdpBasePort = udpbaseport;
                _settings.WinterHillUdpHost = txtUDPIP.Text;
            }
            _settings.DefaultInterface = (byte)comboDefaultInterface.SelectedIndex;
            
            DialogResult = DialogResult.OK;
            Close();
        }

        // Show only the settings that belong to the selected receiver type.
        // Preserve every field's value while switching; Save still persists
        // the appropriate backend configuration for later use.
        private void RefreshInterfaceSettingsLayout()
        {
            if (groupBox1 == null || groupBox3 == null ||
                secondPicoGroup == null || comboDefaultInterface == null) return;

            int mode = comboDefaultInterface.SelectedIndex;
            bool pico = mode == 2;
            bool ws = mode == 1;
            bool udp = mode == 3;
            bool showNetwork = pico || udp;

            groupBox3.Visible = showNetwork;
            groupBox3.Text = pico ? "PicoTuner 1 (Ethernet / WinterHill)" :
                "WinterHill UDP (BATC 4 RX)";
            secondPicoGroup.Visible = pico;
            groupBox1.Visible = ws;
            btnBroadcastListener.Visible = pico;
            btnBroadcastListener.Enabled = pico;

            int top = groupBox2.Bottom + 8;
            if (showNetwork)
            {
                groupBox3.Top = top;
                top = groupBox3.Bottom + 8;
            }
            if (pico)
            {
                secondPicoGroup.Top = top;
                top = secondPicoGroup.Bottom + 8;
            }
            if (ws)
            {
                groupBox1.Top = top;
                top = groupBox1.Bottom + 8;
            }

            // Keep a compact dialog in single-interface modes.
            int footerTop = Math.Max(groupBox2.Bottom + 14, top + 8);
            btnSave.Top = footerTop;
            btnCancel.Top = footerTop;
            btnBroadcastListener.Top = footerTop;
            ClientSize = new Size(501, btnSave.Bottom + 14);
        }

        private void comboDefaultInterface_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshInterfaceSettingsLayout();
        }

        private void btnBroadcastListener_Click(object sender, EventArgs e)
        {
            PicoWHBroadcastListenerForm picoWHBroadcastListenerForm = new PicoWHBroadcastListenerForm(_settings);
            picoWHBroadcastListenerForm.Show();
        }
    }
}
