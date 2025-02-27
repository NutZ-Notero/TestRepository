package co.notero.midiplugin.bluetoothScanner

import android.annotation.SuppressLint
import android.bluetooth.BluetoothManager
import android.bluetooth.le.ScanFilter
import android.bluetooth.le.ScanSettings
import android.os.Handler
import android.os.Looper
import android.os.ParcelUuid
import android.util.Log
import co.notero.midiplugin.bluetoothScanner.model.BluetoothScanCallback
import java.util.UUID

const val TAG = "BluetoothScanManager"

class BluetoothScanManager(
    btManager: BluetoothManager,
    private val scanPeriod: Long = DEFAULT_SCAN_PERIOD,
    private val scanCallback: BluetoothScanCallback = BluetoothScanCallback()
) {
    private val btAdapter = btManager.adapter

    var beforeScanActions: MutableList<() -> Unit> = mutableListOf()
    var afterScanActions: MutableList<() -> Unit> = mutableListOf()

    private var scanning = false

    private val handler = Handler(Looper.getMainLooper())

    @SuppressLint("MissingPermission")
    fun scanBleDevices() {
        val bleScanner = btAdapter.bluetoothLeScanner

        fun stopScan() {
            if (!scanning) return

            scanning = false
            bleScanner.stopScan(scanCallback)

            executeAfterScanActions()
            Log.d(TAG, "scanBleDevices:stop scan: is scanning $scanning")
        }

        fun buildScanSettings(): ScanSettings {
            val builder = ScanSettings.Builder()

            builder.setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY)
            builder.setCallbackType(ScanSettings.CALLBACK_TYPE_ALL_MATCHES);

            return builder.build()
        }

        fun buildScanFilters(): List<ScanFilter> {
            val scanFilters = mutableListOf<ScanFilter>()
            val builder = ScanFilter.Builder()

            builder.setServiceUuid(ParcelUuid(MIDI_SERVICE))
            scanFilters.add(builder.build())

            return scanFilters
        }

        if (bleScanner == null) {
            scanning = false
            return
        }

        if (scanning) {
            stopScan()
        } else {
            handler.postDelayed({ stopScan() }, scanPeriod)
            executeBeforeScanActions()

            scanning = true
            bleScanner.startScan(buildScanFilters(), buildScanSettings(), scanCallback)
        }
    }

    @SuppressLint("MissingPermission")
    fun stopScanDevices() {
        val bleScanner = btAdapter.bluetoothLeScanner

        if (bleScanner == null) {
            scanning = false
            return
        }

        if (!scanning) return

        scanning = false
        bleScanner.stopScan(scanCallback)

        executeAfterScanActions()
        Log.d(TAG, "scanBleDevices:force stop scan: is scanning $scanning")
    }

    private fun executeBeforeScanActions() {
        executeListOfFunctions(beforeScanActions)
    }

    private fun executeAfterScanActions() {
        executeListOfFunctions(afterScanActions)
    }

    companion object {
        const val DEFAULT_SCAN_PERIOD: Long = 10000

        val MIDI_SERVICE: UUID = UUID.fromString("03B80E5A-EDE8-4B33-A751-6CE34EC4C700")

        private fun executeListOfFunctions(toExecute: List<() -> Unit>) {
            toExecute.forEach { it() }
        }
    }
}