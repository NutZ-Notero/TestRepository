using System;
using System.Collections.Generic;
using System.Linq;
using Notero.MidiPlugin;

namespace Notero.MidiPluginConnection
{
    public class MidiPort
    {
        public string MacAddress { get; }

        public bool AutoConnection { get; private set; }

        public MidiPort(string macAddress, bool isAuto = true)
        {
            MacAddress = macAddress;
            AutoConnection = isAuto;
        }

        public void SetAutoConnection(bool isAuto)
        {
            AutoConnection = isAuto;
        }
    }

    public class MidiPluginConnector
    {
        public List<MidiBluetoothDeviceData> BluetoothDeviceInfoList => m_BluetoothDeviceInfoList;
        private List<MidiBluetoothDeviceData> m_BluetoothDeviceInfoList = new();

        public virtual IMidiBluetoothPlugin MidiBluetoothPlugin => Notero.MidiPlugin.MidiBluetoothPlugin.Instance;
        public virtual IMidiPlugin MidiPlugin => Notero.MidiPlugin.MidiPlugin.Instance;

        public event Action<string> OnDeviceConnected
        {
            add => MidiBluetoothPlugin.OnDeviceConnected += value;
            remove => MidiBluetoothPlugin.OnDeviceConnected -= value;
        }

        public event Action<string> OnDeviceDisconnected
        {
            add => MidiBluetoothPlugin.OnDeviceDisconnected += value;
            remove => MidiBluetoothPlugin.OnDeviceDisconnected -= value;
        }

        private List<MidiPort> m_MidiPortInfo = new();
        private bool m_IsReconnectAutomation = true;

        public void Init()
        {
            MidiBluetoothPlugin.Init();
            MidiPlugin.Init();
        }

        public void SetReconnectAutomation(bool isAuto)
        {
            m_IsReconnectAutomation = isAuto;

            if (isAuto) ForceReconnectMidiDevice();
        }

        public void ForceReconnectMidiDevice()
        {
            UpdateMidiDevice();

            UpdateMidiDeviceFromPort();
            ReconnectMidiDevice();
        }

        public void SetMidiPortToAutoConnection(string macAddress, bool isAutoConnection)
        {
            UpdateMidiDevice();

            UpdateMidiDeviceFromPort();

            var midiPort = m_MidiPortInfo.FirstOrDefault(midiPort => midiPort.MacAddress == macAddress);
            midiPort?.SetAutoConnection(isAutoConnection);

            OnDeviceStatusChange();
        }

        public void OnDeviceStatusChange()
        {
            UpdateMidiDevice();

            UpdateMidiDeviceFromPort();

            if(!HasNoConnectedMidiDevice() && !MidiDeviceConnectionsOutOfSync()) return;

            if(m_IsReconnectAutomation) ReconnectMidiDevice();
        }

        public void UpdateMidiDevice()
        {
            var midiBluetoothDevices = MidiBluetoothPlugin.GetMidiDevices().ToList();
            var midiBluetoothDevicesConnected = MidiBluetoothPlugin.GetConnectedMidiDevices().ToList();

            m_BluetoothDeviceInfoList = midiBluetoothDevicesConnected.Union(midiBluetoothDevices).ToList();
        }

        private void UpdateMidiDeviceFromPort()
        {
            var macAddress = m_BluetoothDeviceInfoList.Select(device => device.MacAddress);
            var connectedPorts = new HashSet<string>(macAddress);
            var newData = m_MidiPortInfo.Where(port => connectedPorts.Contains(port.MacAddress)).ToList();

            newData.AddRange(
                from port in connectedPorts
                where m_MidiPortInfo.All(p => p.MacAddress != port)
                select new MidiPort(port)
            );

            m_MidiPortInfo = newData;
        }

        public void OpenDeviceToPortByAddress(string macAddress)
        {
            MidiBluetoothPlugin.OpenDeviceToPort(macAddress);
            MidiPlugin.OpenMidiInputPort(GetDeviceBluetoothByMacAddress(macAddress).DeviceName);
            SetMidiPortToAutoConnection(macAddress, true);
        }

        public void CloseDeviceFromPortByAddress(string macAddress)
        {
            MidiBluetoothPlugin.CloseDeviceFromPort(macAddress);
            MidiPlugin.CloseMidiInputPort(GetDeviceBluetoothByMacAddress(macAddress).DeviceName);
            SetMidiPortToAutoConnection(macAddress, false);
        }

        private MidiBluetoothDeviceData GetDeviceBluetoothByMacAddress(string macAddress)
        {
            return m_BluetoothDeviceInfoList.Find(device => device.MacAddress == macAddress);
        }

        private void ReconnectMidiDevice()
        {
            foreach(var midiPort in m_MidiPortInfo.Where(midiInfo => midiInfo.AutoConnection))
            {
                if(m_BluetoothDeviceInfoList.Any(device => device.MacAddress == midiPort.MacAddress && device.IsConnected)) continue;

                CloseDeviceFromPortByAddress(midiPort.MacAddress);
                OpenDeviceToPortByAddress(midiPort.MacAddress);
            }
        }

        private bool HasNoConnectedMidiDevice()
        {
            return m_BluetoothDeviceInfoList.Count == 0;
        }

        private bool MidiDeviceConnectionsOutOfSync()
        {
            return !m_BluetoothDeviceInfoList
                .All(bluetoothDevice =>
                    m_MidiPortInfo.Any(midiInfo => midiInfo.MacAddress == bluetoothDevice.MacAddress && !midiInfo.AutoConnection) ||
                    bluetoothDevice.IsConnected
                );
        }

        public void ConnectBluetooth(string macAddress)
        {
            MidiBluetoothPlugin.ConnectBluetooth(macAddress);
        }

        public void DisconnectBluetooth(string macAddress)
        {
            MidiBluetoothPlugin.DisconnectBluetooth(macAddress);
        }

        public void ConnectBluetooth(string macAddress, string deviceNameForOpenPort)
        {
            ConnectBluetooth(macAddress);
            MidiPlugin.OpenMidiInputPort(deviceNameForOpenPort);
        }

        public void DisconnectBluetooth(string macAddress, string deviceNameForClosePort)
        {
            MidiPlugin.CloseMidiInputPort(deviceNameForClosePort);
            DisconnectBluetooth(macAddress);
        }
    }
}
