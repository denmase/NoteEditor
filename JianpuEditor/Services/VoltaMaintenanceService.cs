using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    public static class VoltaMaintenanceService
    {
        public static void OnMeasureRemoved(JianpuScore score, int removedMeasureIndex)
        {
            if (score?.Voltas == null || score.Voltas.Count == 0)
            {
                return;
            }

            for (var i = score.Voltas.Count - 1; i >= 0; i--)
            {
                var volta = score.Voltas[i];
                if (removedMeasureIndex < volta.StartMeasureIndex)
                {
                    volta.StartMeasureIndex--;
                    volta.EndMeasureIndex--;
                }
                else if (removedMeasureIndex >= volta.StartMeasureIndex && removedMeasureIndex <= volta.EndMeasureIndex)
                {
                    if (volta.StartMeasureIndex == volta.EndMeasureIndex)
                    {
                        score.Voltas.RemoveAt(i);
                        continue;
                    }

                    volta.EndMeasureIndex--;
                }
            }
        }
    }
}
