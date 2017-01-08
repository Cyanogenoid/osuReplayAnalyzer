using BMAPI.v1;
using BMAPI.v1.Events;
using BMAPI.v1.HitObjects;
using ReplayAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace osuDodgyMomentsFinder
{

    /* This class is a list of pair of a clickable object and a replay frame hit
     * Initializing the class is a task of associating every keypress with an object hit
     * After that all the procedural checks on suspicious moment become possible
     */
    public class ReplayAnalyzer
    {
        //The beatmap
        private Beatmap beatmap;

        //The replay
        private Replay replay;

        //Circle radius
        private double circleRadius;

        //hit time window
        private double hitTimeWindow;

        //hit time window
        private double approachTimeWindow;


        //The list of pair of a <hit, object hit>
        public List<HitFrame> hits
        {
            get; private set;
        }
        public List<HitFrame> attemptedHits
        {
            get; private set;
        }

	    private List<CircleObject> misses
        {
            get; set;
        }

	    private List<CircleObject> effortlessMisses
        {
            get; set;
        }

	    private List<BreakEvent> breaks
        {
            get; set;
        }

	    private List<SpinnerObject> spinners
        {
            get; set;
        }

	    private List<ClickFrame> extraHits
        {
            get; set;
        }

        private void applyHardrock()
        {
            replay.flip();
            beatmap.applyHardRock();
        }

        private void selectBreaks()
        {
            foreach(var event1 in this.beatmap.Events)
            {
                if(event1.GetType() == typeof(BreakEvent))
                {
                    this.breaks.Add((BreakEvent)event1);
                }
            }
        }

        private void selectSpinners()
        {
            foreach (var obj in this.beatmap.HitObjects)
            {
                if (obj.Type.HasFlag(HitObjectType.Spinner))
                {
                    this.spinners.Add((SpinnerObject)obj);
                }
            }
        }

        private void associateHits()
        {
            int keyIndex = 0;
            Keys lastKey;
            KeyCounter keyCounter = new KeyCounter();

            if((replay.Mods & Mods.HardRock) > 0)
            {
                applyHardrock();
            }

            int breakIndex = 0;
            int combo = 0;

            for(int i = 0; i < beatmap.HitObjects.Count; ++i)
            {
                CircleObject note = beatmap.HitObjects[i];
                bool noteHitFlag = false;
                bool noteAttemptedHitFlag = false;

                if ((note.Type.HasFlag(HitObjectType.Spinner)))
                    continue;

                for(int j = keyIndex; j < replay.ReplayFrames.Count; ++j)
                {
                    ReplayFrame frame = replay.ReplayFrames[j];
                    lastKey = j > 0 ? replay.ReplayFrames[j - 1].Keys : Keys.None;

                    Keys pressedKey = getKey(lastKey, frame.Keys);

                    if(breakIndex < breaks.Count && frame.Time > breaks[breakIndex].EndTime)
                    {
                        ++breakIndex;
                    }

                    if(frame.Time >= beatmap.HitObjects[0].StartTime - hitTimeWindow && (breakIndex >= breaks.Count || frame.Time < this.breaks[breakIndex].StartTime - hitTimeWindow))
                    {
                        keyCounter.Update(lastKey, frame.Keys);
                    }

                    frame.keyCounter = new KeyCounter(keyCounter);

                    if (frame.Time - note.StartTime > hitTimeWindow)
                        break;

                    if (pressedKey > 0 && Math.Abs(frame.Time - note.StartTime) <= hitTimeWindow)
                    {
                        if (note.ContainsPoint(new BMAPI.Point2(frame.X, frame.Y)))
                        {
                            if (noteAttemptedHitFlag)
                            {
                                attemptedHits.RemoveAt (attemptedHits.Count - 1);
                            }
                            noteAttemptedHitFlag = true;
                            ++combo;
                            frame.combo = combo;
                            noteHitFlag = true;
                            HitFrame hitFrame = new HitFrame(note, frame, pressedKey);
                            hits.Add(hitFrame);
                            lastKey = frame.Keys;
                            keyIndex = j + 1;
                            break;
                        }
                        else
                        {
                            if (Utils.dist(note.Location.X, note.Location.Y, frame.X, frame.Y) > 150)
                            {
                                extraHits.Add(new ClickFrame(frame, getKey(lastKey, frame.Keys)));
                            }
                            else
                            {
                                if (noteAttemptedHitFlag)
                                {
                                    attemptedHits.RemoveAt (attemptedHits.Count - 1);
                                }
                                noteAttemptedHitFlag = true;
                                attemptedHits.Add(new HitFrame(note, frame, pressedKey));
                            }
                        }
                    }
                    if (pressedKey > 0 && Math.Abs(frame.Time - note.StartTime) <= 3 * hitTimeWindow && note.ContainsPoint(new BMAPI.Point2(frame.X, frame.Y)))
                    {
                        if (noteAttemptedHitFlag)
                        {
                            attemptedHits.RemoveAt (attemptedHits.Count - 1);
                        }
                        noteAttemptedHitFlag = true;
                        attemptedHits.Add(new HitFrame(note, frame, pressedKey));
                    }

                    lastKey = frame.Keys;

                    frame.combo = combo;

                }

                if(!noteHitFlag)
                {
                    misses.Add(note);
                }
                if (!noteAttemptedHitFlag)
                {
                    effortlessMisses.Add(note);
                }
            }
        }

        private Keys getKey(Keys last, Keys current)
        {
            Keys res = Keys.None;
            if(!last.HasFlag(Keys.M1) && current.HasFlag(Keys.M1) && !current.HasFlag(Keys.K1))
                res |= Keys.M1;
            if(!last.HasFlag(Keys.M2) && current.HasFlag(Keys.M2) && !current.HasFlag(Keys.K2))
                res |= Keys.M2;
            if(!last.HasFlag(Keys.K1) && current.HasFlag(Keys.K1))
                res |= Keys.K1 | Keys.M1;
            if(!last.HasFlag(Keys.K2) && current.HasFlag(Keys.K2))
                res |= Keys.K2 | Keys.M2;
            return res;
        }



	    private double calculateAverageFrameTimeDiff()
        {
            return replay.times.ConvertAll(x => x.TimeDiff).Where(x => x > 0 && x < 30).Average();
        }

		private double calculateAverageFrameTimeDiffv2()
		{
			int count = 0;
			int sum = 0;

			for(int i = 1; i < replay.times.Count - 1; i++)
			{
				if(!replay.times[i - 1].Keys.HasFlag(Keys.K1) && !replay.times[i - 1].Keys.HasFlag(Keys.K2) && !replay.times[i - 1].Keys.HasFlag(Keys.M1) && !replay.times[i - 1].Keys.HasFlag(Keys.M2) &&
					!replay.times[i].Keys.HasFlag(Keys.K1) && !replay.times[i].Keys.HasFlag(Keys.K2) && !replay.times[i].Keys.HasFlag(Keys.M1) && !replay.times[i].Keys.HasFlag(Keys.M2) &&
					!replay.times[i + 1].Keys.HasFlag(Keys.K1) && !replay.times[i + 1].Keys.HasFlag(Keys.K2) && !replay.times[i + 1].Keys.HasFlag(Keys.M1) && !replay.times[i + 1].Keys.HasFlag(Keys.M2))
				{
					count++;
					sum += replay.times[i].TimeDiff;
				}
			}

			if(count == 0)
			{
				return -1.0;
			}

			return (double)sum / count;
		}

	    private List<double> speedList()
        {
            return replay.times.ConvertAll(x => x.speed);
        }

	    private List<double> accelerationList()
        {
            return replay.times.ConvertAll(x => x.acceleration);
        }

        public string outputSpeed()
        {
            string res = speedList().Aggregate("", (current, value) => current + (value + ","));
	        return res.Remove(res.Length - 1);
        }

        public string outputAcceleration()
        {
            string res = replay.times.ConvertAll(x => x.acceleration).Aggregate("", (current, value) => current + (value + ","));
	        return res.Remove(res.Length - 1);
        }

        public string outputTime()
        {
            string res = replay.times.ConvertAll(x => x.Time).Aggregate("", (current, value) => current + (value + ","));
	        return res.Remove(res.Length - 1);
        }
			

        public double calcAccelerationVariance()
        {
            return Utils.variance(accelerationList());
        }

        public string outputMisses()
        {
            string res = "";
            this.misses.ForEach((note) => res += "Didn't find the hit for " + note.StartTime);
            return res;
        }

        private double calcTimeWindow(double OD)
        {
            return -12 * OD + 259.5;
        }

        public ReplayAnalyzer(Beatmap beatmap, Replay replay)
        {
            this.beatmap = beatmap;
            this.replay = replay;

            if (!replay.fullLoaded)
                throw new Exception(replay.Filename + " IS NOT FULL");

            multiplier = replay.Mods.HasFlag(Mods.DoubleTime) ? 1.5 : 1;

            circleRadius = beatmap.HitObjects[0].Radius;
            hitTimeWindow = calcTimeWindow(beatmap.OverallDifficulty);

            approachTimeWindow = 1800 - 120 * beatmap.ApproachRate;

            hits = new List<HitFrame>();
            attemptedHits = new List<HitFrame>();
            misses = new List<CircleObject>();
            effortlessMisses = new List<CircleObject>();
            extraHits = new List<ClickFrame>();
            breaks = new List<BreakEvent>();
            spinners = new List<SpinnerObject>();

            selectBreaks();
            selectSpinners();
            associateHits();
        }

		private readonly double multiplier;


		public string hitPositionInfo()
		{
			StringBuilder sb = new StringBuilder();

			int hitsIndex = 0;
            int attemptedIndex = 0;
			foreach (CircleObject note in beatmap.HitObjects)
			{
				if (note.Type.HasFlag(HitObjectType.Spinner))
                    continue;
                if (hitsIndex < hits.Count && note == hits[hitsIndex].note)
                {
                    ReplayFrame frame = hits[hitsIndex].frame;
                    sb.Append("," + frame.X + "," + frame.Y);
                    ++hitsIndex;
                }
                else if (attemptedIndex < attemptedHits.Count && note == attemptedHits[attemptedIndex].note)
                {
                    ReplayFrame frame = attemptedHits[attemptedIndex].frame;
                    sb.Append("," + frame.X + "," + frame.Y);
                    ++attemptedIndex;
                }
				else
				{
					sb.Append(",,");
				}
			}
			return sb.ToString();
		}


    }
}
