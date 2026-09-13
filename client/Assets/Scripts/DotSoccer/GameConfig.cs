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

        // 골대 열(x=0, x=GridWidth-1)에서 몇 칸 안쪽. 스폰 지점을 골대 열 위에 두면
        // 킥오프 직후 공을 가진 채로 이미 자기 골대 안에 있는 셈이 되어 즉시
        // 자책골 판정이 나고, 실점한 쪽이 다시 공을 받아 같은 자리에서 시작하는
        // 구조 때문에 자책골이 끝없이 반복되는 버그가 있었다.
        public Vector2Int Player1Spawn = new Vector2Int(3, 5);
        public Vector2Int Player2Spawn = new Vector2Int(15, 5);

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
