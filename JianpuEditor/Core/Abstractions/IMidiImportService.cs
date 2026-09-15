using System.Collections.Generic;
using JianpuEditor.Models;

namespace JianpuEditor.Core.Abstractions
{
    public interface IMidiImportService
    {
        JianpuScore Import(string path, int? trackIndex = null);

        IReadOnlyList<MidiTrackInfo> GetTrackInfos(string path);
    }
}
