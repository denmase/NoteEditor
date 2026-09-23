using System.Threading;
using System.Threading.Tasks;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Tests.Services
{
    internal sealed class FakeMidiDdspSynthesisService : IMidiDdspSynthesisService
    {
        public bool IsConfigured { get; set; }

        /// <summary>GM programs this fake treats as DDSP-eligible.</summary>
        public System.Collections.Generic.HashSet<int> SupportedPrograms { get; } = new System.Collections.Generic.HashSet<int>();

        public MidiDdspRenderResult NextResult { get; set; } = MidiDdspRenderResult.Empty;

        public int RenderCallCount { get; private set; }

        public bool IsInstrumentSupported(int midiProgram)
        {
            return SupportedPrograms.Contains(midiProgram);
        }

        public Task<MidiDdspRenderResult> RenderAsync(JianpuScore score, CancellationToken cancellationToken)
        {
            RenderCallCount++;
            return Task.FromResult(NextResult);
        }
    }
}
