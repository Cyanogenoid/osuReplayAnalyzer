﻿using BMAPI.v1;
using osuDodgyMomentsFinder;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ReplayAPI
{
    public class Replay : IDisposable
    {
        // for customizing which replays to flip
        public bool AxisFlip
        {
            get; set;
        }

        public GameModes GameMode;
        public string Filename;
        public int FileFormat;
        public string MapHash;
        public string PlayerName;
        public string ReplayHash;
        public uint TotalScore;
        public UInt16 Count300;
        public UInt16 Count100;
        public UInt16 Count50;
        public UInt16 CountGeki;
        public UInt16 CountKatu;
        public UInt16 CountMiss;
        public UInt16 MaxCombo;
        public bool IsPerfect;
        public Mods Mods;
        public List<LifeFrame> LifeFrames = new List<LifeFrame>();
        public DateTime PlayTime;
        public int ReplayLength;
        public List<ReplayFrame> ReplayFrames = new List<ReplayFrame>();
        public int Seed;

        private MemoryStream replayReader;
        private CultureInfo culture = new CultureInfo("en-US", false);
        private bool headerLoaded;
        public bool fullLoaded { get; private set; }

		public Replay(string replayFile, bool fullLoad, string mods)
        {
            Filename = replayFile;
            byte[] content = System.Convert.FromBase64String(File.ReadAllText(replayFile));
            ReplayLength = content.Length;
            using (replayReader = new MemoryStream(content))
            {
                loadHeader();
				Mods = (Mods)int.Parse(mods);
                if (fullLoad)
                {
                    Load();
                }
            }
            if (fullLoad && !fullLoaded)
                throw new Exception("Replay is not full but requsted to be read full.");
        }

        private Keys parseKeys(string v)
        {
            return (Keys)Enum.Parse(typeof(Keys), v);
        }

        private void loadHeader()
        {
            headerLoaded = true;
        }

        public List<ReplayFrame> times
        {
            get; private set;
        }


        /// <summary>
        /// Loads Metadata if not already loaded and loads Lifedata, Timestamp, Playtime and Clicks.
        /// </summary>
        public void Load()
        {
            if (!headerLoaded)
                loadHeader();
            if (fullLoaded)
                return;

            //Data
            if (ReplayLength > 0)
            {
                int lastTime = 0;
                using (MemoryStream codedStream = LZMACoder.Decompress(replayReader))
                using (StreamReader sr = new StreamReader(codedStream))
                {
                    foreach (string frame in sr.ReadToEnd().Split(','))
                    {
                        if (string.IsNullOrEmpty(frame))
                            continue;

                        string[] split = frame.Split('|');
                        if (split.Length < 4)
                            continue;

                        if (split[0] == "-12345")
                        {
                            Seed = int.Parse(split[3], culture);
                            continue;
                        }

                        ReplayFrames.Add(new ReplayFrame()
                        {
                            TimeDiff = int.Parse(split[0], culture),
                            Time = int.Parse(split[0], culture) + lastTime,
                            X = float.Parse(split[1], culture),
                            Y = float.Parse(split[2], culture),
                            Keys = parseKeys(split[3])
                        });
                        lastTime = ReplayFrames[ReplayFrames.Count - 1].Time;
                    }
                }
                fullLoaded = true;
            }

            ReplayFrames.RemoveRange(0, 3);

            //Todo: There are some extra bytes here
        }

        public void Save(string file)
        {
            using (BinaryWriter bw = new BinaryWriter(new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                //Header
                bw.Write((byte)GameMode);
                bw.Write(FileFormat);
                bw.WriteNullableString(MapHash);
                bw.WriteNullableString(PlayerName);
                bw.WriteNullableString(ReplayHash);
                bw.Write(Count300);
                bw.Write(Count100);
                bw.Write(Count50);
                bw.Write(CountGeki);
                bw.Write(CountKatu);
                bw.Write(CountMiss);
                bw.Write(TotalScore);
                bw.Write((UInt16)MaxCombo);
                bw.Write(IsPerfect);
                bw.Write((int)Mods);

                //Life
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < LifeFrames.Count; i++)
                    sb.AppendFormat("{0}|{1},", LifeFrames[i].Time.ToString(culture), LifeFrames[i].Percentage.ToString(culture));
                bw.WriteNullableString(sb.ToString());

                bw.Write(PlayTime.ToUniversalTime().Ticks);

                //Data
                if (ReplayFrames.Count == 0)
                    bw.Write(0);
                else
                {
                    sb.Clear();
                    for (int i = 0; i < ReplayFrames.Count; i++)
                        sb.AppendFormat("{0}|{1}|{2}|{3},", ReplayFrames[i].TimeDiff.ToString(culture), ReplayFrames[i].X.ToString(culture), ReplayFrames[i].Y.ToString(culture), (int)ReplayFrames[i].Keys);
                    sb.AppendFormat("{0}|{1}|{2}|{3},", -12345, 0, 0, Seed);
                    byte[] rawBytes = Encoding.ASCII.GetBytes(sb.ToString());
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(rawBytes, 0, rawBytes.Length);

                        MemoryStream codedStream = LZMACoder.Compress(ms);

                        byte[] rawBytesCompressed = new byte[codedStream.Length];
                        codedStream.Read(rawBytesCompressed, 0, rawBytesCompressed.Length);
                        bw.Write(rawBytesCompressed.Length - 8);
                        bw.Write(rawBytesCompressed);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool state)
        {
            if (replayReader != null)
                replayReader.Close();
            ReplayFrames.Clear();
            LifeFrames.Clear();
        }

        public void flip()
        {
            AxisFlip = !AxisFlip;
            ReplayFrames.ForEach((t) => t.Y = 384 - t.Y);
        }

        public override string ToString()
        {
            return this.PlayerName + " +" + Mods.ToString() + " on " + this.PlayTime;
        }

        public string SaveText(Beatmap map = null)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(ToString());
            sb.AppendLine("Count 300: " + Count300);
            sb.AppendLine("Count 100: " + Count100);
            sb.AppendLine("Count 50: " + Count50);

            sb.AppendLine("Count Geki: " + CountGeki);
            sb.AppendLine("Count Katu: " + CountKatu);
            sb.AppendLine("Count Miss: " + CountMiss);

            sb.AppendLine("Total Score: " + TotalScore);
            sb.AppendLine("Max Combo: " + MaxCombo);
            sb.AppendLine("Is fullcombo: " + IsPerfect);

            sb.AppendLine("Mods: " + Mods.ToString());

            List<HitFrame> hits = null;
            List<HitFrame> attemptedHits = null;
            if (!ReferenceEquals(map, null))
            {
                var analyzer = new ReplayAnalyzer(map, this);
                hits = analyzer.hits;
                attemptedHits = analyzer.attemptedHits;
            }

            int hitIndex = 0;
            int attemptedHitIndex = 0;
            for (int i = 0; i < ReplayFrames.Count; i++)
            {
                if (!ReferenceEquals(hits, null) && hitIndex < hits.Count && hits[hitIndex].frame.Time == ReplayFrames[i].Time)
                {
                    sb.AppendLine(ReplayFrames[i].ToString() + " " + hits[hitIndex].ToString());
                    ++hitIndex;
                    continue;
                }
                if (!ReferenceEquals(attemptedHits, null) && attemptedHitIndex < attemptedHits.Count && attemptedHits[attemptedHitIndex].frame.Time == ReplayFrames[i].Time)
                {
                    sb.AppendLine(ReplayFrames[i].ToString() + " " + attemptedHits[attemptedHitIndex].note.ToString());
                    ++attemptedHitIndex;
                    continue;
                }
                sb.AppendLine(ReplayFrames[i].ToString());
            }

            return sb.ToString();
        }

        public bool IsPass()
        {
            return (!this.Mods.HasFlag(Mods.NoFail)) || LifeFrames.All((x) => x.Percentage > 0);
        }
    }
}
