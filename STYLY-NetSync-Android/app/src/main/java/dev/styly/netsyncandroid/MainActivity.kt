package dev.styly.netsyncandroid

import android.Manifest
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.content.pm.PackageManager
import android.net.wifi.WifiManager
import android.os.Build
import android.os.Bundle
import android.os.IBinder
import android.text.format.Formatter
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.res.stringResource
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import dev.styly.netsyncandroid.service.NetSyncForegroundService
import dev.styly.netsyncandroid.service.ServiceStatus
import java.io.File
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    private var netSyncService: NetSyncForegroundService? = null
    private var statusJob: Job? = null

    private val serviceConnection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, service: IBinder?) {
            val binder = service as? NetSyncForegroundService.LocalBinder
            netSyncService = binder?.getService()
            statusJob?.cancel()
            statusJob = lifecycleScope.launch {
                netSyncService?.statusFlow?.collect { latestStatus = it }
            }
        }

        override fun onServiceDisconnected(name: ComponentName?) {
            statusJob?.cancel()
            netSyncService = null
        }
    }

    private var latestStatus by mutableStateOf(ServiceStatus())

    private val notificationPermissionLauncher =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
            Log.i(NetSyncForegroundService.TAG, "POST_NOTIFICATIONS granted=$granted")
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        requestNotificationPermissionIfNeeded()

        bindService(
            Intent(this, NetSyncForegroundService::class.java),
            serviceConnection,
            Context.BIND_AUTO_CREATE,
        )

        setContent {
            MaterialTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    MainScreen(
                        initialHost = latestStatus.host,
                        initialPort = latestStatus.port,
                        status = latestStatus,
                        lanIp = getLanIpAddress(this),
                        logPath = File(filesDir, "netsync.log").absolutePath,
                        onStart = { host, port -> startServer(host, port) },
                        onStop = { stopServer() },
                    )
                }
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        try {
            unbindService(serviceConnection)
        } catch (e: IllegalArgumentException) {
            Log.w(NetSyncForegroundService.TAG, "Service not bound", e)
        }
    }

    private fun startServer(host: String, port: Int) {
        val intent = Intent(this, NetSyncForegroundService::class.java).apply {
            action = NetSyncForegroundService.ACTION_START
            putExtra(NetSyncForegroundService.EXTRA_HOST, host)
            putExtra(NetSyncForegroundService.EXTRA_PORT, port)
        }
        ContextCompat.startForegroundService(this, intent)
    }

    private fun stopServer() {
        val intent = Intent(this, NetSyncForegroundService::class.java).apply {
            action = NetSyncForegroundService.ACTION_STOP
        }
        startService(intent)
    }

    private fun requestNotificationPermissionIfNeeded() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) {
            return
        }
        val granted = ContextCompat.checkSelfPermission(
            this,
            Manifest.permission.POST_NOTIFICATIONS,
        ) == PackageManager.PERMISSION_GRANTED
        if (!granted) {
            notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    private fun getLanIpAddress(context: Context): String {
        return try {
            val wifiManager = context.applicationContext.getSystemService(WIFI_SERVICE) as WifiManager
            val ip = wifiManager.connectionInfo.ipAddress
            Formatter.formatIpAddress(ip)
        } catch (e: Throwable) {
            "Unavailable"
        }
    }
}

@Composable
private fun MainScreen(
    initialHost: String,
    initialPort: Int,
    status: ServiceStatus,
    lanIp: String,
    logPath: String,
    onStart: (String, Int) -> Unit,
    onStop: () -> Unit,
) {
    var host by remember { mutableStateOf(initialHost.ifBlank { "0.0.0.0" }) }
    var portText by remember { mutableStateOf(initialPort.toString()) }
    var logTail by remember { mutableStateOf("") }
    var refreshTick by remember { mutableIntStateOf(0) }

    LaunchedEffect(refreshTick) {
        val file = File(logPath)
        logTail = if (file.exists()) {
            file.readLines().takeLast(80).joinToString("\n")
        } else {
            "No log file yet"
        }
    }

    LaunchedEffect(status.state) {
        while (true) {
            delay(1000)
            refreshTick += 1
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Text(text = stringResource(R.string.app_name), style = MaterialTheme.typography.headlineSmall)
        Text(text = "State: ${status.state}")
        Text(text = "LAN IP: $lanIp")
        Text(text = "Log file: $logPath", maxLines = 2, overflow = TextOverflow.Ellipsis)

        OutlinedTextField(
            modifier = Modifier.fillMaxWidth(),
            value = host,
            onValueChange = { host = it },
            label = { Text("Host") },
        )

        OutlinedTextField(
            modifier = Modifier.fillMaxWidth(),
            value = portText,
            onValueChange = { portText = it.filter(Char::isDigit).take(5) },
            label = { Text("Port") },
        )

        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Button(onClick = {
                val port = portText.toIntOrNull() ?: 5555
                onStart(host.ifBlank { "0.0.0.0" }, port)
            }) {
                Text(stringResource(R.string.start))
            }

            Button(onClick = onStop) {
                Text(stringResource(R.string.stop))
            }

            Button(onClick = { refreshTick += 1 }) {
                Text(stringResource(R.string.refresh_log))
            }
        }

        Text(
            text = "注意: Foreground Service + WakeLockによりバックグラウンド継続性は上がりますが、機種や省電力設定で停止する場合があります。",
            style = MaterialTheme.typography.bodySmall,
        )
        Text(
            text = "UDPブロードキャスト探索は画面OFFや省電力で不安定になる場合があります。可能なら固定IPやQR配布のユニキャスト方式を推奨します。",
            style = MaterialTheme.typography.bodySmall,
        )

        Spacer(modifier = Modifier.height(8.dp))
        Text("Logs (tail)")
        Text(logTail)
    }
}
