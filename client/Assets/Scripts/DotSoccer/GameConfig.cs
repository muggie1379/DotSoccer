using UnityEngine;

namespace DotSoccer
{
    // Grid dimensions and rules shared by the pure simulation (TurnResolver)
    // and the Unity-side view (GridView). Plain data so it can be ported to
    // the server unchanged once the client/server split happens.
    [System.Serializable]
    public class GameConfig
    {
        public int GridWidth = 19;
        public int GridHeight = 11;

        [Tooltip("골대 높이(칸 수). 양쪽 끝 열(x=0, x=GridWidth-1)에서 세로 중앙 기준으로 적용됨")]
        public int GoalZoneSize = 5;

        public Vector2Int Player1Spawn = new Vector2Int(0, 5);
        public Vector2Int Player2Spawn = new Vector2Int(18, 5);

        public bool IsInGoalRow(int y)
        {
            int centerY = GridHeight / 2;
            int halfSpan = GoalZoneSize / 2;
            return Mathf.Abs(y - centerY) <= halfSpan;
        }

        public Vector2Int ClampToGrid(Vector2Int pos)
        {
            int x = Mathf.Clamp(pos.x, 0, GridWidth - 1);
            int y = Mathf.Clamp(pos.y, 0, GridHeight - 1);
            return new Vector2Int(x, y);
        }
    }
}
