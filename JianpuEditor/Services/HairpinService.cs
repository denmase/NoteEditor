using System.Collections.Generic;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    public static class HairpinService
    {
        public static void NormalizeScore(JianpuScore score)
        {
            if (score != null && score.Hairpins == null)
            {
                score.Hairpins = new List<JianpuHairpin>();
            }
        }

        public static bool TryAddHairpin(
            JianpuScore score,
            int startMeasureIndex,
            int startNoteIndex,
            int endMeasureIndex,
            int endNoteIndex,
            bool isCrescendo,
            out string message)
        {
            NormalizeScore(score);
            if (!IsValidNote(score, startMeasureIndex, startNoteIndex)
                || !IsValidNote(score, endMeasureIndex, endNoteIndex))
            {
                message = "Invalid note range for hairpin";
                return false;
            }

            if (!IsBefore(startMeasureIndex, startNoteIndex, endMeasureIndex, endNoteIndex))
            {
                message = "A hairpin's ending note must come after its starting note";
                return false;
            }

            score.Hairpins.Add(new JianpuHairpin
            {
                StartMeasureIndex = startMeasureIndex,
                StartNoteIndex = startNoteIndex,
                EndMeasureIndex = endMeasureIndex,
                EndNoteIndex = endNoteIndex,
                IsCrescendo = isCrescendo
            });
            message = isCrescendo ? "Added crescendo" : "Added diminuendo";
            return true;
        }

        public static bool TryRemoveHairpinCovering(JianpuScore score, int measureIndex, int noteIndex, out string message)
        {
            NormalizeScore(score);
            if (score?.Hairpins != null)
            {
                for (var i = 0; i < score.Hairpins.Count; i++)
                {
                    var hairpin = score.Hairpins[i];
                    if (Covers(hairpin, measureIndex, noteIndex))
                    {
                        score.Hairpins.RemoveAt(i);
                        message = "Removed hairpin";
                        return true;
                    }
                }
            }

            message = "No hairpin at the selected note";
            return false;
        }

        private static bool Covers(JianpuHairpin hairpin, int measureIndex, int noteIndex)
        {
            return !IsBefore(measureIndex, noteIndex, hairpin.StartMeasureIndex, hairpin.StartNoteIndex)
                && !IsBefore(hairpin.EndMeasureIndex, hairpin.EndNoteIndex, measureIndex, noteIndex);
        }

        private static bool IsValidNote(JianpuScore score, int measureIndex, int noteIndex)
        {
            if (score?.Measures == null || measureIndex < 0 || measureIndex >= score.Measures.Count)
            {
                return false;
            }

            var notes = score.Measures[measureIndex].MelodyNotes;
            return notes != null && noteIndex >= 0 && noteIndex < notes.Count;
        }

        private static bool IsBefore(int measureA, int noteA, int measureB, int noteB)
        {
            if (measureA != measureB)
            {
                return measureA < measureB;
            }

            return noteA < noteB;
        }
    }
}
