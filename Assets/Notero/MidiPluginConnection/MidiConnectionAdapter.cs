using System;
using Notero.MidiPlugin;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Notero.MidiPluginConnection
{
    public interface IMidiConnectable
    {
        Action<List<MidiBluetoothDeviceData>> OnBluetoothDeviceInfoUpdate { get; set; }
        Action OnScanDeviceCompleted { get; set; }
        Action<string> OnConnectedToPort { get; set; }
        Action<string> OnDisconnectedToPort { get; set; }
        Action OnDeviceStatusChanged { get; set; }
        Action<bool> OnCheckBluetoothCompleted { get; set; }
        List<MidiBluetoothDeviceData> BluetoothDeviceInfoList { get; }

        event MidiBytesEvent OnMidiBytesEvent;

        void BluetoothResultStatusHandler(string status);
        void CheckBluetoothPermissions();
        void CheckIsBluetoothEnabled();
        void CloseDeviceFromPortByAddress(string macAddress);
        void CloseDeviceFromPortByName(string deviceName);
        void ConnectBluetooth(string macAddress);
        void ConnectBluetooth(string macAddress, string deviceNameForOpenPort);
        void DisconnectBluetooth(string macAddress);
        void DisconnectBluetooth(string macAddress, string deviceNameForClosePort);
        List<MidiBluetoothDeviceData> GetMidiBluetoothDevices();
        void GoToBluetoothSettings();
        void NoteOffReceived(string midiInfoJson);
        void NoteOnReceived(string midiInfoJson);
        void OpenDeviceToPortByAddress(string macAddress);
        void OpenDeviceToPortByName(string deviceName);
        void ScanMidiBluetoothDevice();
        void ShowBluetoothMIDIDevices();
        void StopScanMidiBluetoothDevice();
        void TickMidiBluetoothDeviceResult();
        void UpdateMidiDevice();
    }

    public abstract class MidiConnectionAdapter : MonoBehaviour, IMidiConnectable
    {
        public event MidiBytesEvent OnMidiBytesEvent;

        protected const string AndroidBridgeName = "MidiBluetoothAdapter";

        protected MidiPluginConnector m_MidiConnector;

        public Action<List<MidiBluetoothDeviceData>> OnBluetoothDeviceInfoUpdate { get; set; }
        public Action OnScanDeviceCompleted { get; set; }
        public Action<string> OnConnectedToPort { get; set; }
        public Action<string> OnDisconnectedToPort { get; set; }
        public Action OnDeviceStatusChanged { get; set; }
        public Action<bool> OnCheckBluetoothCompleted { get; set; }

        public virtual List<MidiBluetoothDeviceData> BluetoothDeviceInfoList => m_MidiConnector.BluetoothDeviceInfoList;

        private IMidiBluetoothPlugin m_MidiBluetoothPlugin;

        #region Mono Methods
        protected virtual void Awake()
        {
            m_MidiConnector = new MidiPluginConnector();
            m_MidiBluetoothPlugin = m_MidiConnector.MidiBluetoothPlugin;

            m_MidiConnector.Init();
            SubscribeEvents();

            m_MidiConnector.ForceReconnectMidiDevice();
        }
        protected virtual void OnDestroy()
        {
            UnsubscribeEvents();

            m_MidiBluetoothPlugin = null;
            m_MidiConnector = null;
        }

        protected virtual void OnApplicationQuit()
        {
            DisconnectMidiDevice();
        }

        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) m_MidiConnector.OnDeviceStatusChange();
        }
        #endregion

        #region Abstracts Methods
        protected abstract void NoteOnReceived(int midiId, int velocity, int channel);

        protected abstract void NoteOffReceived(int midiId, int velocity, int channel);

        protected abstract void OnMidiBytesEventHandler(byte[] bytes);

        /// <summary>
        /// For Android
        /// </summary>
        /// <param name="midiInfoJson"></param>
        public abstract void NoteOnReceived(string midiInfoJson);

        /// <summary>
        /// For Android
        /// </summary>
        /// <param name="midiInfoJson"></param>
        public abstract void NoteOffReceived(string midiInfoJson);

        /// <summary>
        /// For Android
        /// </summary>
        /// <param name="midiInfoJson"></param>
        protected abstract void OnMidiBytesEventHandler(string midiInfoJson);
        #endregion

        #region Events Related
        protected virtual void SubscribeEvents()
        {
            m_MidiBluetoothPlugin.OnBluetoothPermissionResult += BluetoothPermissionResultStatusHandler;

            m_MidiBluetoothPlugin.OnDeviceConnected += ConnectedToPortHandler;
            m_MidiBluetoothPlugin.OnDeviceDisconnected += DisconnectedFromPortHandler;
            m_MidiBluetoothPlugin.OnDeviceStatusChange += UpdateDeviceStatus;

            m_MidiBluetoothPlugin.OnBluetoothStatusResult += BluetoothResultStatusHandler;
            m_MidiBluetoothPlugin.OnTickMidiBluetoothDeviceResult += UpdateMidiDevice;
            m_MidiBluetoothPlugin.OnScanCompleted += ScanMidiBluetoothDeviceCompleted;

            m_MidiBluetoothPlugin.NoteOnEvent += NoteOnReceived;
            m_MidiBluetoothPlugin.NoteOffEvent += NoteOffReceived;
            m_MidiBluetoothPlugin.MidiBytesEvent += OnMidiBytesEventHandler;
        }

        protected virtual void UnsubscribeEvents()
        {
            m_MidiBluetoothPlugin.OnBluetoothPermissionResult -= BluetoothPermissionResultStatusHandler;

            m_MidiBluetoothPlugin.OnDeviceConnected -= ConnectedToPortHandler;
            m_MidiBluetoothPlugin.OnDeviceDisconnected -= DisconnectedFromPortHandler;
            m_MidiBluetoothPlugin.OnDeviceStatusChange -= UpdateDeviceStatus;

            m_MidiBluetoothPlugin.OnBluetoothStatusResult -= BluetoothResultStatusHandler;
            m_MidiBluetoothPlugin.OnTickMidiBluetoothDeviceResult -= UpdateMidiDevice;
            m_MidiBluetoothPlugin.OnScanCompleted -= ScanMidiBluetoothDeviceCompleted;

            m_MidiBluetoothPlugin.NoteOnEvent -= NoteOnReceived;
            m_MidiBluetoothPlugin.NoteOffEvent -= NoteOffReceived;
            m_MidiBluetoothPlugin.MidiBytesEvent -= OnMidiBytesEventHandler;
        }

        /// <summary>
        /// Call from Android native plugin.
        /// </summary>
        protected virtual void UpdateDeviceStatus()
        {
            m_MidiConnector.OnDeviceStatusChange();
            OnDeviceStatusChanged?.Invoke();
        }

        protected virtual void BluetoothPermissionResultStatusHandler()
        {
            CheckIsBluetoothEnabled();
        }

        protected virtual void BluetoothResultStatusHandler(bool status)
        {
            OnCheckBluetoothCompleted?.Invoke(status);
        }

        protected virtual void ConnectedToPortHandler(string macAddress)
        {
            OnConnectedToPort?.Invoke(macAddress);
        }

        protected virtual void DisconnectedFromPortHandler(string macAddress)
        {
            OnDisconnectedToPort?.Invoke(macAddress);
        }

        protected virtual void ScanMidiBluetoothDeviceCompleted()
        {
            UpdateMidiDevice();
            OnScanDeviceCompleted?.Invoke();
        }

        public virtual void UpdateMidiDevice()
        {
            m_MidiConnector.UpdateMidiDevice();
        }
        #endregion

        #region Bluetooth Plugin Facade
        public virtual void CheckIsBluetoothEnabled()
        {
            m_MidiBluetoothPlugin.CheckIsBluetoothEnabled();
        }

        public virtual void ShowBluetoothMIDIDevices()
        {
            m_MidiBluetoothPlugin.ShowBluetoothMIDIDevices();
        }

        public virtual void ScanMidiBluetoothDevice()
        {
            m_MidiBluetoothPlugin.ScanMidiBluetooth();
        }

        public virtual void StopScanMidiBluetoothDevice()
        {
            m_MidiBluetoothPlugin.StopScanMidiBluetooth();
        }

        public virtual List<MidiBluetoothDeviceData> GetMidiBluetoothDevices()
        {
            return m_MidiBluetoothPlugin.GetMidiBluetoothDevices().ToList();
        }

        public virtual void CheckBluetoothPermissions()
        {
            m_MidiBluetoothPlugin.CheckBluetoothPermissions();
        }

        public virtual void GoToBluetoothSettings()
        {
            m_MidiBluetoothPlugin.GoToBluetoothSetting();
        }

        protected virtual void DisconnectMidiDevice()
        {
            m_MidiBluetoothPlugin.DisconnectAllMidiBluetoothDevice();
        }
        #endregion

        #region Midi Connector Facade
        public virtual void ConnectBluetooth(string macAddress)
        {
            m_MidiConnector.ConnectBluetooth(macAddress);
        }

        public virtual void DisconnectBluetooth(string macAddress)
        {
            m_MidiConnector.DisconnectBluetooth(macAddress);
        }

        public virtual void ConnectBluetooth(string macAddress, string deviceNameForOpenPort)
        {
            m_MidiConnector.ConnectBluetooth(macAddress, deviceNameForOpenPort);
        }

        public virtual void DisconnectBluetooth(string macAddress, string deviceNameForClosePort)
        {
            m_MidiConnector.DisconnectBluetooth(macAddress, deviceNameForClosePort);
        }

        public virtual void OpenDeviceToPortByAddress(string macAddress)
        {
            m_MidiConnector.OpenDeviceToPortByAddress(macAddress);
        }

        public virtual void CloseDeviceFromPortByAddress(string macAddress)
        {
            m_MidiConnector.CloseDeviceFromPortByAddress(macAddress);
        }
        #endregion

        public virtual void OpenDeviceToPortByName(string deviceName)
        {
            var bluetoothDevice = GetDeviceBluetoothByName(deviceName);
            if (bluetoothDevice is null) return;

            OpenDeviceToPortByAddress(bluetoothDevice.MacAddress);
        }

        public virtual void CloseDeviceFromPortByName(string deviceName)
        {
            var bluetoothDevice = GetDeviceBluetoothByName(deviceName);
            if (bluetoothDevice is null) return;

            CloseDeviceFromPortByAddress(bluetoothDevice.MacAddress);
        }

        public virtual void TickMidiBluetoothDeviceResult()
        {
            var midiBluetoothDevices = GetMidiBluetoothDevices();

            OnBluetoothDeviceInfoUpdate?.Invoke(midiBluetoothDevices);
        }

        public virtual void BluetoothResultStatusHandler(string status)
        {
            var convertedStatus = bool.Parse(status);
            OnCheckBluetoothCompleted?.Invoke(convertedStatus);
        }

        protected MidiBluetoothDeviceData GetDeviceBluetoothByName(string deviceName)
        {
            return BluetoothDeviceInfoList.Find(device => device.DeviceName == deviceName);
        }

        protected void InvokeOnMidiBytesEvent(byte[] bytes)
        {
            OnMidiBytesEvent?.Invoke(bytes);
        }
    }
}

