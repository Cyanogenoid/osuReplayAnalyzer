using System;
using System.IO;
using BMAPI.v1;
using ReplayAPI;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace osuDodgyMomentsFinder
{
    public class Program
    {
        private static MainControlFrame settings = new MainControlFrame();

        public static Dictionary<string, Beatmap> processOsuDB(OsuDbAPI.OsuDbFile osuDB, string songsFolder)
        {
            Dictionary<string, Beatmap> dict = new Dictionary<string, Beatmap>();
            foreach(OsuDbAPI.Beatmap dbBeatmap in osuDB.Beatmaps)
            {
                string beatmapPath = songsFolder + dbBeatmap.FolderName + "\\" + dbBeatmap.OsuFile;
                Beatmap map = new Beatmap(beatmapPath);
                dict.Add(map.BeatmapHash, map);
            }

            return dict;
        }


        public static List<KeyValuePair<Beatmap, Replay>> AssociateMapsReplays(string basepath)
        {
            if (!basepath.EndsWith("/")) {
                basepath = basepath + "/";
            }
            DirectoryInfo directory = new DirectoryInfo(basepath);
            FileInfo[] files = directory.GetFiles();

            var replaysFiles = new List<string>();
            string mapPath = null;
            foreach(FileInfo file in files)
            {
                if(file.Extension == ".b64")
                {
                    replaysFiles.Add(basepath + file.Name);
                }
                if(file.Extension == ".osu")
                {
                    if (mapPath != null)
                    {
                        Console.WriteLine("WARNING: more than one .osu file found in " + basepath);
                    }
                    mapPath = basepath + file.Name;
                }
            }
			Beatmap beatmap = new Beatmap(mapPath);

			JavaScriptSerializer serializer = new JavaScriptSerializer();
			string jsonInfo= File.ReadAllText("../beatmap-scores.json");
			var scores = serializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonInfo);
			var beatmapScores = scores[beatmap.BeatmapID.ToString()];

			var replays = replaysFiles.ConvertAll((path) => new Replay(path, true, beatmapScores[Path.GetFileNameWithoutExtension(path)]));



            var result = new List<KeyValuePair<Beatmap, Replay>>();
            var dict = new Dictionary<string, Beatmap>();

            foreach(var replay in replays)
            {
				Console.WriteLine (replay.Filename);
                result.Add(new KeyValuePair<Beatmap, Replay>(beatmap, replay));
            }

            return result;
        }

        public static StringBuilder ReplayAnalyzing(Beatmap beatmap, Replay replay, bool onlyMainInfo = false)
        {
            StringBuilder sb = new StringBuilder();
			int hasHR = Convert.ToInt32(replay.Mods.HasFlag (Mods.HardRock)) - Convert.ToInt32(replay.Mods.HasFlag(Mods.Easy));
			int hasDT = Convert.ToInt32(replay.Mods.HasFlag(Mods.DoubleTime)) - Convert.ToInt32(replay.Mods.HasFlag(Mods.HalfTime));

			sb.Append(hasHR);
			sb.Append(",");
			sb.Append(hasDT);

            ReplayAnalyzer analyzer = new ReplayAnalyzer(beatmap, replay);
			sb.AppendLine(analyzer.hitPositionInfo());

			// TODO test out how stacking works when applying HR, if that doesn't just flip the way notes
			// are effectively stacked, probably need to generate different beatmaps when HR is active

            return sb;
        }


        public static void ReplayAnalyzingAll(string path)
        {
            var pairs = AssociateMapsReplays(path);

            string res = "";
            StringBuilder sb = new StringBuilder();
			sb.Append(pairs[0].Key.HitObjectsToString());
            foreach(var pair in pairs)
            {
                string result = ReplayAnalyzing(pair.Key, pair.Value).ToString();
                sb.Append(result);
            }
            File.WriteAllText("log.txt", sb.ToString());
        }

        public static string ReplayAnalyzing(Replay replay)
        {
            var maps = settings.osuDbP.Beatmaps;

            string beatmapPath = "";
            foreach(OsuDbAPI.Beatmap dbBeatmap in maps)
            {
                if(dbBeatmap.Hash == replay.MapHash)
                {
                    beatmapPath = settings.pathSongs + dbBeatmap.FolderName + "/" + dbBeatmap.OsuFile;
                    break;
                }
            }

            Beatmap beatmap = new Beatmap(beatmapPath);

            return ReplayAnalyzing(beatmap, replay).ToString();
        }


        public static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("Welcome the firedigger's replay analyzer. Use one of 3 options");
                Console.WriteLine("-i (path to replay) for getting info about a certain replay");
                Console.WriteLine("-ia for getting info about all replays in the current folder");
                Console.WriteLine("-c for comparing all the replays in the current folder against each other");
                Console.WriteLine("-cr [paths to replays] for comparing the replays from command line args");
                Console.ReadKey();
                return;
            }
            if(args[0] == "-ia")
            {
                ReplayAnalyzingAll(args[1]);
            }
            if(args[0] == "-i")
            {
                Console.WriteLine(ReplayAnalyzing(new Replay(args[1], true, "")));
            }

        }
    }
}
