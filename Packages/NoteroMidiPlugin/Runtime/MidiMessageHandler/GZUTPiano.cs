namespace Notero.MidiPlugin.MidiMessageHandler
{
    public class GZUTPianoMidiDeviceAdapter : MidiDeviceInfo
    {
        private GZUTPianoMidiDeviceAdapter(int uniqueId, string deviceName) : base(uniqueId, deviceName)
        {
            this.UniqueID = uniqueId;
            this.DeviceName = deviceName;
        }

        public static DeviceAssertion DeviceAssertion => Assert;

        private static MidiDeviceInfo Assert(int uniqueId, string deviceName)
        {
            return deviceName.ToLower().StartsWith("gzu-tek")
                ? new GZUTPianoMidiDeviceAdapter(uniqueId, deviceName)
                : null;
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
            var buffer = new byte[5];
            var asDecimal = (16 / 100f * volumeAsPercent) * 7.5625f;

            buffer[0] = 0xF0;
            buffer[1] = 0xAF;
            buffer[2] = 0x70;
            buffer[2] = (byte)asDecimal;
            buffer[2] = 0xF7;

            return buffer;
        }
    }
}
