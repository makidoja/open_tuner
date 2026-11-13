using System;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using Serilog;
using System.Collections.Generic;
using System.Drawing;

namespace opentuner.MediaSources.WinterHill
{
    public partial class PicoWHBroadcastListenerForm : Form
    {
        UdpClient WH_Client = new UdpClient();

        private PicoWHBroadcastListener broadCastListener;
        private readonly WinterHillSettings settings;
        private readonly Dictionary<string, DetectedPico> devices =
            new Dictionary<string, DetectedPico>(StringComparer.OrdinalIgnoreCase);
        private ComboBox deviceSelector;
        private Label selectLabel;

        private sealed class DetectedPico
        {
            public string Address;
            public int BasePort;
            public bool Seen;
            public string Name;
            public override string ToString()
            {
                return Name + " - " + Address +
                    (Seen ? " (UDP " + BasePort + ")" : " (awaiting broadcast)");
            }
        }

        public PicoWHBroadcastListenerForm() : this(null) { }

        public PicoWHBroadcastListenerForm(WinterHillSettings currentSettings)
        {
            settings = currentSettings;
            InitializeComponent();
            BuildDeviceSelector();
        }

        private void BuildDeviceSelector()
        {
            selectLabel = new Label
            {
                Text = "Select PicoTuner:",
                AutoSize = true, Location = new Point(514, 81)
            };
            deviceSelector = new ComboBox
            {
                Name = "PicoTunerDeviceSelector",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(628, 77), Size = new Size(267, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(18, 38, 60),
                ForeColor = Color.White
            };
            deviceSelector.SelectedIndexChanged += delegate { ShowSelectedDevice(); };
            Controls.Add(selectLabel);
            Controls.Add(deviceSelector);

            if (settings != null)
            {
                AddConfiguredDevice(settings.WinterHillUdpHost,
                    settings.WinterHillUdpBasePort, "PicoTuner 1");
                if (settings.SecondPicoTunerEnabled)
                    AddConfiguredDevice(settings.SecondPicoTunerUdpHost,
                        settings.SecondPicoTunerUdpBasePort, "PicoTuner 2");
            }
            groupBox1.Top = 116;
            groupBox1.Height = 260;
            btnClose.Top = 390;
            lbBroadcast.Height = 365;
            ClientSize = new Size(918, 428);
            ShowSelectedDevice();
        }

        private void AddConfiguredDevice(string address, int basePort, string name)
        {
            IPAddress parsed;
            if (!IPAddress.TryParse(address, out parsed) ||
                parsed.AddressFamily != AddressFamily.InterNetwork) return;
            DetectedPico entry;
            if (!devices.TryGetValue(address, out entry))
            {
                entry = new DetectedPico { Address = address, Name = name, BasePort = basePort };
                devices[address] = entry;
                deviceSelector.Items.Add(entry);
                if (deviceSelector.SelectedIndex < 0) deviceSelector.SelectedIndex = 0;
            }
            else entry.Name = name;
        }

        private void ShowSelectedDevice()
        {
            if (lblDetectedIP == null || lblDetectedBasePort == null) return;
            DetectedPico device = deviceSelector == null ? null :
                deviceSelector.SelectedItem as DetectedPico;
            lblDetectedIP.Text = device == null ? "Unknown" : device.Address;
            lblDetectedBasePort.Text = device == null || !device.Seen ?
                "Unknown" : device.BasePort.ToString();
            bool ready = device != null && device.Seen;
            btnReset.Enabled = ready;
            btnReboot.Enabled = ready;
            btnBootsel.Enabled = ready;
            btnChangeBasePort.Enabled = ready;
            if (ready) txtNewBasePort.Text = device.BasePort.ToString();
        }


        private void UpdateLabel(Label Lbl, Object obj)
        {

            if (Lbl == null)
                return;

            if (obj == null)
                return;

            if (Lbl.InvokeRequired)
            {
                UpdateLabelDelegate ulb = new UpdateLabelDelegate(UpdateLabel);
                if (Lbl != null)
                {
                    Lbl?.Invoke(ulb, new object[] { Lbl, obj });
                }
            }
            else
            {
                Lbl.Text = obj.ToString();
            }

        }

        public static void UpdateLB(ListBox LB, Object obj)
        {

            if (LB == null)
                return;

            if (LB.InvokeRequired)
            {
                UpdateLBDelegate ulb = new UpdateLBDelegate(UpdateLB);
                if (LB != null)
                {
                    LB?.Invoke(ulb, new object[] { LB, obj });
                }
            }
            else
            {
                if (LB.Items.Count > 1000)
                {
                    LB.Items.Remove(0);
                }

                int i = LB.Items.Add(DateTime.Now.ToShortTimeString() + " : " + obj);
                LB.TopIndex = i;
            }

        }

        private void PicoWHBroadcastListenerForm_Load(object sender, EventArgs e)
        {
            try
            {
                broadCastListener = new PicoWHBroadcastListener();
                broadCastListener.OnBroadcastFromDevice += BroadCastListener_OnBroadcastFromDevice;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Opening the Broadcast Listener - Make sure you don't have one running already.\n" + ex.Message);
                Close();
            }
        }

        private void BroadCastListener_OnBroadcastFromDevice(string senderIp, string data)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        BroadCastListener_OnBroadcastFromDevice(senderIp, data);
                    });
                }
                catch (InvalidOperationException) { }
                return;
            }

            string[] lines = data.Split('\n');
            int receivedPort = 0;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                UpdateLB(lbBroadcast, "[" + senderIp + "] " + line);
                int pos = line.IndexOf("Base IP port", StringComparison.OrdinalIgnoreCase);
                if (pos >= 0)
                {
                    string tail = line.Substring(pos + "Base IP port".Length).Trim();
                    int.TryParse(tail, out receivedPort);
                }
            }

            DetectedPico entry;
            if (!devices.TryGetValue(senderIp, out entry))
            {
                entry = new DetectedPico
                {
                    Address = senderIp, Name = "Detected PicoTuner",
                    BasePort = receivedPort, Seen = true
                };
                devices[senderIp] = entry;
                deviceSelector.Items.Add(entry);
                if (deviceSelector.SelectedIndex < 0)
                    deviceSelector.SelectedItem = entry;
            }
            else
            {
                entry.Seen = true;
                if (receivedPort > 0) entry.BasePort = receivedPort;
            }
            deviceSelector.Refresh();
            ShowSelectedDevice();
        }

        public void CloseForm()
        {
            broadCastListener?.Close();
            broadCastListener = null;
            Close();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseForm();
        }

        private void lblDetectedIP_TextChanged(object sender, EventArgs e)
        {
            if (lblDetectedIP.Text != "Unknown")
            {
                EnableRemoteCommands();
            }
        }

        private void lblDetectedBasePort_TextChanged(object sender, EventArgs e)
        {
            if (lblDetectedBasePort.Text != "Unknown")
            {
                EnableRemoteCommands();
            }
        }

        private void EnableRemoteCommands()
        {
            btnReset.Enabled = true;
            btnReboot.Enabled = true;
            btnBootsel.Enabled = true;
            btnChangeBasePort.Enabled = true;
        }

        public void SendRemoteCommand(string command)
        {
            DetectedPico device = deviceSelector.SelectedItem as DetectedPico;
            if (device == null || !device.Seen || device.BasePort <= 0)
            {
                MessageBox.Show("Select a detected PicoTuner before sending commands.");
                return;
            }
            IPAddress address;
            if (!IPAddress.TryParse(device.Address, out address)) return;
            int controlPort = (device.BasePort / 100) * 100 + 20;
            IPEndPoint endpoint = new IPEndPoint(address, controlPort);
            byte[] payload = Encoding.ASCII.GetBytes(command);
            try
            {
                WH_Client.Client.SendTo(payload, endpoint);
                Log.Information("Broadcast listener command {Command} to {Ip}:{Port}",
                    command, device.Address, controlPort);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to send PicoTuner command");
                MessageBox.Show("Failed to send command: " + ex.Message);
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            SendRemoteCommand("[to@wh] reset=147");
        }

        private void btnReboot_Click(object sender, EventArgs e)
        {
            SendRemoteCommand("[to@wh] reboot=258");
        }

        private void btnBootsel_Click(object sender, EventArgs e)
        {
            SendRemoteCommand("[to@wh] bootsel=369");
        }

        private void btnChangeBasePort_Click(object sender, EventArgs e)
        {
            int new_baseport = 0;

            // PicoTuner firmware accepts even base-port values ending
            // in 00..14; require separate ranges for configured devices.
            if (!int.TryParse(txtNewBasePort.Text.Trim(), out new_baseport) ||
                new_baseport < 1024 || new_baseport > 65400 ||
                new_baseport % 2 != 0 || new_baseport % 100 > 14)
            {
                MessageBox.Show("Enter an even UDP base port ending in 00..14 (for example 9800 or 9900).");
                return;
            }
            DetectedPico selected = deviceSelector.SelectedItem as DetectedPico;
            if (selected == null || !selected.Seen) return;
            foreach (DetectedPico other in devices.Values)
            {
                if (other == selected) continue;
                if (Math.Abs(other.BasePort - new_baseport) < 100)
                {
                    MessageBox.Show("This UDP port range overlaps another PicoTuner.");
                    return;
                }
            }
            if (MessageBox.Show("Change base port on " + selected.Name +
                " (" + selected.Address + ") from " + selected.BasePort +
                " to " + new_baseport + "?\n\nUpdate Source Settings to match afterwards.",
                "Confirm PicoTuner port change", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            SendRemoteCommand("[to@wh] bip=" + new_baseport.ToString());
        }

        private void llblCopyToClickboardIP_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Clipboard.SetText(lblDetectedIP.Text);
        }

        private void llblCopyToClickboardPort_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Clipboard.SetText(lblDetectedBasePort.Text);
        }

        private void PicoWHBroadcastListenerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (broadCastListener != null)
            {
                broadCastListener.Close();
                broadCastListener = null;
            }
            WH_Client.Close();
        }
    }
}
