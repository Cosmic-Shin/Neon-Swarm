using UnityEditor;
using UnityEngine;

// Music in Resources/Music streams from disk (minutes-long tracks would otherwise sit decoded in memory).
public class MusicImporter : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
        var ai = (AudioImporter)assetImporter;
        var s = ai.defaultSampleSettings;
        s.loadType = AudioClipLoadType.Streaming;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        ai.defaultSampleSettings = s;
        ai.loadInBackground = true;
    }
}
