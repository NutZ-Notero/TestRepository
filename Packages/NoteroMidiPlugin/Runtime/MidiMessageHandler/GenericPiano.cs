using System;
namespace Notero.MidiPlugin.MidiMessageHandler
{
    public class GenericPianoMidiDeviceAdapter : MidiDeviceInfo
    {
        private GenericPianoMidiDeviceAdapter(int uniqueId, string deviceName) : base(uniqueId, deviceName)
        {
            this.UniqueID = uniqueId;
            this.DeviceName = deviceName;
        }

        public static DeviceAssertion DeviceAssertion => Assert;

        private static MidiDeviceInfo Assert(int uniqueId, string deviceName)
        {
            return new GenericPianoMidiDeviceAdapter(uniqueId, deviceName);
        }

        public override byte[] GetLEDControlMessage(bool isOn, int keyIndex)
        {
            var stateByte = isOn ? 1 : 0;
            var buffer = new byte[8];

            buffer[0] = 0xF0;
            buffer[1] = 0x4D;
            buffer[2] = 0x4C;
            buffer[3] = 0x4E;
            buffer[4] = 0x45;
            buffer[5] = (byte)keyIndex;
            buffer[6] = (byte)stateByte;
            buffer[7] = 0xF7;

            return buffer;
        }

        public override byte[] GetVolumeControlMessage(int volumeAsPercent)
        {
            return Array.Empty<byte>();
        }
    }
}
