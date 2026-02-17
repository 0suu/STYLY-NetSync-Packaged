package dev.styly.netsyncandroid.aar

import android.content.Context
import android.content.Intent
import androidx.core.content.ContextCompat

object NetSyncAndroidBridge {
    @JvmStatic
    fun start(context: Context, host: String?, port: Int) {
        val selectedHost = host?.ifBlank { "0.0.0.0" } ?: "0.0.0.0"
        val selectedPort = if (port > 0) port else 5555
        val intent = Intent(context, NetSyncForegroundService::class.java).apply {
            action = NetSyncForegroundService.ACTION_START
            putExtra(NetSyncForegroundService.EXTRA_HOST, selectedHost)
            putExtra(NetSyncForegroundService.EXTRA_PORT, selectedPort)
        }
        ContextCompat.startForegroundService(context, intent)
    }

    @JvmStatic
    fun stop(context: Context) {
        val intent = Intent(context, NetSyncForegroundService::class.java).apply {
            action = NetSyncForegroundService.ACTION_STOP
        }
        context.startService(intent)
    }
}
