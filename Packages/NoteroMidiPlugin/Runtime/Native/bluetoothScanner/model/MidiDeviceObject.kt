package co.notero.midiplugin.bluetoothScanner.model

import android.annotation.SuppressLint
import org.json.JSONArray
import org.json.JSONObject

/**
 * A class that represents a BLE device.
 */
@SuppressLint("MissingPermission")
data class MidiDeviceObject(
    var deviceName: String,
    var macAddress: String,
    var isConnected: Boolean
) {
    companion object {
        fun List<MidiDeviceObject>.toJsonString(): String {
            val jsonArray = JSONArray()

            this.forEach { deviceInfo ->
                val jsonObject = JSONObject()

                jsonObject.put("macAddress", deviceInfo.macAddress)
                jsonObject.put("deviceName", deviceInfo.deviceName)
                jsonObject.put("isConnected", deviceInfo.isConnected)

                jsonArray.put(jsonObject)
            }

            return jsonArray.toString()
        }
    }
}

data class MidiEventInfo(
    var midiMessage: String,
    var midiNoteId: Int,
    var velocity: Int,
    var channel: Int
) {
    companion object {
        fun MidiEventInfo.toJsonString(): String {
            val jsonObject = JSONObject()

            jsonObject.put("midiMessage", midiMessage)
            jsonObject.put("midiNoteId", midiNoteId)
            jsonObject.put("velocity", velocity)
            jsonObject.put("channel", channel)

            return jsonObject.toString()
        }
    }
}