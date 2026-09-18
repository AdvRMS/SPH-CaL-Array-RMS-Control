using NModbus;
using NModbus.Serial;
using System.IO.Ports;

namespace SPHCaLArrayRMS.Core.Services
{
    public sealed class RelayBoardController : IDisposable
    {
        private const byte LegacySlaveId = 1;
        private const byte ExtensionSlaveId = 2;
        private const ushort HeaterChannelRegister = 0x0081;
        private const ushort HeaterPowerRegister = 0x0082;
        private readonly object _syncRoot = new();
        private readonly string _name;
        private SerialPort? _serialPort;
        private IModbusMaster? _master;

        public bool IsConnected => _serialPort?.IsOpen == true && _master != null;

        public RelayBoardController(string name)
        {
            _name = name;
        }

        public void Connect(string portName, int baudRate = 38400)
        {
            lock (_syncRoot)
            {
                DisconnectCore();
                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 200,
                    WriteTimeout = 200
                };
                _serialPort.Open();
                var factory = new ModbusFactory();
                _master = factory.CreateRtuMaster(new SerialPortAdapter(_serialPort));
                _master.ReadCoils(LegacySlaveId, 0, 1);
                DiagnosticLogger.Log($"{_name} connected on {portName}.");
            }
        }

        public void Disconnect()
        {
            lock (_syncRoot)
                DisconnectCore();
        }

        public void SetRelay(int relayNumber, bool state)
        {
            if (relayNumber < 1 || relayNumber > 8)
                throw new ArgumentOutOfRangeException(nameof(relayNumber));

            lock (_syncRoot)
            {
                EnsureConnected();
                _master!.WriteSingleCoil(LegacySlaveId, (ushort)(relayNumber - 1), state);
                DiagnosticLogger.Log($"{_name} Y{relayNumber} -> {(state ? "ON" : "OFF")}.");
            }
        }

        public void SendHeaterParameters(int channel, int powerWatts)
        {
            if (channel < 1 || channel > 4)
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (powerWatts < 0 || powerWatts > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(powerWatts));

            lock (_syncRoot)
            {
                EnsureConnected();
                byte[] frame = BuildExtensionFrame((ushort)channel, (ushort)powerWatts);
                _serialPort!.Write(frame, 0, frame.Length);
                Thread.Sleep(5);
                DiagnosticLogger.Log($"Heater parameters sent: CH{channel}, {powerWatts} W.");
            }
        }

        private static byte[] BuildExtensionFrame(ushort channel, ushort powerWatts)
        {
            byte[] frame = new byte[13];
            frame[0] = ExtensionSlaveId;
            frame[1] = 0x10;
            frame[2] = (byte)(HeaterChannelRegister >> 8);
            frame[3] = (byte)HeaterChannelRegister;
            frame[4] = 0x00;
            frame[5] = 0x02;
            frame[6] = 0x04;
            frame[7] = (byte)(channel >> 8);
            frame[8] = (byte)channel;
            frame[9] = (byte)(powerWatts >> 8);
            frame[10] = (byte)powerWatts;
            ushort crc = ComputeModbusCrc(frame.AsSpan(0, 11));
            frame[11] = (byte)crc;
            frame[12] = (byte)(crc >> 8);
            return frame;
        }

        private static ushort ComputeModbusCrc(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            foreach (byte value in data)
            {
                crc ^= value;
                for (int i = 0; i < 8; i++)
                    crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
            }
            return crc;
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException($"{_name} is not connected.");
        }

        private void DisconnectCore()
        {
            _master?.Dispose();
            _master = null;
            if (_serialPort?.IsOpen == true)
                _serialPort.Close();
            _serialPort?.Dispose();
            _serialPort = null;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
