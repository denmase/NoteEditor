using System.Collections.Generic;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    public static class VoltaService
    {
        public static void NormalizeScore(JianpuScore score)
        {
            if (score != null && score.Voltas == null)
            {
                score.Voltas = new List<JianpuVolta>();
            }
        }

        public static bool TryAddVolta(
            JianpuScore score,
            int startMeasureIndex,
            int endMeasureIndex,
            string label,
            out string message)
        {
            NormalizeScore(score);
            if (score?.Measures == null
                || startMeasureIndex < 0
                || endMeasureIndex < startMeasureIndex
                || endMeasureIndex >= score.Measures.Count)
            {
                message = "Invalid measure range for volta bracket";
                return false;
            }

            foreach (var existing in score.Voltas)
            {
                if (startMeasureIndex <= existing.EndMeasureIndex && endMeasureIndex >= existing.StartMeasureIndex)
                {
                    message = "Selected measures already have a volta bracket";
                    return false;
                }
            }

            score.Voltas.Add(new JianpuVolta
            {
                StartMeasureIndex = startMeasureIndex,
                EndMeasureIndex = endMeasureIndex,
                Label = string.IsNullOrWhiteSpace(label) ? "1." : label
            });
            message = "Added volta bracket";
            return true;
        }

        public static bool TryRemoveVoltaCovering(JianpuScore score, int measureIndex, out string message)
        {
            NormalizeScore(score);
            if (score?.Voltas != null)
            {
                for (var i = 0; i < score.Voltas.Count; i++)
                {
                    var volta = score.Voltas[i];
                    if (measureIndex >= volta.StartMeasureIndex && measureIndex <= volta.EndMeasureIndex)
                    {
                        score.Voltas.RemoveAt(i);
                        message = "Removed volta bracket";
                        return true;
                    }
                }
            }

            message = "No volta bracket at the current measure";
            return false;
        }
    }
}
