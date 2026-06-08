package extensions

import com.android.build.api.dsl.BaseFlavor
import org.gradle.api.Project
import java.util.*

fun BaseFlavor.stringField(name: String, value: String) {
    buildConfigField("String", name, "\"$value\"")
}

@Suppress("UNCHECKED_CAST")
fun <T> Project.loadProperty(name: String, default: T) : T {
    val file = rootProject.file(".gradle/gradle.properties")
    if (!file.exists()) {
        val rootFile = rootProject.file("gradle.properties")
        if (rootFile.exists()) {
            val properties = Properties().apply {
                load(rootFile.reader())
            }
            return (properties.getProperty(name) as? T) ?: default
        }
        return default
    }
    val properties = Properties().apply {
        load(file.reader())
    }
    return (properties.getProperty(name) as? T) ?: default
}