using System;
using System.Drawing;
using System.IO.Ports;
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

            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimumSize = new Size(723, 408);
            this.MaximumSize = new Size(723, 408);

            PopulateAvailablePorts();
            InitializeReceiveTimer();
            textBoxSend.KeyDown += TextBoxSend_KeyDown;

            UpdateConnectionStatus(false, null, 0); // There is no connection in the first case
            buttonSend.Enabled = false; // Initially, the send button is passive
        }

        /// <summary>
        /// Initialize and start a timer to process incoming serial data every 100 ms
        /// </summary>
        private void InitializeReceiveTimer()
        {
            receiveTimer = new System.Windows.Forms.Timer
            {
                Interval = 100 // Interval in milliseconds
            };
            receiveTimer.Tick += ReceiveTimer_Tick;
            receiveTimer.Start();
        }

        /// <summary>
        /// Timer event handler to process buffered incoming data if timeout elapsed
        /// </summary>
        private void ReceiveTimer_Tick(object sender, EventArgs e)
        {
            // Check if 100ms passed since last received data and buffer is not empty
            if ((DateTime.Now - lastReceiveTime).TotalMilliseconds >= 100 && receiveBuffer.Length > 0)
            {
                string fullMessage = receiveBuffer.ToString();
                receiveBuffer.Clear();
                LogReceivedData(fullMessage);
            }
        }

        /// <summary>
        /// Populate the available serial ports into the comboBoxPorts and select defaults
        /// </summary>
        private void PopulateAvailablePorts()
        {
            string[] ports = SerialPort.GetPortNames();
            comboBoxPorts.Items.Clear();
            comboBoxPorts.Items.AddRange(ports);

            if (ports.Length > 0)
                comboBoxPorts.SelectedIndex = 0;

            if (comboBoxBaudRate.Items.Count > 0)
                comboBoxBaudRate.SelectedIndex = 0;
        }

        /// <summary>
        /// Handles pressing Enter key in textBoxSend to trigger sending data if port is open
        /// </summary>
        private void TextBoxSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Prevent ding sound and new line

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
        /// Updates status label and Send button enable state according to connection
        /// </summary>
        /// <param name="connected">Is serial port connected?</param>
        /// <param name="portName">Connected port name</param>
        /// <param name="baudRate">Connected baud rate</param>
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
        /// Connect or disconnect the serial port based on current state
        /// </summary>
        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.PortName = comboBoxPorts.SelectedItem.ToString();
                    serialPort1.BaudRate = int.Parse(comboBoxBaudRate.SelectedItem.ToString());
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
        /// Send the data from textBoxSend if the serial port is open
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
        /// Event handler for serial port data reception, append data to buffer and update last receive timestamp
        /// </summary>
        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string incomingData = serialPort1.ReadExisting();
            receiveBuffer.Append(incomingData);
            lastReceiveTime = DateTime.Now;
        }

        /// <summary>
        /// Clear the richTextBoxLog content
        /// </summary>
        private void buttonClear_Click(object sender, EventArgs e)
        {
            richTextBoxLog.Clear();
        }

        /// <summary>
        /// Helper method to convert control characters (\r and \n) into visible notation
        /// </summary>
        /// <param name="data">Input string data</param>
        /// <returns>String with control chars replaced</returns>
        private string ShowControlChars(string data)
        {
            return data.Replace("\r", "[0x0D]").Replace("\n", "[0x0A]");
        }

        /// <summary>
        /// Append a colored line of text to the richTextBoxLog and scroll to the bottom
        /// </summary>
        private void AppendLogLine(string text, Color color)
        {
            richTextBoxLog.SelectionStart = richTextBoxLog.TextLength;
            richTextBoxLog.SelectionColor = color;
            richTextBoxLog.AppendText(text + Environment.NewLine);
            richTextBoxLog.ScrollToCaret();
        }

        /// <summary>
        /// Log sent data in richTextBoxLog with timestamp, visible control characters and byte count
        /// </summary>
        /// <param name="data">Sent data string</param>
        private void LogSentData(string data)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string visibleData = ShowControlChars(data);
            int byteCount = Encoding.ASCII.GetByteCount(data);
            string logLine = $"[{timestamp}] TRANSMIT DATA: \"{visibleData}\", Total: {byteCount} byte";
            AppendLogLine(logLine, Color.Lime);
        }

        /// <summary>
        /// Log received data in richTextBoxLog with timestamp, visible control characters and byte count
        /// </summary>
        /// <param name="data">Received data string</param>
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
