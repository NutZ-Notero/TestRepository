#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
using UnityEngine.Android;
#endif

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Notero.MidiPlugin.Sheared;
using UnityEngine;
using UnityEngine.Android;


namespace Notero.MidiPlugin
{
    public delegate void NoteEventHandler(int aMidiId, int aValue, int aChannel);

    public delegate void MidiBytesEvent(byte[] bytes);

    public delegate void MidiReceivedCallback(IntPtr data, int length);

    public interface IMidiBluetoothPlugin
    {
        event Action<bool> OnBluetoothStatusResult;

        event Action OnBluetoothPermissionResult;

        event Action<string> OnDeviceConnected;

        event Action<string> OnDeviceDisconnected;

        event Action OnDeviceStatusChange;

        event Action OnTickMidiBluetoothDeviceResult;

        event Action OnScanCompleted;

        event NoteEventHandler NoteOnEvent;

        event NoteEventHandler NoteOffEvent;

        event MidiBytesEvent MidiBytesEvent;

        void Init();

        void ScanMidiBluetooth();

        void StopScanMidiBluetooth();

        IEnumerable<MidiBluetoothDeviceData> GetMidiBluetoothDevices();

        void ConnectBluetooth(string macAddress);

        void DisconnectBluetooth(string macAddress);

        IEnumerable<MidiBluetoothDeviceData> GetMidiDevices();

        IEnumerable<MidiBluetoothDeviceData> GetConnectedMidiDevices();

        void OpenDeviceToPort(string macAddress);

        void CloseDeviceFromPort(string macAddress);

        void DisconnectAllMidiBluetoothDevice();

        void CheckIsBluetoothEnabled();

        void ShowBluetoothMIDIDevices();

        void GoToBluetoothSetting();

        void CheckBluetoothPermissions();
    }

    public class MidiBluetoothDeviceDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(MidiBluetoothDeviceData));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var connectionType = Enum.TryParse<ConnectionType>(jsonObject.Value<string>("Type"), out var announcementType) ? announcementType : ConnectionType.UNKNOW;

            return new MidiBluetoothDeviceData(
                jsonObject.Value<string>("macAddress"),
                jsonObject.Value<string>("deviceName"),
                jsonObject.Value<bool>("isConnected"),
                connectionType,
                jsonObject.Value<int>("sourceId"),
                jsonObject.Value<int>("destinationId")
            );
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) { }
    }

    [JsonConverter(typeof(MidiBluetoothDeviceDataConverter))]
    public class MidiBluetoothDeviceData
    {
        public string MacAddress { get; }

        public string DeviceName { get; }

        public bool IsConnected { get; set; }

        public ConnectionType ConnectionType { get; }

        public int SourceId { get; set; }

        public int DestinationId { get; set; }

        public MidiBluetoothDeviceData(string macAddress, string deviceName, bool isConnected, ConnectionType connectionType, int sourceId, int destinationId)
        {
            MacAddress = macAddress ?? throw new ArgumentNullException(nameof(macAddress));
            DeviceName = deviceName;
            IsConnected = isConnected;
            ConnectionType = connectionType;
            SourceId = sourceId;
            DestinationId = destinationId;
        }

        public override bool Equals(object obj)
        {
            if(obj is MidiBluetoothDeviceData other)
            {
                return this.MacAddress == other.MacAddress && this.DeviceName == other.DeviceName;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return $"{MacAddress}-{DeviceName}".GetHashCode();
        }
    }

    public class MidiBluetoothEventDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(MidiBluetoothEventData));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var data = jsonObject.Value<string>("midiMessage").Split(",");
            var byteData = data.Select(byteString => byte.Parse(byteString)).ToArray();
            return new MidiBluetoothEventData(
                byteData,
                jsonObject.Value<int>("midiNoteId"),
                jsonObject.Value<int>("velocity"),
                jsonObject.Value<int>("channel")
            );
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) { }
    }

    [JsonConverter(typeof(MidiBluetoothEventDataConverter))]
    public class MidiBluetoothEventData
    {
        public byte[] Data { get; }

        public int MidiNoteId { get; }

        public int Velocity { get; }

        public int Channel { get; set; }

        public MidiBluetoothEventData(byte[] data, int midiNoteId, int velocity, int channel)
        {
            Data = data;
            MidiNoteId = midiNoteId;
            Velocity = velocity;
            Channel = channel;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    public class MidiBluetoothPluginNativeBridge : IMidiBluetoothPlugin
    {
        private const string UnityPlayerClass = "com.unity3d.player.UnityPlayer";
        private const string NativeClass = "co.notero.midiplugin.MidiBluetoothPlugin";

        private AndroidJavaObject m_MidiBluetoothPlugin;

        private Action<List<MidiBluetoothDeviceData>> m_OnScanMidiBluetoothFinish;
        private Action m_OnScanMidiBluetoothFail;
        private bool IsInitialized;

        public void Init()
        {
            if(IsInitialized) return;
            IsInitialized = true;

            m_MidiBluetoothPlugin = new AndroidJavaObject(NativeClass, this.GetJavaPluginActivity());
        }

        private AndroidJavaObject GetJavaPluginActivity()
        {
            var unityPlayer = new AndroidJavaClass(UnityPlayerClass);
            var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var context = currentActivity.Call<AndroidJavaObject>("getApplicationContext");

            return context;
        }

        public event Action<bool> OnBluetoothStatusResult;
        public event Action OnBluetoothPermissionResult;
        public event Action<string> OnDeviceConnected;
        public event Action<string> OnDeviceDisconnected;
        public event Action OnDeviceStatusChange;
        public event Action OnTickMidiBluetoothDeviceResult;
        public event Action OnScanCompleted;
        public event NoteEventHandler NoteOnEvent;
        public event NoteEventHandler NoteOffEvent;
        public event MidiBytesEvent MidiBytesEvent;

        public void ScanMidiBluetooth()
        {
            if(m_MidiBluetoothPlugin == null) return;
            m_MidiBluetoothPlugin.Call("scanMidiBluetooth");
        }

        public void StopScanMidiBluetooth()
        {
            if(m_MidiBluetoothPlugin == null) return;

            m_MidiBluetoothPlugin.Call("stopScanMidiBluetooth");
        }

        public IEnumerable<MidiBluetoothDeviceData> GetMidiBluetoothDevices()
        {
            if(m_MidiBluetoothPlugin == null) return new List<MidiBluetoothDeviceData>();

            var midiBluetoothDeviceJsonString = m_MidiBluetoothPlugin.Call<string>("getBluetoothDeviceJsonString");

            return JsonConvert.DeserializeObject<List<MidiBluetoothDeviceData>>(midiBluetoothDeviceJsonString);
        }

        public void ConnectBluetooth(string macAddress)
        {
            m_MidiBluetoothPlugin.Call("connectBluetooth", macAddress);
        }

        public void DisconnectBluetooth(string macAddress)
        {
            m_MidiBluetoothPlugin.Call("disconnectBluetooth", macAddress);
        }

        public IEnumerable<MidiBluetoothDeviceData> GetMidiDevices()
        {
            if(m_MidiBluetoothPlugin == null) return new List<MidiBluetoothDeviceData>();

            var midiBluetoothDeviceJsonString = m_MidiBluetoothPlugin.Call<string>("getMidiDeviceJsonString");
            var midiBluetoothList = JsonConvert.DeserializeObject<List<MidiBluetoothDeviceData>>(midiBluetoothDeviceJsonString);

            return midiBluetoothList.Where(bt => !bt.IsConnected);
        }

        public IEnumerable<MidiBluetoothDeviceData> GetConnectedMidiDevices()
        {
            if(m_MidiBluetoothPlugin == null) return new List<MidiBluetoothDeviceData>();

            var midiBluetoothDeviceJsonString = m_MidiBluetoothPlugin.Call<string>("getMidiDeviceJsonString");
            var midiBluetoothList = JsonConvert.DeserializeObject<List<MidiBluetoothDeviceData>>(midiBluetoothDeviceJsonString);

            return midiBluetoothList.Where(bt => bt.IsConnected);
        }

        public void OpenDeviceToPort(string macAddress)
        {
            m_MidiBluetoothPlugin.Call("openDeviceToPort", macAddress);
        }

        public void CloseDeviceFromPort(string macAddress)
        {
            m_MidiBluetoothPlugin.Call("closeDeviceFromPort", macAddress);
        }

        public void DisconnectAllMidiBluetoothDevice() { }

        public void CheckIsBluetoothEnabled()
        {
            m_MidiBluetoothPlugin.Call("checkIsBluetoothEnabled");
        }

        public void ShowBluetoothMIDIDevices() { }

        public void GoToBluetoothSetting()
        {
            var unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var currentActivityObject = unityClass.GetStatic<AndroidJavaObject>("currentActivity");

            using var intentObject = new AndroidJavaObject("android.content.Intent", "android.settings.BLUETOOTH_SETTINGS");

            if(currentActivityObject != null)
            {
                currentActivityObject.Call("startActivity", intentObject);
            }
            else
            {
                Debug.LogError("Failed to open device settings");
            }
        }

        private void RequestMultiplePermission(Dictionary<string /*permission key*/, bool /*is permission granted*/> permissionStatus)
        {
            var permissionCallback = new PermissionCallbacks();

            void onPermissionGranted(string permission)
            {
                if(permissionStatus.Values.All(status => status == true))
                {
                    permissionCallback.PermissionGranted -= onPermissionGranted;
                    permissionCallback.PermissionDenied -= onPermissionDenied;
                    permissionCallback.PermissionDeniedAndDontAskAgain -= onPermissionDeniedAndDontAskAgain;

                    OnBluetoothPermissionResult?.Invoke();
                }
                else
                {
                    CheckBluetoothPermissions();
                }
            }

            void onPermissionDenied(string permission)
            {
                Debug.LogWarning($"Bluetooth permissions isn't granted.");
            }

            void onPermissionDeniedAndDontAskAgain(string permission)
            {
                Debug.LogWarning($"Bluetooth permissions isn't granted and don't ask again.");
            }

            permissionCallback.PermissionGranted += onPermissionGranted;
            permissionCallback.PermissionDenied += onPermissionDenied;
            permissionCallback.PermissionDeniedAndDontAskAgain += onPermissionDeniedAndDontAskAgain;
            var permissions = new List<string>();
            foreach(var info in permissionStatus)
            {
                if(!info.Value)
                {
                    permissions.Add(info.Key);
                }
            }

            Permission.RequestUserPermissions(permissions.ToArray(), permissionCallback);
        }

        public void CheckBluetoothPermissions()
        {
            var version = new AndroidJavaClass("android.os.Build$VERSION");
            var androidVersion = version.GetStatic<int>("SDK_INT");
            var currentPermissionCallback = new PermissionCallbacks();
            var permissionList = new Dictionary<string /*permission*/, bool /*isGranted*/>();

            var bluetoothAdminPermissionKey = "android.permission.BLUETOOTH_ADMIN";
            var bluetoothScanPermissionKey = "android.permission.BLUETOOTH_SCAN";
            var bluetoothAdvertisePermissionKey = "android.permission.BLUETOOTH_ADVERTISE";
            var bluetoothConnectPermissionKey = "android.permission.BLUETOOTH_CONNECT";
            var bluetoothPermissionKey = "android.permission.BLUETOOTH";
            var bluetoothAccessFineLocationPermissionKey = "android.permission.ACCESS_FINE_LOCATION";
            var bluetoothAccessBackgroundLocationPermissionKey = "android.permission.ACCESS_BACKGROUND_LOCATION";

            if(androidVersion > 30)
            {
                if(
                    !Permission.HasUserAuthorizedPermission(bluetoothScanPermissionKey) ||
                    !Permission.HasUserAuthorizedPermission(bluetoothAdvertisePermissionKey) ||
                    !Permission.HasUserAuthorizedPermission(bluetoothConnectPermissionKey) ||
                    !Permission.HasUserAuthorizedPermission(bluetoothAdminPermissionKey)
                )
                {
                    permissionList = new Dictionary<string, bool>
                    {
                        {
                            bluetoothScanPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothScanPermissionKey)
                        },
                        {
                            bluetoothAdvertisePermissionKey, Permission.HasUserAuthorizedPermission(bluetoothAdvertisePermissionKey)
                        },
                        {
                            bluetoothConnectPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothConnectPermissionKey)
                        },
                        {
                            bluetoothAdminPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothAdminPermissionKey)
                        },
                    };

                    RequestMultiplePermission(permissionList);
                }
                else
                {
                    OnBluetoothPermissionResult?.Invoke();
                }
            }
            else if(androidVersion is < 30 and > 24 and not 30 and not 29)
            {
                if(
                    !Permission.HasUserAuthorizedPermission(bluetoothPermissionKey) ||
                    !Permission.HasUserAuthorizedPermission(bluetoothAccessFineLocationPermissionKey) ||
                    !Permission.HasUserAuthorizedPermission(bluetoothAdminPermissionKey)
                )
                {
                    permissionList = new Dictionary<string, bool>
                    {
                        {
                            bluetoothPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothPermissionKey)
                        },
                        {
                            bluetoothAccessFineLocationPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothAccessFineLocationPermissionKey)
                        },
                        {
                            bluetoothAdminPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothAdminPermissionKey)
                        },
                    };

                    RequestMultiplePermission(permissionList);
                }
                else
                {
                    OnBluetoothPermissionResult?.Invoke();
                }
            }
            else if(androidVersion is 30 or 29)
            {
                if(
                    !Permission.HasUserAuthorizedPermission(bluetoothPermissionKey) ||
                    !Permission.HasUserAuthorizedPermission(bluetoothAdminPermissionKey) ||
                    !Permission.HasUserAuthorizedPermission(bluetoothAccessFineLocationPermissionKey) ||
                    !Permission.HasUserAuthorizedPermission(bluetoothAccessBackgroundLocationPermissionKey)
                )
                {
                    permissionList = new Dictionary<string, bool>
                    {
                        {
                            bluetoothPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothPermissionKey)
                        },
                        {
                            bluetoothAdminPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothAdminPermissionKey)
                        },
                        {
                            bluetoothAccessFineLocationPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothAccessFineLocationPermissionKey)
                        },
                        {
                            bluetoothAccessBackgroundLocationPermissionKey, Permission.HasUserAuthorizedPermission(bluetoothAccessBackgroundLocationPermissionKey)
                        },
                    };

                    RequestMultiplePermission(permissionList);
                }
                else
                {
                    OnBluetoothPermissionResult?.Invoke();
                }
            }
        }
    }
#elif UNITY_IOS && !UNITY_EDITOR
    public class MidiBluetoothPluginNativeBridge : IMidiBluetoothPlugin
    {
        [DllImport("__Internal")] private static extern bool showBluetoothMIDIDevices();

        [DllImport("__Internal")] private static extern bool disconnectAllMidiBluetoothDevice();

        [DllImport("__Internal")] private static extern bool isEnableBluetooth();

        [DllImport("__Internal")] private static extern void goToBluetoothSetting();

        [DllImport("__Internal")] private static extern void connectToDevice(string macAdress);

        [DllImport("__Internal")] private static extern void disconnectFromDevice(string macAdress);

        [DllImport("__Internal")] private static extern void onConneced(Action<string> callback);

        [DllImport("__Internal")] private static extern void onDisconnected(Action<string> callback);

        [DllImport("__Internal")] private static extern void onTickMidiBluetoothResult(Action<string> callback);

        [DllImport("__Internal")] private static extern void onMidiReceived(MidiReceivedCallback callback);

        [DllImport("__Internal")] private static extern void onDeviceStatusChangeReceived(Action callback);

        [DllImport("__Internal")] private static extern IntPtr getMidiDevices();

        [DllImport("__Internal")] private static extern void freePointer(IntPtr ptr);

        [DllImport("__Internal")] private static extern void printLog(string log);

        public event Action<bool> OnBluetoothStatusResult;
        public event Action OnBluetoothPermissionResult;
        public event Action<string> OnDeviceConnected;
        public event Action<string> OnDeviceDisconnected;
        public event Action OnDeviceStatusChange;
        public event Action OnTickMidiBluetoothDeviceResult;
        public event Action OnScanCompleted;
        public event NoteEventHandler NoteOnEvent;
        public event NoteEventHandler NoteOffEvent;
        public event MidiBytesEvent MidiBytesEvent;

        private static event Action<string> onConnecedEvent;
        private static event Action<string> onDisconnecedEvent;
        private static event Action OnDeviceStatusChangeEvent;
        private static event Action<string> OnTickMidiBluetoothResultEvent;
        private static event Action<byte[]> OnMidiReceivedEvent;

        private Thread m_ScanThread;
        private JsonSerializerSettings m_JsonSerializerSettings;
        private bool IsInitialized;

        public void Init()
        {
            m_JsonSerializerSettings = new JsonSerializerSettings();
            m_JsonSerializerSettings.Converters.Add(new MidiBluetoothDeviceDataConverter());

            if(IsInitialized) return;
            IsInitialized = true;

            onConnecedEvent += OnConnected;
            onDisconnecedEvent += OnDisconnected;
            OnTickMidiBluetoothResultEvent += OnTickMidiBluetoothResult;
            OnMidiReceivedEvent += OnMidiReceivedFunction;
            OnDeviceStatusChangeEvent += OnDeviceStatusChangeReceived;

            onConneced(OnConnectedStatic);
            onDisconnected(OnDisconnectedStatic);
            onTickMidiBluetoothResult(OnTickMidiBluetoothResultStatic);
            onMidiReceived(OnMidiReceivedStatic);
            onDeviceStatusChangeReceived(OnDeviceStatusChangeReceivedStatic);
        }

        public void ScanMidiBluetooth()
        {
            OnScanCompleted?.Invoke();
        }

        public void StopScanMidiBluetooth() { }

        public IEnumerable<MidiBluetoothDeviceData> GetMidiBluetoothDevices()
        {
            return new List<MidiBluetoothDeviceData>();
        }

        public void ConnectBluetooth(string macAddress) { }

        public void DisconnectBluetooth(string macAddress) { }

        public IEnumerable<MidiBluetoothDeviceData> GetMidiDevices()
        {
            var ptr = getMidiDevices();
            var midiDevices = Marshal.PtrToStringAnsi(ptr);

            freePointer(ptr);

            if(string.IsNullOrEmpty(midiDevices)) return new List<MidiBluetoothDeviceData>();

            var deviceInfoList = JsonConvert.DeserializeObject<List<MidiBluetoothDeviceData>>(midiDevices, m_JsonSerializerSettings);

            return deviceInfoList.Where(item => !item.IsConnected);
        }

        public IEnumerable<MidiBluetoothDeviceData> GetConnectedMidiDevices()
        {
            var ptr = getMidiDevices();
            var midiDevices = Marshal.PtrToStringAnsi(ptr);

            freePointer(ptr);

            if(string.IsNullOrEmpty(midiDevices)) return new List<MidiBluetoothDeviceData>();

            var deviceInfoList = JsonConvert.DeserializeObject<List<MidiBluetoothDeviceData>>(midiDevices, m_JsonSerializerSettings);

            return deviceInfoList.Where(item => item.IsConnected);
        }

        public void OpenDeviceToPort(string macAddress)
        {
            connectToDevice(macAddress);
        }

        public void CloseDeviceFromPort(string macAddress)
        {
            disconnectFromDevice(macAddress);
        }

        public void DisconnectAllMidiBluetoothDevice()
        {
            disconnectAllMidiBluetoothDevice();
        }

        public void CheckIsBluetoothEnabled()
        {
            var isEnable = isEnableBluetooth();

            OnBluetoothStatusResult?.Invoke(isEnable);
        }

        public void ShowBluetoothMIDIDevices()
        {
            showBluetoothMIDIDevices();
        }

        public void GoToBluetoothSetting()
        {
            goToBluetoothSetting();
        }

        private void OnConnected(string deviceName)
        {
            OnDeviceConnected?.Invoke(deviceName);
        }

        private void OnDisconnected(string deviceName)
        {
            OnDeviceDisconnected?.Invoke(deviceName);
        }

        private void OnTickMidiBluetoothResult(string deviceInfo)
        {
            OnTickMidiBluetoothDeviceResult?.Invoke();
        }

        private void OnMidiReceivedFunction(byte[] midiData)
        {
            if(midiData.Length <= 0) return;

            byte statusByte = midiData[0];
            byte channel = (byte)(statusByte & 0x0F);

            switch(statusByte & 0xF0)
            {
                case 0x90:
                {
                    byte note = midiData[1];
                    byte velocity = midiData[2];
                    NoteOnEvent?.Invoke(note, velocity, channel);
                    break;
                }
                case 0x80:
                {
                    byte note = midiData[1];
                    byte velocity = midiData[2];
                    NoteOffEvent?.Invoke(note, velocity, channel);
                    break;
                }
            }

            MidiBytesEvent?.Invoke(midiData);
        }

        private void OnDeviceStatusChangeReceived()
        {
            OnDeviceStatusChange?.Invoke();
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

        [AOT.MonoPInvokeCallback(typeof(Action))]
        private static void OnDeviceStatusChangeReceivedStatic()
        {
            OnDeviceStatusChangeEvent?.Invoke();
        }

        [AOT.MonoPInvokeCallback(typeof(Action<string>))]
        private static void OnTickMidiBluetoothResultStatic(string deviceInfo)
        {
            OnTickMidiBluetoothResultEvent?.Invoke(deviceInfo);
        }

        [AOT.MonoPInvokeCallback(typeof(MidiReceivedCallback))]
        private static void OnMidiReceivedStatic(IntPtr data, int length)
        {
            byte[] midiData = new byte[length];
            Marshal.Copy(data, midiData, 0, length);

            OnMidiReceivedEvent?.Invoke(midiData);
        }

        public void CheckBluetoothPermissions()
        {
            OnBluetoothPermissionResult?.Invoke();
        }
    }
#else
    public class MidiBluetoothPluginNativeBridge : IMidiBluetoothPlugin
    {
        public event Action<bool> OnBluetoothStatusResult;
        public event Action OnBluetoothPermissionResult;
        public event Action<string> OnDeviceConnected;
        public event Action<string> OnDeviceDisconnected;
        public event Action OnDeviceStatusChange;
        public event Action OnTickMidiBluetoothDeviceResult;
        public event Action OnScanCompleted;
        public event NoteEventHandler NoteOnEvent;
        public event NoteEventHandler NoteOffEvent;
        public event MidiBytesEvent MidiBytesEvent;

        private bool IsInitialized;

        public void Init()
        {
            if(IsInitialized) return;
            IsInitialized = true;
        }

        public void ScanMidiBluetooth() { }

        public void StopScanMidiBluetooth() { }

        public IEnumerable<MidiBluetoothDeviceData> GetMidiBluetoothDevices()
        {
            return new List<MidiBluetoothDeviceData>();
        }

        public void ConnectBluetooth(string macAddress) { }

        public void DisconnectBluetooth(string macAddress) { }

        public IEnumerable<MidiBluetoothDeviceData> GetMidiDevices()
        {
            return new List<MidiBluetoothDeviceData>();
        }

        public IEnumerable<MidiBluetoothDeviceData> GetConnectedMidiDevices()
        {
            return new List<MidiBluetoothDeviceData>();
        }

        public void OpenDeviceToPort(string macAddress) { }

        public void CloseDeviceFromPort(string macAddress) { }

        public void DisconnectAllMidiBluetoothDevice() { }

        public void CheckIsBluetoothEnabled() { }

        public void ShowBluetoothMIDIDevices() { }

        public void GoToBluetoothSetting() { }

        public void CheckBluetoothPermissions() { }
    }
#endif

    public abstract class MidiBluetoothPlugin
    {
        public static IMidiBluetoothPlugin Instance => m_Instance ??= new MidiBluetoothPluginNativeBridge();
        private static IMidiBluetoothPlugin m_Instance;
    }
}
