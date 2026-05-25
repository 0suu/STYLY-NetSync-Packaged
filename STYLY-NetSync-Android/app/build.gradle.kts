import java.io.File

plugins {
    id("com.android.application")
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

android {
    namespace = "dev.styly.netsyncandroid"
    compileSdk = 35

    defaultConfig {
        applicationId = "dev.styly.netsyncandroid"
        minSdk = 26
        targetSdk = 34
        versionCode = 1
        versionName = "0.1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"

        ndk {
            abiFilters += listOf("arm64-v8a")
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        compose = true
    }

    composeOptions {
        kotlinCompilerExtensionVersion = "1.5.14"
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
    val composeBom = platform("androidx.compose:compose-bom:2025.01.01")

    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("androidx.activity:activity-compose:1.10.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.8.7")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation(composeBom)

    debugImplementation("androidx.compose.ui:ui-tooling")
    debugImplementation("androidx.compose.ui:ui-test-manifest")
}

tasks.named("preBuild") {
    dependsOn("validatePythonWheels")
}
