using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    public static class ChordTransposeService
    {
        public static bool TryTransposeChords(JianpuScore score, string targetKeySignature, out string errorMessage, out int transposedCount)
        {
            transposedCount = 0;
            errorMessage = null;

            if (score == null)
            {
                errorMessage = "There is no score to transpose.";
                return false;
            }

            if (!KeySignatureService.TryParseTonicPitchClass(score.KeySignature, out var sourcePitchClass))
            {
                errorMessage = "Unable to recognize the current key signature \"" + (score.KeySignature ?? string.Empty) + "\". Please use a format like C, 1=G, or F#.";
                return false;
            }

            if (!KeySignatureService.TryParseTonicPitchClass(targetKeySignature, out var targetPitchClass))
            {
                errorMessage = "Unable to recognize the target key signature \"" + (targetKeySignature ?? string.Empty) + "\". Please use a format like C, 1=G, or F#.";
                return false;
            }

            var semitones = KeySignatureService.GetTransposeSemitones(sourcePitchClass, targetPitchClass);
            if (semitones == 0)
            {
                errorMessage = "The target key is the same as the current key; no transposition is needed.";
                return false;
            }

            if (score.Measures != null)
            {
                foreach (var measure in score.Measures)
                {
                    ChordMarkerService.NormalizeMeasure(measure);
                    if (measure.ChordMarkers == null)
                    {
                        continue;
                    }

                    foreach (var marker in measure.ChordMarkers)
                    {
                        var text = marker.Text?.Trim();
                        if (string.IsNullOrEmpty(text) || !ChordParser.IsChordSymbol(text))
                        {
                            continue;
                        }

                        marker.Text = ChordParser.TransposeSymbol(text, semitones);
                        transposedCount++;
                    }
                }
            }

            score.KeySignature = KeySignatureService.FormatKeySignature(targetPitchClass);
            return true;
        }
    }
}
