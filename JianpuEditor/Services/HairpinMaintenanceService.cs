using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    public static class HairpinMaintenanceService
    {
        public static void OnMelodyNoteCountChanged(JianpuScore score, int measureIndex, int startNoteIndex, int countDelta)
        {
            if (score?.Hairpins == null || score.Hairpins.Count == 0 || countDelta == 0)
            {
                return;
            }

            foreach (var hairpin in score.Hairpins)
            {
                if (hairpin.StartMeasureIndex == measureIndex && hairpin.StartNoteIndex >= startNoteIndex)
                {
                    hairpin.StartNoteIndex += countDelta;
                }

                if (hairpin.EndMeasureIndex == measureIndex && hairpin.EndNoteIndex >= startNoteIndex)
                {
                    hairpin.EndNoteIndex += countDelta;
                }
            }
        }

        public static void OnNoteRemoved(JianpuScore score, int measureIndex, int removedNoteIndex)
        {
            if (score?.Hairpins == null || score.Hairpins.Count == 0)
            {
                return;
            }

            for (var i = score.Hairpins.Count - 1; i >= 0; i--)
            {
                var hairpin = score.Hairpins[i];
                if (ReferencesNote(hairpin, measureIndex, removedNoteIndex))
                {
                    score.Hairpins.RemoveAt(i);
                    continue;
                }

                if (hairpin.StartMeasureIndex == measureIndex && hairpin.StartNoteIndex > removedNoteIndex)
                {
                    hairpin.StartNoteIndex--;
                }

                if (hairpin.EndMeasureIndex == measureIndex && hairpin.EndNoteIndex > removedNoteIndex)
                {
                    hairpin.EndNoteIndex--;
                }
            }
        }

        public static void OnMeasureRemoved(JianpuScore score, int removedMeasureIndex)
        {
            if (score?.Hairpins == null || score.Hairpins.Count == 0)
            {
                return;
            }

            for (var i = score.Hairpins.Count - 1; i >= 0; i--)
            {
                var hairpin = score.Hairpins[i];
                if (hairpin.StartMeasureIndex == removedMeasureIndex || hairpin.EndMeasureIndex == removedMeasureIndex)
                {
                    score.Hairpins.RemoveAt(i);
                    continue;
                }

                if (hairpin.StartMeasureIndex > removedMeasureIndex)
                {
                    hairpin.StartMeasureIndex--;
                }

                if (hairpin.EndMeasureIndex > removedMeasureIndex)
                {
                    hairpin.EndMeasureIndex--;
                }
            }
        }

        private static bool ReferencesNote(JianpuHairpin hairpin, int measureIndex, int noteIndex)
        {
            return (hairpin.StartMeasureIndex == measureIndex && hairpin.StartNoteIndex == noteIndex)
                || (hairpin.EndMeasureIndex == measureIndex && hairpin.EndNoteIndex == noteIndex);
        }
    }
}
