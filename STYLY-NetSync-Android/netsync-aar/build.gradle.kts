import java.io.File

plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android")
    id("com.chaquo.python")
}

fun wheelNamesFor(projectDir: File): List<String> {
    val wheelDir = File(projectDir, "src/main/python/wheels")
    if (!wheelDir.isDirectory) {
        throw GradleException(
            "Missing wheel directory: ${wheelDir.path}\n" +
                "Place local wheels here (offline install mode is enabled)."
        )
    }
    return wheelDir
        .listFiles()
        ?.filter { it.isFile && it.extension == "whl" }
        ?.map { it.name }
        ?.sorted()
        .orEmpty()
}

fun requireAnyWheel(names: List<String>, description: String, predicate: (String) -> Boolean) {
    if (names.none(predicate)) {
        throw GradleException(
            "Required wheel not found for $description.\n" +
                "Current wheels:\n- ${if (names.isEmpty()) "(none)" else names.joinToString("\n- ")}"
        )
    }
}

tasks.register("validatePythonWheels") {
    group = "verification"
    description = "Validate required local Python wheels for Chaquopy offline install."

    doLast {
        val names = wheelNamesFor(project.projectDir)

        requireAnyWheel(names, "pyzmq 27.1.0 cp313 android_24_arm64_v8a") { name ->
            name.startsWith("pyzmq-27.1.0-") &&
                name.contains("-cp313-") &&
                name.contains("-android_24_arm64_v8a.whl")
        }

        requireAnyWheel(names, "loguru 0.7.2 (any compatible wheel)") { name ->
            name.startsWith("loguru-0.7.2-") && name.endsWith(".whl")
        }
    }
}

val syncScriptRelativePath = "scripts/sync_styly_netsync_from_server.sh"

tasks.register<Exec>("syncPythonSources") {
    group = "build setup"
    description = "Sync styly_netsync from Server upstream and Android patches."
    workingDir = rootProject.projectDir
    commandLine("bash", syncScriptRelativePath)
}

android {
    namespace = "dev.styly.netsyncandroid.aar"
    compileSdk = 35

    defaultConfig {
        minSdk = 26

        consumerProguardFiles("consumer-rules.pro")

        ndk {
            abiFilters += listOf("arm64-v8a")
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    packaging {
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
}

chaquopy {
    defaultConfig {
        version = "3.13"
        pip {
            options("--no-index", "--find-links", "src/main/python/wheels", "--only-binary=:all:")
            install("loguru==0.7.2")
            install("pyzmq==27.1.0")
        }
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.appcompat:appcompat:1.7.0")
}

tasks.named("preBuild") {
    dependsOn("syncPythonSources", "validatePythonWheels")
}
