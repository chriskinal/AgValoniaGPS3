// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.YouTurn;
using System;
using static AgValoniaGPS.Models.Base.GeometryMath;

namespace AgValoniaGPS.Services.YouTurn
{
    /// <summary>
    /// Service for calculating steering guidance while following a U-turn path.
    /// Supports both Stanley (steer axle) and Pure Pursuit (pivot axle) algorithms.
    /// </summary>
    public class YouTurnGuidanceService
    {

        /// <summary>
        /// Calculate steering guidance for following a U-turn path.
        /// </summary>
        public YouTurnGuidanceOutput CalculateGuidance(YouTurnGuidanceInput input)
        {
            var output = new YouTurnGuidanceOutput();

            int ptCount = input.TurnPath.Count;
            if (ptCount == 0)
            {
                output.IsTurnComplete = true;
                return output;
            }

            if (input.UseStanley)
            {
                CalculateStanleyGuidance(input, output, ptCount);
            }
            else
            {
                CalculatePurePursuitGuidance(input, output, ptCount);
            }

            return output;
        }

        private void CalculateStanleyGuidance(YouTurnGuidanceInput input, YouTurnGuidanceOutput output, int ptCount)
        {
            Vec3 pivot = input.SteerPosition;

            // Find the closest 2 points to current fix
            double minDistA = double.MaxValue;
            double minDistB = double.MaxValue;
            int A = 0, B = 0;

            for (int t = 0; t < ptCount; t++)
            {
                double dist = ((pivot.Easting - input.TurnPath[t].Easting) * (pivot.Easting - input.TurnPath[t].Easting))
                                + ((pivot.Northing - input.TurnPath[t].Northing) * (pivot.Northing - input.TurnPath[t].Northing));
                if (dist < minDistA)
                {
                    minDistB = minDistA;
                    B = A;
                    minDistA = dist;
                    A = t;
                }
                else if (dist < minDistB)
                {
                    minDistB = dist;
                    B = t;
                }
            }

            // Check if too far away - turn complete
            if (minDistA > 16)
            {
                output.IsTurnComplete = true;
                return;
            }

            // Make sure points continue ascending
            if (A > B)
            {
                (B, A) = (A, B);
            }

            // Bounds check
            if (A < 0) A = 0;
            B = A + 1;

            // Return and reset if too far away or end of the line
            if (B >= ptCount - 1)
            {
                output.IsTurnComplete = true;
                return;
            }

            // K-style turn in reverse completes immediately
            if (input.UTurnStyle == 1 && input.IsReverse)
            {
                output.IsTurnComplete = true;
                return;
            }

            // Get the distance from currently active line
            double dx = input.TurnPath[B].Easting - input.TurnPath[A].Easting;
            double dz = input.TurnPath[B].Northing - input.TurnPath[A].Northing;
            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
            {
                output.IsTurnComplete = false;
                return;
            }

            double abHeading = input.TurnPath[A].Heading;

            // How far from current line is steer point (90 degrees from steer position)
            double distanceFromCurrentLine = ((dz * pivot.Easting) - (dx * pivot.Northing) + (input.TurnPath[B].Easting
                        * input.TurnPath[A].Northing) - (input.TurnPath[B].Northing * input.TurnPath[A].Easting))
                            / Math.Sqrt((dz * dz) + (dx * dx));

            // Calc point on line closest to current position and 90 degrees to segment heading
            double U = (((pivot.Easting - input.TurnPath[A].Easting) * dx)
                        + ((pivot.Northing - input.TurnPath[A].Northing) * dz))
                        / ((dx * dx) + (dz * dz));

            // Critical point used as start for the uturn path
            double rEast = input.TurnPath[A].Easting + (U * dx);
            double rNorth = input.TurnPath[A].Northing + (U * dz);

            // The first part of stanley is to extract heading error
            double abFixHeadingDelta = (pivot.Heading - abHeading);

            // Fix the circular error - get it from -Pi/2 to Pi/2
            if (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Math.PI;
            else if (abFixHeadingDelta < -Math.PI) abFixHeadingDelta += Math.PI;
            if (abFixHeadingDelta > PIBy2) abFixHeadingDelta -= Math.PI;
            else if (abFixHeadingDelta < -PIBy2) abFixHeadingDelta += Math.PI;

            if (input.IsReverse) abFixHeadingDelta *= -1;

            // Normally set to 1, less than unity gives less heading error
            abFixHeadingDelta *= input.StanleyHeadingErrorGain;
            if (abFixHeadingDelta > 0.74) abFixHeadingDelta = 0.74;
            if (abFixHeadingDelta < -0.74) abFixHeadingDelta = -0.74;

            // The non linear distance error part of stanley
            double steerAngle = Math.Atan((distanceFromCurrentLine * input.StanleyDistanceErrorGain) / ((input.AvgSpeed * 0.277777) + 1));

            // Clamp it to max 42 degrees
            if (steerAngle > 0.74) steerAngle = 0.74;
            if (steerAngle < -0.74) steerAngle = -0.74;

            // Add them up and clamp to max in vehicle settings
            steerAngle = ToDegrees((steerAngle + abFixHeadingDelta * input.UTurnCompensation) * -1.0);
            if (steerAngle < -input.MaxSteerAngle) steerAngle = -input.MaxSteerAngle;
            if (steerAngle > input.MaxSteerAngle) steerAngle = input.MaxSteerAngle;

            // Output
            output.IsTurnComplete = false;
            output.DistanceFromCurrentLine = distanceFromCurrentLine;
            output.REast = rEast;
            output.RNorth = rNorth;
            output.SteerAngle = steerAngle;
            output.PointA = A;
            output.PointB = B;
            output.ModeActualXTE = distanceFromCurrentLine;
            output.GuidanceLineDistanceOff = (short)Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);
            output.GuidanceLineSteerAngle = (short)(steerAngle * 100);
            output.PathCount = ptCount - B;
        }

        private void CalculatePurePursuitGuidance(YouTurnGuidanceInput input, YouTurnGuidanceOutput output, int ptCount)
        {
            Vec3 pivot = input.PivotPosition;

            // Find the closest 2 points to current fix
            double minDistA = double.MaxValue;
            double minDistB = double.MaxValue;
            int A = 0, B = 0;

            for (int t = 0; t < ptCount; t++)
            {
                double dist = ((pivot.Easting - input.TurnPath[t].Easting) * (pivot.Easting - input.TurnPath[t].Easting))
                                + ((pivot.Northing - input.TurnPath[t].Northing) * (pivot.Northing - input.TurnPath[t].Northing));
                if (dist < minDistA)
                {
                    minDistB = minDistA;
                    B = A;
                    minDistA = dist;
                    A = t;
                }
                else if (dist < minDistB)
                {
                    minDistB = dist;
                    B = t;
                }
            }

            // Off-path safety net: tractor abandoned the path entirely.
            // Mirrors the Stanley check at line 85 (squared > 16 = > 4m off-path).
            // The previous past-end heuristics (`distancePiv > 2`, `B >= ptCount-1
            // && A > halfwayPoint`) bailed before the lookahead-walk could run,
            // zeroing the goal point upstream and freezing the goal dot at the
            // path end while the tractor was still completing the headland
            // traverse — same failure mode as #337, just an earlier exit.
            // Turn completion at the end of the arc is owned by
            // YouTurnStateMachine.Tick's closest-approach check.
            if (minDistA > 16)
            {
                output.IsTurnComplete = true;
                return;
            }

            // Make sure points continue ascending
            if (A > B)
            {
                (B, A) = (A, B);
            }

            // Omega-fold disambiguation: when the closest-point search picks
            // two non-adjacent indices, the path is curling back on itself
            // and the pivot is sitting in the fold where the entry leg and
            // exit leg of an omega-shaped U-turn come physically close.
            // The legacy "B = A+1" clamp picks whichever of the two close
            // legs has the lower index — that's the ENTRY leg, anti-tangent
            // to the pivot's actual heading on the EXIT leg. The pure-
            // pursuit controller then steers full-lock anti-forward (the
            // user-reported "drive over the path" at end of U-turn).
            //
            // Instead, when A and B are non-adjacent, treat each candidate
            // as the anchor of its own one-step-forward segment and pick
            // whichever segment's tangent better matches the pivot's
            // heading vector. Then enforce B = anchor + 1 as before.
            if (B != A + 1 && A + 1 < ptCount && ptCount >= 3)
            {
                int candA = A;
                int candB = B;

                // Build the forward segment from each candidate. If a
                // candidate is the last point, walk back one — there's only
                // a previous segment available.
                (int sa, int sb) SegFor(int idx) =>
                    idx + 1 < ptCount ? (idx, idx + 1) : (idx - 1, idx);

                var (saA, sbA) = SegFor(candA);
                var (saB, sbB) = SegFor(candB);

                double tanAx = input.TurnPath[sbA].Easting - input.TurnPath[saA].Easting;
                double tanAz = input.TurnPath[sbA].Northing - input.TurnPath[saA].Northing;
                double tanBx = input.TurnPath[sbB].Easting - input.TurnPath[saB].Easting;
                double tanBz = input.TurnPath[sbB].Northing - input.TurnPath[saB].Northing;

                double pivotDirE = Math.Sin(input.FixHeading);
                double pivotDirN = Math.Cos(input.FixHeading);

                // Use signed (not normalised) dot product — magnitudes are
                // comparable because adjacent path samples have similar
                // spacing. Larger forward-dot = better heading alignment.
                double dotA = tanAx * pivotDirE + tanAz * pivotDirN;
                double dotB = tanBx * pivotDirE + tanBz * pivotDirN;

                // Adopt the chosen candidate's segment endpoints directly —
                // SegFor already handles the last-point case by walking back
                // one (so A,B is a real forward segment even when chosen ==
                // ptCount - 1). Don't blindly set B = A+1 here: that would
                // re-introduce the off-by-one zero-length segment when the
                // anchor is the path's tail.
                bool bWins = dotB > dotA;
                (A, B) = bWins ? (saB, sbB) : (saA, sbA);
            }
            else if (B != A + 1 && A + 1 < ptCount)
            {
                // ptCount < 3 — no real ambiguity, keep the legacy clamp.
                B = A + 1;
            }

            // Get the distance from currently active line
            double dx = input.TurnPath[B].Easting - input.TurnPath[A].Easting;
            double dz = input.TurnPath[B].Northing - input.TurnPath[A].Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
            {
                output.IsTurnComplete = false;
                return;
            }

            // How far from current line is fix
            double distanceFromCurrentLine = ((dz * pivot.Easting) - (dx * pivot.Northing) + (input.TurnPath[B].Easting
                        * input.TurnPath[A].Northing) - (input.TurnPath[B].Northing * input.TurnPath[A].Easting))
                            / Math.Sqrt((dz * dz) + (dx * dx));

            // Calc point on line closest to current position
            double U = (((pivot.Easting - input.TurnPath[A].Easting) * dx)
                        + ((pivot.Northing - input.TurnPath[A].Northing) * dz))
                        / ((dx * dx) + (dz * dz));

            double rEast = input.TurnPath[A].Easting + (U * dx);
            double rNorth = input.TurnPath[A].Northing + (U * dz);

            // Sharp turns on you turn - update based on autosteer settings and distance from line
            double goalPointDistance = input.GoalPointDistance;

            bool isHeadingSameWay = true;
            bool reverseHeading = !input.IsReverse;

            int count = reverseHeading ? 1 : -1;
            Vec3 start = new Vec3(rEast, rNorth, 0);
            double distSoFar = 0;
            Vec2 goalPoint = new Vec2();

            for (int i = reverseHeading ? B : A; i < ptCount && i >= 0; i += count)
            {
                // Used for calculating the length squared of next segment
                double tempDist = Distance(start, input.TurnPath[i]);

                // Will we go too far?
                if ((tempDist + distSoFar) > goalPointDistance)
                {
                    double j = (goalPointDistance - distSoFar) / tempDist; // The remainder to yet travel

                    goalPoint.Easting = (((1 - j) * start.Easting) + (j * input.TurnPath[i].Easting));
                    goalPoint.Northing = (((1 - j) * start.Northing) + (j * input.TurnPath[i].Northing));
                    break;
                }
                else distSoFar += tempDist;

                start = input.TurnPath[i];

                if (i == ptCount - 1) // goalPointDistance is longer than remaining u-turn
                {
                    // Lookahead extends past the last point of the U-turn path.
                    // Project the remainder so the goal keeps advancing onto
                    // the next pass direction. Do NOT set IsTurnComplete here —
                    // that would zero the goal upstream and gate off
                    // SetGuidancePoints, freezing the goal-point dot at the
                    // U-turn end while the tractor is still approaching it.
                    // Turn completion is owned by YouTurnStateMachine.Tick's
                    // closest-approach check, which uses the actual tractor
                    // position. Without this projection the steering controller
                    // chases a stationary target during the headland traverse,
                    // producing visible wobble entering the next pass. (#337)
                    //
                    // Forward-of-pivot guard: when the path's endpoint heading
                    // is more than ~90° away from the pivot's actual heading
                    // (tight arc + large lookahead), the path-tangent overshoot
                    // lands the goal BEHIND the pivot and the steering
                    // controller chases anti-tangent — the visible "drive
                    // over the path at apex of tight arcs" symptom. Fall back
                    // to projecting from the endpoint along the pivot's own
                    // heading: by construction the goal is then forward, and
                    // pure pursuit re-anchors onto the path on the next cycle.
                    double remaining = goalPointDistance - distSoFar;
                    var endPt = input.TurnPath[i];

                    double candE = endPt.Easting + Math.Sin(endPt.Heading) * remaining;
                    double candN = endPt.Northing + Math.Cos(endPt.Heading) * remaining;
                    double pivotDirE = Math.Sin(input.FixHeading);
                    double pivotDirN = Math.Cos(input.FixHeading);
                    double forwardDot =
                        (candE - pivot.Easting) * pivotDirE
                        + (candN - pivot.Northing) * pivotDirN;

                    if (forwardDot <= 0)
                    {
                        goalPoint.Easting = endPt.Easting + pivotDirE * remaining;
                        goalPoint.Northing = endPt.Northing + pivotDirN * remaining;
                    }
                    else
                    {
                        goalPoint.Easting = candE;
                        goalPoint.Northing = candN;
                    }
                    break;
                }

                if (input.UTurnStyle == 1 && input.IsReverse)
                {
                    output.IsTurnComplete = true;
                    return;
                }
            }

            // Anti-tangent / collapsed-goal post-walk guard. Independent of
            // which loop branch produced the goal: if the resulting goal is
            // behind the pivot's heading vector OR has collapsed onto the
            // pivot (Euclidean distance much smaller than the configured
            // lookahead), the pure-pursuit math degenerates — steering
            // angle = atan(2·L·sin(α)/D) explodes as D → 0 — and the
            // controller slams the wheels to full lock. The visible
            // "drive over" symptom comes in two flavours:
            //
            //   (a) Anti-tangent on an omega fold: lookahead walks
            //       around a tight loop and lands on a far segment whose
            //       chord-to-pivot is anti-aligned with pivot heading.
            //       Symptom: forward_dot decays smoothly +4 → −3 over
            //       ~15 cycles while heading rotates. (v8 / v10 dumps.)
            //
            //   (b) Collapsed on path end: pivot reaches the path's last
            //       segment, the walk can't advance past index ptCount-1,
            //       so as the pivot keeps driving forward the goal stays
            //       at the path endpoint while the chord-distance shrinks
            //       to zero. forward_dot stays positive but tiny.
            //       Symptom: gd decays smoothly 4 → 0 over 8 cycles, then
            //       a single +35° steer spike on the cycle where D
            //       crosses ~0.3 m. (v11 dump row 483, v12 dump row 384.)
            //
            // Both cases are caught by re-projecting from the pivot along
            // its own heading at the configured lookahead distance. Goal
            // is then guaranteed-forward and goal-distance == lookahead by
            // construction. Pure pursuit re-anchors onto the path the
            // next cycle as the pivot rotates and/or the
            // YouTurnStateMachine completion detector fires.
            //
            // The collapsed-goal threshold is `goalPointDistance × 0.5`:
            // above that, the controller's pure-pursuit math is stable;
            // below it, |steer| starts growing super-linearly with the
            // shrinking D. Calibrated against the v12 dump where the
            // first dangerous cycle had gd=1.794 (still safe — steer
            // -1.6°) and the spike-cycle had gd=0.322 (steer -31.9°);
            // 0.5 × 4 m = 2 m sits cleanly in the safety margin.
            {
                double pivotDirE = Math.Sin(input.FixHeading);
                double pivotDirN = Math.Cos(input.FixHeading);
                double goalFwd =
                    (goalPoint.Easting - pivot.Easting) * pivotDirE
                    + (goalPoint.Northing - pivot.Northing) * pivotDirN;
                double goalDx = goalPoint.Easting - pivot.Easting;
                double goalDy = goalPoint.Northing - pivot.Northing;
                double goalDist = Math.Sqrt(goalDx * goalDx + goalDy * goalDy);

                bool antiTangent = goalFwd < 0;
                bool collapsed = goalDist < goalPointDistance * 0.5;

                if (antiTangent || collapsed)
                {
                    goalPoint.Easting = pivot.Easting + pivotDirE * goalPointDistance;
                    goalPoint.Northing = pivot.Northing + pivotDirN * goalPointDistance;
                    output.AntiTangentGuardFired = true;
                }
            }

            // Calc "D" the distance from pivot axle to lookahead point
            double goalPointDistanceSquared = DistanceSquared(goalPoint.Northing, goalPoint.Easting, pivot.Northing, pivot.Easting);

            // Calculate the delta x in local coordinates and steering angle degrees based on wheelbase
            double localHeading = twoPI - input.FixHeading;
            double ppRadius = goalPointDistanceSquared / (2 * (((goalPoint.Easting - pivot.Easting) * Math.Cos(localHeading)) + ((goalPoint.Northing - pivot.Northing) * Math.Sin(localHeading))));

            double steerAngle = ToDegrees(Math.Atan(2 * (((goalPoint.Easting - pivot.Easting) * Math.Cos(localHeading))
                + ((goalPoint.Northing - pivot.Northing) * Math.Sin(localHeading))) * input.Wheelbase / goalPointDistanceSquared));

            steerAngle *= input.UTurnCompensation;

            if (steerAngle < -input.MaxSteerAngle) steerAngle = -input.MaxSteerAngle;
            if (steerAngle > input.MaxSteerAngle) steerAngle = input.MaxSteerAngle;

            if (ppRadius < -500) ppRadius = -500;
            if (ppRadius > 500) ppRadius = 500;

            Vec2 radiusPoint = new Vec2
            {
                Easting = pivot.Easting + (ppRadius * Math.Cos(localHeading)),
                Northing = pivot.Northing + (ppRadius * Math.Sin(localHeading))
            };

            // Distance is negative if on left, positive if on right
            if (!isHeadingSameWay)
                distanceFromCurrentLine *= -1.0;

            // Output
            output.IsTurnComplete = false;
            output.DistanceFromCurrentLine = distanceFromCurrentLine;
            output.REast = rEast;
            output.RNorth = rNorth;
            output.SteerAngle = steerAngle;
            output.GoalPoint = goalPoint;
            output.RadiusPoint = radiusPoint;
            output.PPRadius = ppRadius;
            output.PointA = A;
            output.PointB = B;
            output.ModeActualXTE = distanceFromCurrentLine;
            output.GuidanceLineDistanceOff = (short)Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);
            output.GuidanceLineSteerAngle = (short)(steerAngle * 100);
            output.PathCount = ptCount - B;
        }
    }
}
