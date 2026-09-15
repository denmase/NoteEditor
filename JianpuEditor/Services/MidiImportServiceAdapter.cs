using System.Collections.Generic;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    public sealed class MidiImportServiceAdapter : IMidiImportService
    {
        public JianpuScore Import(string path, int? trackIndex = null)
        {
            return MidiImportService.Import(path, trackIndex);
        }

        public IReadOnlyList<MidiTrackInfo> GetTrackInfos(string path)
        {
            return MidiImportService.GetTrackInfos(path);
        }
    }
}
