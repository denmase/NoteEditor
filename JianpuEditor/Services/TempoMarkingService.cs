namespace JianpuEditor.Services
{
    /// <summary>Maps a BPM value to the conventional Italian tempo marking for that range.</summary>
    public static class TempoMarkingService
    {
        public static string FromBpm(int bpm)
        {
            if (bpm < 40)
            {
                return "Grave";
            }

            if (bpm < 66)
            {
                return "Largo";
            }

            if (bpm < 76)
            {
                return "Adagio";
            }

            if (bpm < 108)
            {
                return "Andante";
            }

            if (bpm < 120)
            {
                return "Moderato";
            }

            if (bpm < 168)
            {
                return "Allegro";
            }

            if (bpm < 200)
            {
                return "Presto";
            }

            return "Prestissimo";
        }
    }
}
