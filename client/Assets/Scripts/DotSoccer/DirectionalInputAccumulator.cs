using UnityEngine;

namespace DotSoccer
{
    // Accumulates how long each of 4 directional keys was held during a
    // turn, so both a quick tap and a full hold register, and a key
    // released just before the turn resolves isn't silently dropped as
    // long as it was released within `releaseTolerance` seconds.
    //
    // One instance per local player. When input moves to the network, this
    // whole class goes away -- each client will just send its instant
    // direction once per tick instead.
    public class DirectionalInputAccumulator
    {
        float holdUp, holdDown, holdLeft, holdRight;
        float lastSeenUp = -999f, lastSeenDown = -999f, lastSeenLeft = -999f, lastSeenRight = -999f;

        public void Accumulate(bool up, bool down, bool left, bool right, float deltaTime, float now)
        {
            if (up) { holdUp += deltaTime; lastSeenUp = now; }
            if (down) { holdDown += deltaTime; lastSeenDown = now; }
            if (left) { holdLeft += deltaTime; lastSeenLeft = now; }
            if (right) { holdRight += deltaTime; lastSeenRight = now; }
        }

        public static Vector2Int InstantDirection(bool up, bool down, bool left, bool right)
        {
            int x = (right ? 1 : 0) - (left ? 1 : 0);
            int y = (up ? 1 : 0) - (down ? 1 : 0);
            return new Vector2Int(x, y);
        }

        public Vector2Int FinalizeDirection(float now, float releaseTolerance)
        {
            float x = Effective(holdRight, lastSeenRight, now, releaseTolerance)
                    - Effective(holdLeft, lastSeenLeft, now, releaseTolerance);
            float y = Effective(holdUp, lastSeenUp, now, releaseTolerance)
                    - Effective(holdDown, lastSeenDown, now, releaseTolerance);
            return new Vector2Int(SignZero(x), SignZero(y));
        }

        public void Reset()
        {
            holdUp = holdDown = holdLeft = holdRight = 0f;
        }

        static float Effective(float hold, float lastSeen, float now, float releaseTolerance)
        {
            return (now - lastSeen <= releaseTolerance) ? hold : 0f;
        }

        static int SignZero(float v)
        {
            if (v > 0f) return 1;
            if (v < 0f) return -1;
            return 0;
        }
    }
}
