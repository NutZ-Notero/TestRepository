using Newtonsoft.Json;

namespace Notero.MidiPluginConnection
{
    public class BluetoothConnection
    {
        [JsonProperty]
        public string Address;

        [JsonProperty]
        public string MidiDeviceId;

        public override string ToString()
        {
            return $"{Address}-{MidiDeviceId}";
        }
    }
}
