package dev.styly.netsyncandroid.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.net.wifi.WifiManager
import android.os.Binder
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.util.Log
import androidx.core.app.NotificationCompat
import com.chaquo.python.PyObject
import com.chaquo.python.Python
import com.chaquo.python.android.AndroidPlatform
import dev.styly.netsyncandroid.MainActivity
import dev.styly.netsyncandroid.R
import java.io.File
import java.util.concurrent.Executors
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

private object ServerState {
    const val STOPPED = 0
    const val STARTING = 1
    const val RUNNING = 2
    const val STOPPING = 3
    const val ERROR = 4
}

data class ServiceStatus(
    val state: Int = ServerState.STOPPED,
    val host: String = "0.0.0.0",
    val port: Int = 5555,
    val message: String = "",
)

class NetSyncForegroundService : Service() {
    inner class LocalBinder : Binder() {
        fun getService(): NetSyncForegroundService = this@NetSyncForegroundService
    }

    companion object {
        const val TAG = "NetSync"
        const val ACTION_START = "dev.styly.netsyncandroid.action.START"
        const val ACTION_STOP = "dev.styly.netsyncandroid.action.STOP"

        const val EXTRA_HOST = "host"
        const val EXTRA_PORT = "port"

        private const val CHANNEL_ID = "netsync_server_channel"
        private const val NOTIFICATION_ID = 11001
    }

    private val binder = LocalBinder()
    private val executor = Executors.newSingleThreadExecutor()
    private val _statusFlow = MutableStateFlow(ServiceStatus())
    private var pyBootstrap: PyObject? = null

    private var wakeLock: PowerManager.WakeLock? = null
    private var multicastLock: WifiManager.MulticastLock? = null

    val statusFlow: StateFlow<ServiceStatus> = _statusFlow

    override fun onBind(intent: Intent?): IBinder {
        return binder
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START -> {
                val host = intent.getStringExtra(EXTRA_HOST).orEmpty().ifBlank { "0.0.0.0" }
                val port = intent.getIntExtra(EXTRA_PORT, 5555)
                startServer(host, port)
            }

            ACTION_STOP -> stopServerAndService()
            else -> Log.i(TAG, "Ignored unknown action: ${intent?.action}")
        }
        return START_STICKY
    }

    override fun onDestroy() {
        super.onDestroy()
        releaseLocks()
        executor.shutdownNow()
    }

    private fun startServer(host: String, port: Int) {
        val current = _statusFlow.value.state
        if (current == ServerState.RUNNING || current == ServerState.STARTING) {
            Log.i(TAG, "Server already running/starting")
            return
        }
        if (port != 5555) {
            Log.w(TAG, "Port $port selected. Unity NetSync default client expects dealer=5555 and sub=5556.")
        }

        _statusFlow.value = ServiceStatus(ServerState.STARTING, host, port, "Starting")

        ensureNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification(host, port))
        acquireLocks()

        executor.execute {
            try {
                initializePythonIfNeeded()
                val logPath = File(filesDir, "netsync.log").absolutePath

                pyBootstrap?.callAttr("start", host, port, logPath)

                _statusFlow.value = ServiceStatus(ServerState.RUNNING, host, port, "Server running")
                Log.i(TAG, "Server started at $host:$port")
                refreshNotification(host, port)
            } catch (t: Throwable) {
                Log.e(TAG, "Failed to start server", t)
                _statusFlow.value = ServiceStatus(ServerState.ERROR, host, port, t.message ?: "Unknown error")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
        }
    }

    private fun stopServerAndService() {
        val host = _statusFlow.value.host
        val port = _statusFlow.value.port
        _statusFlow.value = ServiceStatus(ServerState.STOPPING, host, port, "Stopping")

        executor.execute {
            try {
                pyBootstrap?.callAttr("stop")
                Log.i(TAG, "Server stopped")
            } catch (t: Throwable) {
                Log.e(TAG, "Failed to stop server cleanly", t)
            } finally {
                _statusFlow.value = ServiceStatus(ServerState.STOPPED, host, port, "Stopped")
                releaseLocks()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
        }
    }

    private fun initializePythonIfNeeded() {
        if (!Python.isStarted()) {
            Python.start(AndroidPlatform(this))
        }
        if (pyBootstrap == null) {
            val python = Python.getInstance()
            pyBootstrap = python.getModule("netsync_bootstrap")
        }
    }

    private fun ensureNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return
        }
        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        val existing = manager.getNotificationChannel(CHANNEL_ID)
        if (existing != null) {
            return
        }

        val channel = NotificationChannel(
            CHANNEL_ID,
            getString(R.string.notification_channel_name),
            NotificationManager.IMPORTANCE_LOW,
        ).apply {
            description = getString(R.string.notification_channel_description)
        }
        manager.createNotificationChannel(channel)
    }

    private fun buildNotification(host: String, port: Int): Notification {
        val stopIntent = Intent(this, NetSyncForegroundService::class.java).apply {
            action = ACTION_STOP
        }
        val stopPendingIntent = PendingIntent.getService(
            this,
            0,
            stopIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )

        val openIntent = Intent(this, MainActivity::class.java)
        val openPendingIntent = PendingIntent.getActivity(
            this,
            1,
            openIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle(getString(R.string.notification_title))
            .setContentText("$host:$port")
            .setSmallIcon(android.R.drawable.stat_notify_sync)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setContentIntent(openPendingIntent)
            .addAction(0, getString(R.string.stop), stopPendingIntent)
            .build()
    }

    private fun refreshNotification(host: String, port: Int) {
        val notification = buildNotification(host, port)
        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.notify(NOTIFICATION_ID, notification)
    }

    private fun acquireLocks() {
        val pm = getSystemService(Context.POWER_SERVICE) as PowerManager
        if (wakeLock == null) {
            wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "NetSync::ServerWakeLock")
        }
        if (wakeLock?.isHeld == false) {
            wakeLock?.acquire()
            Log.i(TAG, "WakeLock acquired")
        }

        try {
            val wifiManager = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
            if (multicastLock == null) {
                multicastLock = wifiManager.createMulticastLock("NetSync::MulticastLock")
                multicastLock?.setReferenceCounted(false)
            }
            if (multicastLock?.isHeld == false) {
                multicastLock?.acquire()
                Log.i(TAG, "MulticastLock acquired")
            }
        } catch (se: SecurityException) {
            Log.w(TAG, "MulticastLock not acquired (missing permission or restricted device)", se)
        } catch (t: Throwable) {
            Log.w(TAG, "MulticastLock not acquired", t)
        }
    }

    private fun releaseLocks() {
        if (wakeLock?.isHeld == true) {
            wakeLock?.release()
            Log.i(TAG, "WakeLock released")
        }
        try {
            if (multicastLock?.isHeld == true) {
                multicastLock?.release()
                Log.i(TAG, "MulticastLock released")
            }
        } catch (t: Throwable) {
            Log.w(TAG, "Failed to release MulticastLock", t)
        }
    }
}
