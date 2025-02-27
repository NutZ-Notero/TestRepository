package co.notero.midiplugin

import android.annotation.SuppressLint
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothManager
import android.content.Context
import android.media.midi.MidiDevice
import android.media.midi.MidiDeviceInfo
import android.media.midi.MidiDeviceInfo.PortInfo
import android.media.midi.MidiDeviceStatus
import android.media.midi.MidiManager
import android.media.midi.MidiOutputPort
import android.media.midi.MidiReceiver
import android.os.Handler
import android.util.Log
import co.notero.midiplugin.bluetoothScanner.BluetoothScanManager
import co.notero.midiplugin.bluetoothScanner.model.BluetoothScanCallback
import co.notero.midiplugin.bluetoothScanner.model.MidiDeviceObject
import co.notero.midiplugin.bluetoothScanner.model.MidiDeviceObject.Companion.toJsonString
import co.notero.midiplugin.bluetoothScanner.model.MidiEventInfo
import co.notero.midiplugin.bluetoothScanner.model.MidiEventInfo.Companion.toJsonString
import com.unity3d.player.UnityPlayer
import org.json.JSONObject
import java.io.IOException

const val TAG_BLUETOOTH_DEVICE_PLUGIB = "NoteroMIDIBTPlugin"

interface OnScanBluetoothListener {
    fun onScaneCompleted()
}

class MidiReceiverHandler : MidiReceiver() {
    @Throws(IOException::class)
    override fun onSend(data: ByteArray, offset: Int, count: Int, timestamp: Long) {
        val statusByte = data[0].toInt() and 0xFF
        val command = data[1].toInt() and 0xFF
        val note = data[2].toInt() and 0xFF
        val velocity = data[3].toInt() and 0xFF
        val channel = statusByte and 0x0F
        val midiMessage = listOf(command, note, velocity, channel).joinToString(",")
        var info = MidiEventInfo(midiMessage, note, velocity, channel)

        UnityPlayer.UnitySendMessage("MidiBluetoothAdapter","OnMidiBytesEventHandler",info.toJsonString())

        if (command in 0x90..0x9F) {
            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "onSend: noteon $note")
            UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "NoteOnReceived", info.toJsonString())
        } else if (command in 0x80..0x8F) {
            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "onSend: noteoff $note")
            UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "NoteOffReceived", info.toJsonString())
        }
    }
}

@SuppressLint("MissingPermission")
class NoteroBluetoothDevice(private val bluetoothDevice: BluetoothDevice) {
    public var isActive: Boolean = true
    public var isConnected: Boolean = false

    fun getBluetoothDeviceName(): String {
        return this.bluetoothDevice.name
    }

    fun getBluetoothDevice(): BluetoothDevice {
        return this.bluetoothDevice
    }

    fun setIsActive(isActive: Boolean) {
        this.isActive = isActive
    }

    fun setIsConnected(isConnected: Boolean) {
        this.isConnected = isConnected
    }
}

class NoteroMidiDevicePort(private val outputPort: MidiOutputPort, private val midiDevice: MidiDevice) {
    private val midiReceiver: MidiReceiver = MidiReceiverHandler()

    fun open() {
        outputPort.connect(this.midiReceiver)
    }

    fun close() {
        outputPort.disconnect(this.midiReceiver)
        outputPort.close()

        midiDevice.close()
    }

    fun getId(): String {
        return midiDevice.info.id.toString()
    }
}

@SuppressLint("MissingPermission")
class MidiBluetoothPlugin(private val context: Context) {
    private var bluetoothScanManager: BluetoothScanManager
    private var btManager: BluetoothManager = context.getSystemService(Context.BLUETOOTH_SERVICE) as BluetoothManager

    private var bluetoothScanning: MutableList<String> = arrayListOf()
    private var bluetoothDeviceList: MutableList<Pair<String, NoteroBluetoothDevice>> = arrayListOf()
    private var midiDeviceList: MutableList<Pair<String, MidiDevice>> = arrayListOf()

    private var noteroMidiDevicePorts: MutableList<NoteroMidiDevicePort> = arrayListOf()
    private val onScanBluetoothListeners: MutableList<OnScanBluetoothListener> = arrayListOf()

    private val deviceCallback = object : MidiManager.DeviceCallback() {
        override fun onDeviceAdded(device: MidiDeviceInfo?) {
            super.onDeviceAdded(device)
            if (device == null) return

            UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "UpdateDeviceStatus", "")

            val deviceName = device.properties.getString(MidiDeviceInfo.PROPERTY_NAME)
            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "MEL-Device added $deviceName")
        }

        override fun onDeviceRemoved(device: MidiDeviceInfo?) {
            super.onDeviceRemoved(device)
            if (device == null) return

            closeDeviceFromPort(device.id.toString())

            device.properties.getParcelable<BluetoothDevice>(MidiDeviceInfo.PROPERTY_BLUETOOTH_DEVICE)?.let {
                midiDeviceList.removeIf { midiDevice -> midiDevice.first == it.address }
                bluetoothDeviceList.find { bluetoothDevice -> bluetoothDevice.first == it.address }?.let {
                    it.second.setIsActive(false)
                    it.second.setIsConnected(false)
                }
            }

            UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "UpdateDeviceStatus", "")

            val deviceName = device.properties.getString(MidiDeviceInfo.PROPERTY_NAME)
            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "MEL-Device removed $deviceName")
        }

        override fun onDeviceStatusChanged(status: MidiDeviceStatus?) {
            super.onDeviceStatusChanged(status)
            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "onDeviceStatusChanged")
        }
    }

    init {
        val handler = Handler.createAsync(context.mainLooper)
        val midiManager: MidiManager = context.getSystemService(Context.MIDI_SERVICE) as MidiManager

        midiManager.registerDeviceCallback(deviceCallback, handler)

        bluetoothScanManager = BluetoothScanManager(btManager = btManager, scanCallback = BluetoothScanCallback({ scanResult ->
            val bluetoothDevice = scanResult.device
            val name = bluetoothDevice?.name
            val address = bluetoothDevice?.address

            if (name.isNullOrBlank() || address.isNullOrBlank()) return@BluetoothScanCallback

            if (bluetoothScanning.any { it == address }) return@BluetoothScanCallback
            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "scanCallback: $name [$address]")

            bluetoothScanning.add(address)
            bluetoothDeviceList.add(address to NoteroBluetoothDevice(bluetoothDevice))
            UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "TickMidiBluetoothDeviceResult", "")
        }))

        bluetoothScanManager.beforeScanActions.add {
            bluetoothScanning.clear()
        }

        bluetoothScanManager.afterScanActions.add {
            onScanBluetoothListeners.forEach { it.onScaneCompleted() }
            bluetoothDeviceList.removeIf { bluetoothDevice -> !bluetoothScanning.any { it == bluetoothDevice.first } && !bluetoothDevice.second.isConnected }

            UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "ScanMidiBluetoothDeviceCompleted", "")
        }
    }

    fun checkIsBluetoothEnabled() {
        val btManager: BluetoothManager = context.getSystemService(Context.BLUETOOTH_SERVICE) as BluetoothManager
        val bluetoothAdapter = btManager.adapter

        UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "BluetoothResultStatusHandler", bluetoothAdapter.isEnabled.toString())
    }

    fun scanMidiBluetooth() {
        bluetoothScanManager.scanBleDevices()
    }

    fun stopScanMidiBluetooth() {
        bluetoothScanManager.stopScanDevices()
    }

    fun getBluetoothDeviceJsonString(): String {
        var midiBluetoothDevices: MutableList<MidiDeviceObject> = arrayListOf()

        midiDeviceList.forEach {
            if (midiBluetoothDevices.any { btDevice -> btDevice.macAddress == it.first }) return@forEach

            val btDevice = it.second.info.properties.getParcelable<BluetoothDevice>(MidiDeviceInfo.PROPERTY_BLUETOOTH_DEVICE)
            val address = if (btDevice == null) it.first else btDevice.address
            val deviceName = if (btDevice == null) it.second.info.properties.getString(MidiDeviceInfo.PROPERTY_NAME) ?: "Unknown" else btDevice.name

            midiBluetoothDevices.add(MidiDeviceObject(macAddress = address, deviceName = deviceName, isConnected = true))

            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "getBluetoothDeviceJsonString2: $deviceName [$address]")
        }

        bluetoothDeviceList.forEach {
            if (!it.second.isActive || midiBluetoothDevices.any { btDevice -> btDevice.macAddress == it.first }) return@forEach

            val bluetoothDeviceName = it.second.getBluetoothDeviceName()
            val isConnected = it.second.isConnected

            midiBluetoothDevices.add(MidiDeviceObject(macAddress = it.first, deviceName = bluetoothDeviceName, isConnected = isConnected))

            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "getBluetoothDeviceJsonString1: $bluetoothDeviceName [${it.first}] $isConnected")
        }

        return midiBluetoothDevices.toJsonString()
    }

    fun connectBluetooth(address: String) {
        val midiManager: MidiManager = context.getSystemService(Context.MIDI_SERVICE) as MidiManager
        val handler = Handler.createAsync(context.mainLooper)
        val bluetoothDevice = bluetoothDeviceList.find { it.first == address }

        if (bluetoothDevice == null) {
            Log.e(TAG_BLUETOOTH_DEVICE_PLUGIB, "connectBluetooth: not found bluetooth device $address")
            return
        }

        midiManager.openBluetoothDevice(bluetoothDevice.second.getBluetoothDevice(), {
            bluetoothDevice.second.setIsConnected(true)
            midiDeviceList.add(address to it)

            val jsonObject = JSONObject()

            jsonObject.put("Address", address)
            jsonObject.put("MidiDeviceId", it.info.id)

            UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "OnBluetoothDeviceConnected", jsonObject.toString())

            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "connectBluetooth: bluetooth device $address [${it.info.id}] connected")
        }, handler)
    }

    fun disconnectBluetooth(address: String) {
        val bluetoothDevice = bluetoothDeviceList.find { it.first == address }
        val midiDevice = midiDeviceList.find { it.first == address }

        if (midiDevice == null || bluetoothDevice == null) {
            Log.e(TAG_BLUETOOTH_DEVICE_PLUGIB, "disconnectBluetooth: not found bluetooth device $address")
            return
        }

        closeDeviceFromPort(midiDevice.second.info.id.toString())

        midiDevice.second.close()
        bluetoothDevice.second.setIsConnected(false)

        val jsonObject = JSONObject()

        jsonObject.put("Address", address)
        jsonObject.put("MidiDeviceId", midiDevice.second.info.id)

        UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "OnBluetoothDeviceDisconnected", jsonObject.toString())

        Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "disconnectBluetooth: bluetooth device $address disconnected")
    }

    fun getMidiDeviceJsonString(): String {
        var midiDeviceList: MutableList<MidiDeviceObject> = arrayListOf()
        val midiManager = context.getSystemService(Context.MIDI_SERVICE) as MidiManager

        midiManager.devices.forEach { device ->
            val idString = device.id.toString()
            val deviceName = device.properties.getString(MidiDeviceInfo.PROPERTY_NAME) ?: "Unknown device"
            val isConnected = noteroMidiDevicePorts.any { it.getId() == idString }

            midiDeviceList.add(MidiDeviceObject(deviceName = deviceName, macAddress = idString, isConnected = isConnected))
        }

        return midiDeviceList.toJsonString()
    }

    fun openDeviceToPort(idString: String) {
        val midiManager = context.getSystemService(Context.MIDI_SERVICE) as MidiManager
        val bluetoothDevice = midiManager.devices.find { it.id == idString.toInt() }

        if (bluetoothDevice != null) discoverMidiBluetooth(bluetoothDevice)
    }

    fun closeDeviceFromPort(idString: String) {
        noteroMidiDevicePorts.find { it.getId() == idString }?.let {
            it.close()
            noteroMidiDevicePorts.remove(it)

            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "closeDeviceFromPort: close ported $idString")
            UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "DisconnectedFromPortHandler", idString)
        }
    }

    fun setOnScanBluetoothListener(listener: OnScanBluetoothListener) {
        onScanBluetoothListeners.add(listener)
    }

    private fun discoverMidiBluetooth(deviceInfo: MidiDeviceInfo) {
        val midiManager = context.getSystemService(Context.MIDI_SERVICE) as MidiManager
        val outputPortInfo = deviceInfo.ports.find { it.type == PortInfo.TYPE_OUTPUT }

        if (outputPortInfo == null) return

        val portIndex = outputPortInfo.portNumber
        val handler = Handler.createAsync(context.mainLooper)

        midiManager.openDevice(deviceInfo, { midiDevice ->
            val outputPort = midiDevice.openOutputPort(portIndex)

            if (outputPort != null) {
                val idString = midiDevice.info.id.toString()

                if (!noteroMidiDevicePorts.any { it.getId() == idString }) {
                    val noteroDevicePort = NoteroMidiDevicePort(outputPort, midiDevice)

                    noteroDevicePort.open()

                    noteroMidiDevicePorts.add(noteroDevicePort)

                    Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "discoverMidiBluetooth: open ported $idString")
                    UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "ConnectedToPortHandler", idString)
                }
            } else {
                Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "cannot connect to output port ${portIndex}, maybe you already connect to it?")
            }
        }, handler)
    }

//    private fun discoverMidiBluetooth(deviceInfo: MidiDevice) {
//        val outputPortInfo = deviceInfo.info.ports.find { it.type == PortInfo.TYPE_OUTPUT }
//
//        if (outputPortInfo == null) return
//
//        val portIndex = outputPortInfo.portNumber
//        val outputPort = deviceInfo.openOutputPort(portIndex)
//
//        if (outputPort != null) {
//            val idString = deviceInfo.info.id.toString()
//
//            if (!noteroMidiDevicePorts.any { it.getId() == idString }) {
//                val noteroDevicePort = NoteroMidiDevicePort(outputPort, idString)
//
//                noteroDevicePort.open()
//
//                noteroMidiDevicePorts.add(noteroDevicePort)
//
//                Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "closeDeviceFromPort: open ported $idString")
//                UnityPlayer.UnitySendMessage("MidiBluetoothAdapter", "ConnectedToPortHandler", idString)
//            }
//        } else {
//            Log.d(TAG_BLUETOOTH_DEVICE_PLUGIB, "cannot connect to output port ${portIndex}, maybe you already connect to it?")
//        }
//    }
}