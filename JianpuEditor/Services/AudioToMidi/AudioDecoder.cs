using System;
using ManagedBass;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Decodes any file format BASS supports (wav/mp3/ogg/flac/...) down to mono float
    /// samples at the exact rate the basic-pitch model was trained on, using BASS's own
    /// decoder + resampler instead of writing one. Setting the channel's Frequency
    /// attribute away from its native rate makes BASS resample decode output too, not
    /// just playback.
    /// </summary>
    /// <remarks>
    /// <see cref="BassFlags.Mono"/> does NOT downmix arbitrary decode output -- per BASS's
    /// own documentation it only applies "(MP3/MP2/MP1 only)". For every other codec
    /// (including plain WAV/PCM, the common case here) it is silently ignored and the
    /// channel stays at its source channel count, so a stereo file would come back as
    /// raw interleaved L/R samples read as if they were one mono stream -- doubling the
    /// apparent sample count (and therefore duration) and turning the actual audio
    /// content into noise. This class downmixes manually instead, using the channel's
    /// real channel count from <see cref="Bass.ChannelGetInfo(int)"/>, which works for
    /// every codec.
    /// </remarks>
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

            var handle = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Float);
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

                var channelCount = Math.Max(1, Bass.ChannelGetInfo(handle).Channels);

                var samples = new System.Collections.Generic.List<float>();
                var buffer = new float[65536];
                var pending = new float[channelCount];
                var pendingFill = 0;
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
                        pending[pendingFill] = buffer[i];
                        pendingFill++;
                        if (pendingFill == channelCount)
                        {
                            var sum = 0f;
                            for (var c = 0; c < channelCount; c++)
                            {
                                sum += pending[c];
                            }
                            samples.Add(sum / channelCount);
                            pendingFill = 0;
                        }
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
