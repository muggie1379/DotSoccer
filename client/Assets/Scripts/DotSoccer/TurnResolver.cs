using UnityEngine;

namespace DotSoccer
{
    public enum GoalEvent
    {
        None,
        Touchdown,
        OwnGoal,
    }

    public struct TurnOutcome
    {
        public GoalEvent Goal;
        public int GoalPlayer; // meaningful only when Goal != None
    }

    // Pure game rules: given the current state and both players' chosen
    // directions for this turn, mutates the state to the next turn and
    // reports whether a goal happened. No Unity MonoBehaviour, no I/O --
    // this is the piece that moves to the C++ server once the client/server
    // split happens (see devlog roadmap). Keeping it Unity-free now means
    // porting it later is a rules translation, not a rewrite.
    public static class TurnResolver
    {
        public static TurnOutcome Resolve(MatchState state, Vector2Int dir1, Vector2Int dir2, GameConfig config)
        {
            state.Player1Pos = config.ClampToGrid(state.Player1Pos + dir1);
            state.Player2Pos = config.ClampToGrid(state.Player2Pos + dir2);

            // Two players landing on the same cell steals the ball.
            if (state.Player1Pos == state.Player2Pos)
            {
                state.BallOwner = state.BallOwner == 1 ? 2 : 1;
            }

            int carrier = state.BallOwner;
            Vector2Int carrierPos = carrier == 1 ? state.Player1Pos : state.Player2Pos;

            if (!config.IsInGoalRow(carrierPos.y))
            {
                return new TurnOutcome { Goal = GoalEvent.None };
            }

            int opponentGoalX = carrier == 1 ? config.GridWidth - 1 : 0;
            int ownGoalX = carrier == 1 ? 0 : config.GridWidth - 1;

            if (carrierPos.x == opponentGoalX)
            {
                return new TurnOutcome { Goal = GoalEvent.Touchdown, GoalPlayer = carrier };
            }
            if (carrierPos.x == ownGoalX)
            {
                return new TurnOutcome { Goal = GoalEvent.OwnGoal, GoalPlayer = carrier };
            }

            return new TurnOutcome { Goal = GoalEvent.None };
        }
    }
}
