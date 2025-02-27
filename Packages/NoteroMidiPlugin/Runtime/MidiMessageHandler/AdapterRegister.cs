using System.Collections.Generic;
using System.Linq;

namespace Notero.MidiPlugin.MidiMessageHandler
{
    public delegate MidiDeviceInfo DeviceAssertion(int uniqueId, string deviceName);

    public static class AdapterRegister
    {
        private static List<DeviceAssertion> m_Assertors = new List<DeviceAssertion>(){
            GeneralMidiDeviceAdapter.DeviceAssertion,
            MasterROMidiDeviceAdapter.DeviceAssertion,
            StarlightMidiDeviceAdapter.DeviceAssertion,
            GZUTPianoMidiDeviceAdapter.DeviceAssertion,
            PopPianoMidiDeviceAdapter.DeviceAssertion,
            DreamSASMidiDeviceAdapter.DeviceAssertion,
            RGBPianoMidiDeviceAdapter.DeviceAssertion,
            GenericPianoMidiDeviceAdapter.DeviceAssertion
        };

        public static MidiDeviceInfo AssertDevice(int uniqueId, string deviceName)
        {
            return m_Assertors.Select(assertor => assertor(uniqueId, deviceName)).FirstOrDefault(device => device != null);
        }
    }
}
