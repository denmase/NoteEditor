using System;
using ManagedBass;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Decodes any file format BASS supports (wav/mp3/ogg/flac/...) down to mono float
    /// samples at the exact rate the basic-pitch model was trained on, using BASS's own
    /// decoder + resampler instead of writing one. <see cref="BassFlags.Mono"/> downmixes
    /// multi-channel audio during decode, and setting the channel's Frequency attribute
    /// away from its native rate makes BASS resample decode output too, not just playback.
    /// </summary>
    internal static class AudioDecoder
    {
        public static float[] DecodeToMono(string path, int targetSampleRate)
        {
            if (!Bass.Init())
            {
                // BASS is process-wide; the playback synthesizer (BassMidiSynthesizer /
                // BassVstSynthesizer) has typically already initialized it by the time this
                // runs, which Bass.Init reports as Errors.Already -- not a real failure.
                if (Bass.LastError != Errors.Already)
                {
                    throw new InvalidOperationException("Failed to initialize BASS for audio decoding. Error: " + Bass.LastError);
                }
            }

            var handle = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Float | BassFlags.Mono);
            if (handle == 0)
            {
                throw new InvalidOperationException("Failed to open audio file '" + path + "'. Error: " + Bass.LastError);
            }

            try
            {
                if (!Bass.ChannelSetAttribute(handle, ChannelAttribute.Frequency, targetSampleRate))
                {
                    throw new InvalidOperationException("Failed to set decode sample rate. Error: " + Bass.LastError);
                }

                var samples = new System.Collections.Generic.List<float>();
                var buffer = new float[65536];
                while (true)
                {
                    var bytesRead = Bass.ChannelGetData(handle, buffer, buffer.Length * sizeof(float));
                    if (bytesRead <= 0)
                    {
                        break; // 0/negative: end of stream (or an error, which we treat as "done")
                    }

                    var floatsRead = bytesRead / sizeof(float);
                    for (var i = 0; i < floatsRead; i++)
                    {
                        samples.Add(buffer[i]);
                    }
                }

                return samples.ToArray();
            }
            finally
            {
                Bass.StreamFree(handle);
            }
        }
    }
}
