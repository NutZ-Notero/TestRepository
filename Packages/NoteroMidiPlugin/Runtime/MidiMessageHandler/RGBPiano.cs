namespace Notero.MidiPlugin.MidiMessageHandler
{
    public class RGBPianoMidiDeviceAdapter : MidiDeviceInfo
    {
        private RGBPianoMidiDeviceAdapter(int uniqueId, string deviceName) : base(uniqueId, deviceName)
        {
            this.UniqueID = uniqueId;
            this.DeviceName = deviceName;
        }

        public static DeviceAssertion DeviceAssertion => Assert;

        private static MidiDeviceInfo Assert(int uniqueId, string deviceName)
        {
            return deviceName.ToLower().StartsWith("piano midi device") || deviceName.ToLower().StartsWith("piano midi-0001")
                ? new RGBPianoMidiDeviceAdapter(uniqueId, deviceName)
                : null;
        }

        public override byte[] GetLEDControlMessage(bool isOn, int keyIndex)
        {
            var stateByte = isOn ? 0x7F : 0x00;
            var buffer = new byte[8];

            buffer[0] = 0xF0;
            buffer[1] = 0x19;
            buffer[2] = (byte)keyIndex;
            buffer[3] = 0x00;
            buffer[4] = (byte)stateByte;
            buffer[5] = 0x00;
            buffer[6] = 0x00;
            buffer[7] = 0xF7;

            return buffer;
        }

        public override byte[] GetVolumeControlMessage(int volumeAsPercent)
        {
            var buffer = new byte[3];
            var asDecimal = volumeAsPercent != 0 ? 0x01 : 0x00;

            buffer[0] = 0xFB;
            buffer[1] = 0x7A;
            buffer[2] = (byte)asDecimal;

            return buffer;
        }
    }
}
