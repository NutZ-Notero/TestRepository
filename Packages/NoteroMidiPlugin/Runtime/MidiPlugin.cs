#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR
using Notero.MidiPlugin.Windows;
#endif
using Newtonsoft.Json;
using Notero.MidiPlugin.MidiMessageHandler;
using Notero.MidiPlugin.Sheared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Notero.MidiPlugin
{
    public abstract class MidiDeviceInfo
    {
        public int UniqueID;

        public string DeviceName;

        protected MidiDeviceInfo(int uniqueId, string deviceName)
        {
            this.UniqueID = uniqueId;
            this.DeviceName = deviceName;
        }

        public abstract byte[] GetLEDControlMessage(bool isOn, int keyIndex);

        public abstract byte[] GetVolumeControlMessage(int volumeAsPercent);
    }

    public interface IMidiPlugin
    {
        void Init();

        void DiscoverAllMidiInputPortByDevice();

        void DiscoverMidiInputPortByDeviceName(string name);

        bool IsReady();

        void SendLedControlMessage(bool isOn, int keyIndex, int? id = null);

        void SendVolumeControlMessage(int valueAsPercent, int? id = null);

        bool IsInteractiveAfterMute(string name);

        void AutoManageDevice();

        void SendMidiEvent(byte[] data, int? id = null);

        void OpenMidiInputPort(string deviceName);

        void CloseMidiInputPort(string deviceName);

        void CloseAllMidiDevice();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// MidiPlugin for Android
    /// </summary>
    class MidiPluginNativeBridge : IMidiPlugin
    {
        private const string UnityPlayerClass = "com.unity3d.player.UnityPlayer";
        private const string NativeClass = "co.notero.midiplugin.MidiPlugin";

        private AndroidJavaObject _midiPlugin;
        private bool IsInitialized;

        public void Init()
        {
            if(IsInitialized) return;
            IsInitialized = true;

            _midiPlugin = new AndroidJavaObject(NativeClass, this.GetJavaPluginContext());
        }

        private AndroidJavaObject GetJavaPluginContext()
        {
            var unityPlayer = new AndroidJavaClass(UnityPlayerClass);
            var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var context = currentActivity.Call<AndroidJavaObject>("getApplicationContext");

            return context;
        }

        public void DiscoverAllMidiInputPortByDevice()
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("discoverAllMidiInputPort");
        }

        public void DiscoverMidiInputPortByDeviceName(string name)
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("discoverMidiInputPort", name);
        }

        public bool IsReady()
        {
            if(_midiPlugin == null) return true;

            return _midiPlugin.Call<bool>("IsReady");
        }

        public void SendLedControlMessage(bool isOn, int keyIndex, int? id = null)
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("sendLEDControlMessage", isOn, keyIndex);
        }

        public void SendVolumeControlMessage(int valueAsPercent, int? id = null)
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("sendVolumeControlMessage", valueAsPercent);
        }

        public bool IsInteractiveAfterMute(string name)
        {
            if(_midiPlugin == null) return true;

            return _midiPlugin.Call<bool>("isInteractiveAfterMute", name);
        }

        public void AutoManageDevice()
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("autoManageDevice");
        }

        public void SendMidiEvent(byte[] data, int? id = null)
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("sendMidiEvent", data);
        }

        public void OpenMidiInputPort(string deviceName)
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("discoverMidiInputPort", deviceName);
        }

        public void CloseMidiInputPort(string deviceName)
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("closeMidiInputPort", deviceName);
        }

        public void CloseAllMidiDevice()
        {
            if(_midiPlugin == null) return;

            _midiPlugin.Call("closeAllMidiDevice");
        }
    }
#elif UNITY_IOS && !UNITY_EDITOR
    /// <summary>
    /// MidiPlugin for Unsupported
    /// </summary>
    class MidiPluginNativeBridge : IMidiPlugin
    {
        [DllImport("__Internal")] private static extern void onConneced(Action<string> callback);

        [DllImport("__Internal")] private static extern void onDisconnected(Action<string> callback);

        [DllImport("__Internal")] private static extern void sendMIDIMessage(string jsonData);

        struct MidiMessageObject
        {
            public byte[] data;

            public int? id;
        }

        private static event Action<string> onConnecedEvent;
        private static event Action<string> onDisconnecedEvent;

        private readonly List<MidiDeviceInfo> m_MidiDeviceInfo = new();
        private bool IsInitialized;

        public void Init()
        {
            if(IsInitialized) return;
            IsInitialized = true;

            onConnecedEvent += OnConnected;
            onDisconnecedEvent += OnDisconnected;

            onConneced(OnConnectedStatic);
            onDisconnected(OnDisconnectedStatic);
        }

        public void DiscoverAllMidiInputPortByDevice() { }

        public void DiscoverMidiInputPortByDeviceName(string name) { }

        public bool IsReady()
        {
            return true;
        }

        public void SendLedControlMessage(bool isOn, int keyIndex, int? id = null)
        {
            if(id is not null)
            {
                var deviceList = m_MidiDeviceInfo.Where(item => item.UniqueID == id);

                foreach(var deviceInfo in deviceList)
                {
                    var jsonData = JsonConvert.SerializeObject(new MidiMessageObject{
                        data = deviceInfo.GetLEDControlMessage(isOn, keyIndex), id = id
                    });
                    sendMIDIMessage(jsonData);
                }
                return;
            }

            foreach(var deviceInfo in m_MidiDeviceInfo)
            {
                var jsonData = JsonConvert.SerializeObject(new MidiMessageObject{
                    data = deviceInfo.GetLEDControlMessage(isOn, keyIndex), id = null
                });
                sendMIDIMessage(jsonData);
            }
        }

        public void SendVolumeControlMessage(int valueAsPercent, int? id = null) { }

        public bool IsInteractiveAfterMute(string name)
        {
            return true;
        }

        public void AutoManageDevice() { }

        public void SendMidiEvent(byte[] data, int? id = null)
        {
            var jsonData = JsonConvert.SerializeObject(new MidiMessageObject{
                data = data, id = id
            });

            sendMIDIMessage(jsonData);
        }

        public void OpenMidiInputPort(string deviceName) { }

        public void CloseMidiInputPort(string deviceName) { }

        public void CloseAllMidiDevice() { }

        private void OnConnected(string jsonString)
        {
            m_MidiDeviceInfo.Clear();

            var midiDeviceInfoList = JsonConvert.DeserializeObject<List<MidiBluetoothDeviceData>>(jsonString);

            foreach(var deviceInfo in midiDeviceInfoList)
            {
                if(!int.TryParse(deviceInfo.MacAddress, out var uniqueId)) continue;

                var midiDeviceAdapter = AdapterRegister.AssertDevice(uniqueId, deviceInfo.DeviceName);
                if(midiDeviceAdapter is null) continue;

                m_MidiDeviceInfo.Add(midiDeviceAdapter);
            }
        }

        private void OnDisconnected(string jsonString)
        {
            m_MidiDeviceInfo.Clear();

            var midiDeviceInfoList = JsonConvert.DeserializeObject<List<MidiBluetoothDeviceData>>(jsonString);

            foreach(var deviceInfo in midiDeviceInfoList)
            {
                if(!int.TryParse(deviceInfo.MacAddress, out var uniqueId)) continue;

                var midiDeviceAdapter = AdapterRegister.AssertDevice(uniqueId, deviceInfo.DeviceName);
                if(midiDeviceAdapter is null) continue;

                m_MidiDeviceInfo.Add(midiDeviceAdapter);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Action<string>))]
        private static void OnConnectedStatic(string deviceName)
        {
            onConnecedEvent?.Invoke(deviceName);
        }

        [AOT.MonoPInvokeCallback(typeof(Action<string>))]
        private static void OnDisconnectedStatic(string deviceName)
        {
            onDisconnecedEvent?.Invoke(deviceName);
        }
    }
#else
    /// <summary>
    /// MidiPlugin for Unsupported
    /// </summary>
    class MidiPluginNativeBridge : IMidiPlugin
    {
        private MidiOutWindowsPlugin m_MidiOutWindowsPlugin;
        private bool IsInitialized;

        public void Init()
        {
            if(IsInitialized) return;
            IsInitialized = true;

            m_MidiOutWindowsPlugin = new();
        }

        public void DiscoverAllMidiInputPortByDevice() { }

        public void DiscoverMidiInputPortByDeviceName(string name) { }

        public bool IsReady()
        {
            return true;
        }

        public void SendLedControlMessage(bool isOn, int keyIndex, int? id = null)
        {
            if(m_MidiOutWindowsPlugin == null) return;

            m_MidiOutWindowsPlugin.SendLEDControlMessage(isOn, keyIndex);
        }

        public void SendVolumeControlMessage(int valueAsPercent, int? id = null)
        {
            if(m_MidiOutWindowsPlugin == null) return;

            m_MidiOutWindowsPlugin.SendVolumeControlMessage(valueAsPercent);
        }

        public bool IsInteractiveAfterMute(string name)
        {
            return true;
        }

        public void AutoManageDevice()
        {
            if(m_MidiOutWindowsPlugin == null) return;

            m_MidiOutWindowsPlugin.AutoManageDevice();
        }

        public void SendMidiEvent(byte[] data, int? id = null)
        {
            if(m_MidiOutWindowsPlugin == null) return;

            m_MidiOutWindowsPlugin.SendMidiEvent(data);
        }

        public void OpenMidiInputPort(string deviceName) { }

        public void CloseMidiInputPort(string deviceName) { }

        public void CloseAllMidiDevice()
        {
            if(m_MidiOutWindowsPlugin == null) return;

            m_MidiOutWindowsPlugin.CloseAllMidiDevice();
        }
    }
#endif

    public abstract class MidiPlugin
    {
        public static IMidiPlugin Instance => m_Instance ??= new MidiPluginNativeBridge();
        private static IMidiPlugin m_Instance;
    }
}
