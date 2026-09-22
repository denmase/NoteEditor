using System;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>Backs the Edit &gt; Voices menu's Single/SATB preset -- the minimal, whole-score
    /// way to reach the new multi-voice rendering/playback path (see ROADMAP.md) without yet
    /// having per-note editing for the extra voices themselves. Populates or clears every
    /// measure's <see cref="JianpuMeasure.ExtraVoices"/> uniformly; content already entered into
    /// an existing SATB voice (e.g. by hand-editing a saved file) is left alone by
    /// <see cref="ApplySatb"/>, so re-choosing "SATB" never clobbers real voice content.</summary>
    public static class VoiceModeService
    {
        private static readonly string[] SatbRoles = { "Alto", "Tenor", "Bass" };

        /// <summary>Adds rest-filled Alto/Tenor/Bass voices to every measure that doesn't already
        /// have extra voices, each rest sized to that measure's own beat count so it never trips
        /// the cross-voice duration-mismatch check in <c>ScoreMidiSchedule</c>. Returns true if any
        /// measure actually changed.</summary>
        public static bool ApplySatb(JianpuScore score)
        {
            var changed = false;
            var measures = score?.Measures;
            if (measures == null)
            {
                return false;
            }

            // Only when still at the default -- never overwrites a label the user picked by hand,
            // matching how the loop below never clobbers a measure's already-populated ExtraVoices.
            if (string.IsNullOrEmpty(score.PrimaryVoiceLabel))
            {
                score.PrimaryVoiceLabel = "Soprano";
                changed = true;
            }

            foreach (var measure in measures)
            {
                if (measure.ExtraVoices == null)
                {
                    measure.ExtraVoices = new System.Collections.Generic.List<JianpuVoice>();
                }

                if (measure.ExtraVoices.Count > 0)
                {
                    continue;
                }

                var restDashes = GetRestDashesForMeasure(measure);
                foreach (var role in SatbRoles)
                {
                    measure.ExtraVoices.Add(new JianpuVoice
                    {
                        Role = role,
                        Notes = { new JianpuNote { Type = NoteType.Rest, Dashes = restDashes } }
                    });
                }

                changed = true;
            }

            return changed;
        }

        /// <summary>Clears every measure's <see cref="JianpuMeasure.ExtraVoices"/>, returning to
        /// plain single-voice rendering/playback. Returns true if any measure actually changed.</summary>
        public static bool ApplySingle(JianpuScore score)
        {
            var changed = false;
            var measures = score?.Measures;
            if (measures == null)
            {
                return false;
            }

            // "Soprano" (or any other custom label) only makes sense alongside Alto/Tenor/Bass;
            // unconditionally resets back to the "Melody" default, mirroring how every measure's
            // ExtraVoices are unconditionally cleared below regardless of how they got there.
            if (!string.IsNullOrEmpty(score.PrimaryVoiceLabel))
            {
                score.PrimaryVoiceLabel = null;
                changed = true;
            }

            foreach (var measure in measures)
            {
                if (measure.ExtraVoices != null && measure.ExtraVoices.Count > 0)
                {
                    measure.ExtraVoices.Clear();
                    changed = true;
                }
            }

            return changed;
        }

        private static int GetRestDashesForMeasure(JianpuMeasure measure)
        {
            var beats = ScoreMidiSchedule.GetMeasureDurationUnits(measure);
            // A rest's own GetDurationUnits is (1 + Dashes), so Dashes = beats - 1 reproduces the
            // measure's beat count exactly for any whole number of beats (including a pickup
            // measure's fewer-than-4 beats). A fractional beat count (e.g. a triplet or a dotted
            // pickup) can't be expressed as a single rest's dash count -- rounds to the nearest
            // whole beat rather than leaving the new voice silently un-populated.
            return Math.Max(0, (int)Math.Round(beats) - 1);
        }
    }
}
