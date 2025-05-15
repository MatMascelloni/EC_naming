using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip, out string filepath, bool saveToFile, string filename)
    {
        filepath = Path.Combine(Application.dataPath, filename);
        MemoryStream stream = new MemoryStream();
        WriteWavFile(stream, clip);

        byte[] bytes = stream.ToArray();

        if (saveToFile)
        {
            File.WriteAllBytes(filepath, bytes);
        }

        return bytes;
    }

    private static void WriteWavFile(Stream stream, AudioClip clip)
    {
        int samples = clip.samples * clip.channels;
        float[] data = new float[samples];
        clip.GetData(data, 0);

        ushort bitDepth = 16;
        ushort channels = (ushort)clip.channels;
        int sampleRate = clip.frequency;
        int byteRate = sampleRate * channels * bitDepth / 8;
        int blockAlign = channels * bitDepth / 8;

        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((ushort)blockAlign);
            writer.Write((ushort)bitDepth);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(samples * 2);

            foreach (var sample in data)
            {
                short s = (short)(Mathf.Clamp(sample, -1.0f, 1.0f) * short.MaxValue);
                writer.Write(s);
            }
        }
    }
}

