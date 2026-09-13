using UnityEngine;

namespace DotSoccer
{
    // Pure simulation state -- no Unity component references. This is the
    // data the server will eventually own and broadcast each tick, once the
    // turn logic in TurnResolver moves there.
    public class MatchState
    {
        public Vector2Int Player1Pos;
        public Vector2Int Player2Pos;
        public int BallOwner = 1; // 1 or 2
        public int Score1;
        public int Score2;
        public int CurrentHalf = 1; // 1 or 2

        public Vector2Int BallPos => BallOwner == 1 ? Player1Pos : Player2Pos;
    }
}
