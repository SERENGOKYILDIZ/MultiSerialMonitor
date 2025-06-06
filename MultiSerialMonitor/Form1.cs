using System;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MultiSerialMonitor
{
    public partial class Form1 : Form
    {
        private StringBuilder receiveBuffer = new StringBuilder(); // Buffer for incoming serial data
        private DateTime lastReceiveTime;                          // Timestamp of last received data
        private System.Windows.Forms.Timer receiveTimer;           // Timer to batch received data processing

        public Form1()
        {
            InitializeComponent();

            // Fix the form size to prevent resizing
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimumSize = new Size(723, 408);
            this.MaximumSize = new Size(723, 408);

            // Populate ports at startup and setup dynamic refresh on dropdown open
            UpdateAvailablePorts();
            comboBoxPorts.DropDown += comboBoxPorts_DropDown;

            InitializeReceiveTimer();

            // Handle Enter key press to send data
            textBoxSend.KeyDown += TextBoxSend_KeyDown;

            // Initial UI state: no connection
            UpdateConnectionStatus(false, null, 0);
            buttonSend.Enabled = false; // Send button disabled initially
        }

        /// <summary>
        /// Initialize and start a timer to process incoming serial data every 100 ms.
        /// </summary>
        private void InitializeReceiveTimer()
        {
            receiveTimer = new System.Windows.Forms.Timer
            {
                Interval = 100
            };
            receiveTimer.Tick += ReceiveTimer_Tick;
            receiveTimer.Start();
        }

        /// <summary>
        /// Timer event handler that flushes the receive buffer if no new data is received within the timeout.
        /// </summary>
        private void ReceiveTimer_Tick(object sender, EventArgs e)
        {
            if ((DateTime.Now - lastReceiveTime).TotalMilliseconds >= 100 && receiveBuffer.Length > 0)
            {
                string fullMessage = receiveBuffer.ToString();
                receiveBuffer.Clear();
                LogReceivedData(fullMessage);
            }
        }

        /// <summary>
        /// Refresh the list of available COM ports, preserving the current selection if still valid.
        /// </summary>
        private void UpdateAvailablePorts()
        {
            string currentSelection = comboBoxPorts.SelectedItem?.ToString();

            string[] ports = SerialPort.GetPortNames();
            comboBoxPorts.Items.Clear();
            comboBoxPorts.Items.AddRange(ports);

            if (ports.Length == 0)
            {
                comboBoxPorts.Text = "No COM ports found";
            }
            else if (currentSelection != null && ports.Contains(currentSelection))
            {
                comboBoxPorts.SelectedItem = currentSelection;
            }
            else
            {
                comboBoxPorts.SelectedIndex = 0;
            }

            // Ensure a baud rate is selected if not set
            if (comboBoxBaudRate.Items.Count > 0 && comboBoxBaudRate.SelectedIndex == -1)
                comboBoxBaudRate.SelectedIndex = 0;
        }

        /// <summary>
        /// Event handler for dropdown opening of the ports combo box to refresh port list.
        /// </summary>
        private void comboBoxPorts_DropDown(object sender, EventArgs e)
        {
            UpdateAvailablePorts();
        }

        /// <summary>
        /// Handle Enter key press in the send text box to trigger sending data if connected.
        /// </summary>
        private void TextBoxSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

                if (serialPort1.IsOpen)
                {
                    buttonSend.PerformClick();
                }
                else
                {
                    MessageBox.Show("Connection is not open. Cannot send data.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        /// <summary>
        /// Update the status label and the Send button enabled state based on connection status.
        /// </summary>
        /// <param name="connected">True if serial port is connected.</param>
        /// <param name="portName">Port name string.</param>
        /// <param name="baudRate">Baud rate value.</param>
        private void UpdateConnectionStatus(bool connected, string portName, int baudRate)
        {
            if (connected)
            {
                toolStripStatusLabelConnection.Text = $"Connected to {portName} at {baudRate} baud";
                buttonSend.Enabled = true;
            }
            else
            {
                toolStripStatusLabelConnection.Text = "No connection";
                buttonSend.Enabled = false;
            }
        }

        /// <summary>
        /// Connect or disconnect serial port when Connect button is clicked.
        /// </summary>
        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.PortName = comboBoxPorts.SelectedItem?.ToString();
                    serialPort1.BaudRate = int.Parse(comboBoxBaudRate.SelectedItem?.ToString() ?? "9600");
                    serialPort1.DataReceived += SerialPort1_DataReceived;
                    serialPort1.Open();

                    buttonConnect.Text = "Disconnect";
                    MessageBox.Show("Connection established.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UpdateConnectionStatus(true, serialPort1.PortName, serialPort1.BaudRate);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateConnectionStatus(false, null, 0);
                }
            }
            else
            {
                try
                {
                    serialPort1.Close();
                    buttonConnect.Text = "Connect";
                    MessageBox.Show("Connection closed.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UpdateConnectionStatus(false, null, 0);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error closing connection: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Send data through serial port if connected.
        /// </summary>
        private void buttonSend_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show("Connection is not open!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string dataToSend = textBoxSend.Text.Trim();
            if (string.IsNullOrEmpty(dataToSend))
                return;

            try
            {
                serialPort1.WriteLine(dataToSend);
                LogSentData(dataToSend);
                textBoxSend.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Send error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Event handler for serial port DataReceived event, buffering incoming data.
        /// </summary>
        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string incomingData = serialPort1.ReadExisting();
            receiveBuffer.Append(incomingData);
            lastReceiveTime = DateTime.Now;
        }

        /// <summary>
        /// Clear the log display.
        /// </summary>
        private void buttonClear_Click(object sender, EventArgs e)
        {
            richTextBoxLog.Clear();
        }

        /// <summary>
        /// Replace control characters (\r and \n) with visible representations.
        /// </summary>
        /// <param name="data">Input string.</param>
        /// <returns>String with control characters shown as [0x0D] and [0x0A].</returns>
        private string ShowControlChars(string data)
        {
            return data.Replace("\r", "[0x0D]").Replace("\n", "[0x0A]");
        }

        /// <summary>
        /// Append a colored line of text to the log textbox.
        /// </summary>
        private void AppendLogLine(string text, Color color)
        {
            richTextBoxLog.SelectionStart = richTextBoxLog.TextLength;
            richTextBoxLog.SelectionColor = color;
            richTextBoxLog.AppendText(text + Environment.NewLine);
            richTextBoxLog.ScrollToCaret();
        }

        /// <summary>
        /// Log sent data with timestamp, visible control characters, and byte count.
        /// </summary>
        private void LogSentData(string data)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string visibleData = ShowControlChars(data);
            int byteCount = Encoding.ASCII.GetByteCount(data);
            string logLine = $"[{timestamp}] TRANSMIT DATA: \"{visibleData}\", Total: {byteCount} byte";
            AppendLogLine(logLine, Color.Lime);
        }

        /// <summary>
        /// Log received data with timestamp, visible control characters, and byte count.
        /// </summary>
        private void LogReceivedData(string data)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string visibleData = ShowControlChars(data);
            int byteCount = Encoding.ASCII.GetByteCount(data);
            string logLine = $"[{timestamp}] RECEIVE DATA: \"{visibleData}\", Total: {byteCount} byte";
            AppendLogLine(logLine, Color.Cyan);
        }
    }
}
