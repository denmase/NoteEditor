using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JianpuEditor.Models;

namespace JianpuEditor.Core.Abstractions
{
    public sealed class MidiDdspRenderResult
    {
        public static readonly MidiDdspRenderResult Empty = new MidiDdspRenderResult
        {
            RenderedChannels = System.Array.Empty<int>()
        };

        public bool HasAudio => Samples != null && Samples.Length > 0;

        public float[] Samples { get; set; }

        public int SampleRate { get; set; }

        /// <summary>Which <see cref="Services.ScoreMidiSchedule"/> channels (melody=0, chord=1,
        /// extra-voice slots from 2) ended up rendered into <see cref="Samples"/>, so the live,
        /// per-note MIDI playback path can exclude them and avoid playing the same part twice.</summary>
        public IReadOnlyList<int> RenderedChannels { get; set; }
    }

    /// <summary>
    /// Renders the parts of a score whose instrument MIDI-DDSP has a pretrained model for (13
    /// URMP instruments -- violin, viola, cello, double bass, flute, oboe, clarinet, saxophone,
    /// bassoon, trumpet, horn, trombone, tuba) to a single pre-mixed audio buffer, using the
    /// neural model instead of the bundled SoundFont. MIDI-DDSP itself can only render a whole
    /// part upfront (a full neural-network pass), not stream note-by-note, so this is inherently
    /// an offline, potentially multi-second operation -- callers should treat it as such (a
    /// background task with a status indication), never call it on a UI thread.
    /// </summary>
    public interface IMidiDdspSynthesisService
    {
        /// <summary>True once a weights folder is configured (see <see
        /// cref="Rendering.AppTheme.MidiDdspWeightsPath"/>) and exists. Doesn't guarantee the
        /// weights are actually valid -- that's only known once a render is attempted.</summary>
        bool IsConfigured { get; }

        /// <summary>Whether this GM program is one MIDI-DDSP has a pretrained model for. A cheap,
        /// synchronous lookup -- doesn't need the model loaded.</summary>
        bool IsInstrumentSupported(int midiProgram);

        /// <summary>Renders every DDSP-eligible part of <paramref name="score"/> into one mixed
        /// buffer. Returns <see cref="MidiDdspRenderResult.Empty"/> (no exception) when nothing in
        /// the score is DDSP-eligible, or when <see cref="IsConfigured"/> is false. Loads the
        /// pretrained model on first use and keeps it cached for later calls.</summary>
        Task<MidiDdspRenderResult> RenderAsync(JianpuScore score, CancellationToken cancellationToken);
    }
}
