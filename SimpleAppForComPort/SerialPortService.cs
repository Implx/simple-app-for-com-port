using System;
using System.IO.Ports;
using System.Text;

namespace SimpleAppForComPort;

public class SerialPortService
{
    private readonly SerialPort _port = new();

    public bool IsOpen => _port.IsOpen;

    public event Action<string>? MessageReceived; // ASCII text
    public event Action<byte[]>? DataReceived;    // Raw bytes
    public event Action<string>? ErrorOccurred;

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public void Connect(string portName, int baudRate = 115200, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name is required", nameof(portName));

        if (_port.IsOpen)
            Disconnect();

        _port.PortName = portName;
        _port.BaudRate = baudRate;
        _port.Parity = parity;
        _port.DataBits = dataBits;
        _port.StopBits = stopBits;

        _port.DataReceived += OnDataReceived;
        _port.Open();
    }

    public void Disconnect()
    {
        try
        {
            if (_port.IsOpen)
                _port.Close();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
        finally
        {
            _port.DataReceived -= OnDataReceived;
        }
    }

    public void SendAscii(string text)
    {
        if (!_port.IsOpen) throw new InvalidOperationException("Port is closed");
        var bytes = Encoding.ASCII.GetBytes(text);
        _port.Write(bytes, 0, bytes.Length);
    }

    public void SendBytes(byte[] data)
    {
        if (!_port.IsOpen) throw new InvalidOperationException("Port is closed");
        _port.Write(data, 0, data.Length);
    }

    public void SendSetting(byte index, byte value)
    {
        // 's' + index + value
        var packet = new byte[] { (byte)'s', index, value };
        SendBytes(packet);
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            int count = _port.BytesToRead;
            if (count <= 0) return;
            var buffer = new byte[count];
            int read = _port.Read(buffer, 0, count);
            if (read <= 0) return;

            DataReceived?.Invoke(buffer);

            // Also raise ASCII message for convenience
            var text = Encoding.ASCII.GetString(buffer, 0, read);
            MessageReceived?.Invoke(text);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
    }
}