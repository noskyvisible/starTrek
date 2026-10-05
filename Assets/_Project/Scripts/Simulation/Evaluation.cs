using System;
using System.Collections.Generic;

namespace StarTrek.Simulation
{
    /// <summary>One line of the evaluation: a grade (or none) and what the board noticed.</summary>
    public sealed class EvaluationCategory
    {
        public string Title;
        /// <summary>0..4, or -1 for "not graded".</summary>
        public int Grade;
        public readonly List<string> Notes = new List<string>();

        public string GradeName => Evaluation.GradeName(Grade);
    }

    /// <summary>
    /// The Starfleet Academy evaluation of a Kobayashi Maru run (GAME_PROMPT §8, beat 7): grades
    /// decisions, crew care, composure and the final choice. Never "victory".
    /// </summary>
    public sealed class Evaluation
    {
        public readonly List<EvaluationCategory> Categories = new List<EvaluationCategory>();
        public string OutcomeTitle;
        public string OutcomeText;
        public int OverallGrade;
        public string InstructorRemark;
        /// <summary>Set when the cadet reprogrammed the simulation.</summary>
        public string IntegrityNote;

        public string OverallGradeName => GradeName(OverallGrade);

        public static string GradeName(int grade)
        {
            switch (grade)
            {
                case 4: return "Outstanding";
                case 3: return "Commendable";
                case 2: return "Satisfactory";
                case 1: return "Needs improvement";
                case 0: return "Unsatisfactory";
                default: return "Not graded";
            }
        }

        public static Evaluation Build(MissionRecord r)
        {
            var e = new Evaluation();
            bool declined = r.Outcome == MissionOutcome.DeclinedRescue;
            bool fought = r.AmbushTime >= 0;

            // 1. The decision
            var decision = new EvaluationCategory { Title = "The decision" };
            bool prudent = r.CalledStarfleet || r.SweptForCloaks || r.ScannedMaru || r.ScannedLifeSigns;
            if (declined)
            {
                decision.Grade = prudent ? 3 : 2;
                decision.Notes.Add("Kept to the treaty and stayed out of the Neutral Zone. A defensible call.");
                decision.Notes.Add($"The {Mission.SoulsAboard} people aboard the Kobayashi Maru were left waiting for help.");
            }
            else if (r.EnteredZoneTime >= 0)
            {
                float score = 3f + (prudent ? 1f : 0f) - (r.ShieldsUpEnteringZone ? 0f : 1f);
                decision.Grade = Clamp(score);
                double delay = r.DistressTime >= 0 ? r.EnteredZoneTime - r.DistressTime : -1;
                decision.Notes.Add(delay >= 0
                    ? $"Crossed into the Neutral Zone {Seconds(delay)} after the distress call."
                    : "Crossed into the Neutral Zone to answer the distress call.");
                if (r.CalledStarfleet) decision.Notes.Add("Informed Starfleet Command before acting.");
                if (r.ScannedMaru || r.ScannedLifeSigns) decision.Notes.Add("Confirmed the situation with sensors first.");
                if (r.SweptForCloaks) decision.Notes.Add("Swept for cloaked ships. The board noticed.");
                if (!r.ShieldsUpEnteringZone) decision.Notes.Add("Entered hostile space with the shields down.");
            }
            else
            {
                decision.Grade = 1;
                decision.Notes.Add("The test ended before a decision was made.");
            }
            e.Categories.Add(decision);

            // 2. Care of the crew and the Maru
            var care = new EvaluationCategory { Title = "Care of the crew and the Maru" };
            if (declined)
            {
                care.Grade = 2;
                care.Notes.Add("Your own crew came home safe. No one aboard the Maru did.");
            }
            else
            {
                float rescued = r.SurvivorsRescued / (float)Mission.SoulsAboard;
                float score = 1f + rescued * 2.5f + (r.AwayTeamSent ? 0.5f : 0f) - r.CrewLost * 0.5f;
                care.Grade = Clamp(score);
                care.Notes.Add($"{r.SurvivorsRescued} of {Mission.SoulsAboard} survivors brought aboard.");
                if (r.AwayTeamSent) care.Notes.Add(r.LeakSealed ? "Led the away team that sealed the Maru's fuel leak." : "Led an away team to the Maru.");
                if (r.TransportedUnderFire) care.Notes.Add("Kept the shields down under fire to finish the transport. A real risk to your own ship.");
                if (r.CrewLost > 0) care.Notes.Add($"{r.CrewLost} bridge officer{(r.CrewLost == 1 ? "" : "s")} lost.");
                else if (fought) care.Notes.Add("No bridge officers lost.");
            }
            e.Categories.Add(care);

            // 3. Composure under fire
            var composure = new EvaluationCategory { Title = "Composure under fire" };
            float refusals = r.OrdersGiven > 0 ? r.OrdersRefused / (float)r.OrdersGiven : 0f;
            if (!fought)
            {
                composure.Grade = refusals > 0.35f ? 1 : 2;
                composure.Notes.Add("Never came under fire.");
            }
            else
            {
                float score = r.DefenceDelay < 0 ? 1f : r.DefenceDelay <= 5 ? 4f : r.DefenceDelay <= 12 ? 3f : r.DefenceDelay <= 25 ? 2f : 1f;
                if (refusals > 0.35f) score -= 1f;
                int boarders = r.BoardersStunned + r.BoardersKilled;
                if (boarders > 0) score += 0.5f;
                if (r.ChoiceSeconds >= 0 && r.ChoiceSeconds <= 20) score += 0.5f;
                composure.Grade = Clamp(score);
                composure.Notes.Add(r.DefenceDelay < 0
                    ? "Never raised the shields or called red alert during the ambush."
                    : r.DefenceDelay <= 0.01 ? "Shields were already up when the Klingons appeared."
                    : $"Defences up {Seconds(r.DefenceDelay)} after the Klingons de-cloaked.");
                if (r.CruisersDestroyed > 0) composure.Notes.Add($"{r.CruisersDestroyed} Klingon cruiser{(r.CruisersDestroyed == 1 ? "" : "s")} destroyed.");
                if (boarders > 0)
                    composure.Notes.Add(r.BoardersKilled == 0 ? $"Repelled {boarders} boarders with phasers on stun."
                        : r.BoardersStunned == 0 ? $"Killed {boarders} boarders."
                        : $"Repelled {boarders} boarders ({r.BoardersKilled} killed).");
                if (refusals > 0.35f) composure.Notes.Add("Many orders the crew couldn't carry out. Know your ship.");
            }
            composure.Notes.Add($"{r.OrdersGiven} orders given, {r.OrdersRefused} refused.");
            e.Categories.Add(composure);

            // 4. The final choice: described, never graded. There is no right answer.
            var choice = new EvaluationCategory { Title = "The final choice", Grade = -1 };
            Describe(r, out e.OutcomeTitle, out e.OutcomeText);
            choice.Notes.Add(e.OutcomeText);
            if (r.ChoiceSeconds >= 0) choice.Notes.Add($"Chosen with {Seconds(Mission.BreachSeconds - r.ChoiceSeconds)} left before the core breach.");
            e.Categories.Add(choice);

            int sum = 0, count = 0;
            foreach (var c in e.Categories)
                if (c.Grade >= 0)
                {
                    sum += c.Grade;
                    count++;
                }
            e.OverallGrade = count > 0 ? (int)Math.Round(sum / (double)count, MidpointRounding.AwayFromZero) : -1;

            if (r.Reprogrammed)
            {
                e.IntegrityNote = "SIMULATION INTEGRITY: COMPROMISED. The test's programming was altered before it began.";
                e.InstructorRemark = r.Outcome == MissionOutcome.MaruSaved
                    ? "You changed the conditions of the test, and you saved the Maru. Some on the board will call it cheating. Others will call it original thinking. You'll be asked to explain yourself in the morning, Cadet."
                    : "You altered the simulation, and it still didn't save you. The board will want to know why you tried.";
            }
            else
                e.InstructorRemark = "The Kobayashi Maru isn't a test of tactics. No one beats it. It shows us how a captain faces a situation they cannot win. That's what we graded.";
            return e;
        }

        static void Describe(MissionRecord r, out string title, out string text)
        {
            switch (r.Outcome)
            {
                case MissionOutcome.DeclinedRescue:
                    title = "Declined the rescue";
                    text = "Never crossed into the Neutral Zone. The treaty held, and the Maru was lost.";
                    break;
                case MissionOutcome.ShipDestroyed:
                    title = "Fought to the end";
                    text = "Refused to abandon the Maru and refused to surrender. The ship was lost with all hands.";
                    break;
                case MissionOutcome.CaptainKilled:
                    title = "Fell defending the bridge";
                    text = "The captain was killed when the Klingons boarded the bridge.";
                    break;
                case MissionOutcome.Surrendered:
                    title = "Surrendered";
                    text = "Gave up the ship so the crew would live, as prisoners of the Klingon Empire.";
                    break;
                case MissionOutcome.SelfDestructed:
                    title = "Auto-destruct";
                    text = "Destroyed the ship rather than let it be taken, and took a Klingon cruiser with it.";
                    break;
                case MissionOutcome.Retreated:
                    title = "Retreated";
                    text = r.SurvivorsRescued > 0
                        ? $"Escaped at warp with {r.SurvivorsRescued} survivors aboard, and left the rest of the Maru behind."
                        : "Escaped at warp and left the Kobayashi Maru behind.";
                    break;
                case MissionOutcome.MaruSaved:
                    title = "The Kobayashi Maru was saved";
                    text = "Destroyed the Klingon force and saved the freighter. No cadet has done that before.";
                    break;
                default:
                    title = "Incomplete";
                    text = "The simulation ended before the test was finished.";
                    break;
            }
        }

        static int Clamp(float score) => Math.Max(0, Math.Min(4, (int)Math.Round(score, MidpointRounding.AwayFromZero)));

        static string Seconds(double s) => s < 90 ? $"{Math.Max(0, s):0} seconds" : $"{s / 60.0:0.#} minutes";
    }
}
