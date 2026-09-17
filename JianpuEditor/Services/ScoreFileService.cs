using System;
using System.Collections.Generic;
using System.IO;
using JianpuEditor.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JianpuEditor.Services
{
    public static class ScoreFileService
    {
        public static void Save(JianpuScore score, string path)
        {
            var json = JsonConvert.SerializeObject(score, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public static JianpuScore Load(string path)
        {
            var json = File.ReadAllText(path);
            var token = JToken.Parse(json);

            if (token["Measures"] != null)
            {
                var settings = new JsonSerializerSettings
                {
                    // If the default Measures list already has entries, Auto would append rather than replace,
                    // resulting in an extra empty measure after loading.
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                };
                var score = JsonConvert.DeserializeObject<JianpuScore>(json, settings) ?? CreateEmptyScore();
                ImportLegacyChordMarkers(score, token["Measures"] as JArray);
                MelodyChordService.NormalizeScore(score);
                ChordMarkerService.NormalizeScore(score);
                LyricSyllableService.NormalizeScore(score);
                OrnamentService.NormalizeScore(score);
                return score;
            }

            var legacyScore = MigrateLegacyScore(token);
            MelodyChordService.NormalizeScore(legacyScore);
            ChordMarkerService.NormalizeScore(legacyScore);
            LyricSyllableService.NormalizeScore(legacyScore);
            OrnamentService.NormalizeScore(legacyScore);
            return legacyScore;
        }

        private static void ImportLegacyChordMarkers(JianpuScore score, JArray measuresToken)
        {
            if (score?.Measures == null || measuresToken == null)
            {
                return;
            }

            var count = Math.Min(measuresToken.Count, score.Measures.Count);
            for (var i = 0; i < count; i++)
            {
                var legacyText = measuresToken[i].Value<string>("SecondaryText");
                if (!string.IsNullOrWhiteSpace(legacyText))
                {
                    ChordMarkerService.ImportLegacyChordText(score.Measures[i], legacyText);
                }
            }
        }

        private static JianpuScore MigrateLegacyScore(JToken token)
        {
            var score = new JianpuScore
            {
                Title = token.Value<string>("Title") ?? "Untitled Score",
                KeySignature = token.Value<string>("KeySignature") ?? "1=C",
                TimeSignature = token.Value<string>("TimeSignature") ?? "4/4",
                Tempo = token.Value<string>("Tempo") ?? "Moderato",
                Bpm = token.Value<int?>("Bpm") ?? 120,
                Composer = token.Value<string>("Composer") ?? string.Empty,
                Measures = new List<JianpuMeasure>()
            };

            var current = new JianpuMeasure();
            score.Measures.Add(current);

            foreach (var noteToken in token["Notes"] ?? new JArray())
            {
                var typeValue = noteToken.Value<int?>("Type") ?? 0;
                if (typeValue == 2)
                {
                    current = new JianpuMeasure();
                    score.Measures.Add(current);
                    continue;
                }

                current.MelodyNotes.Add(noteToken.ToObject<JianpuNote>());
            }

            if (score.Measures.Count == 0)
            {
                score.Measures.Add(new JianpuMeasure());
            }

            return score;
        }

        private static JianpuScore CreateEmptyScore()
        {
            return new JianpuScore();
        }
    }
}
