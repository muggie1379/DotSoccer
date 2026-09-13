using UnityEngine;

namespace DotSoccer
{
    // Pure view: draws the grid/goal markers and moves the player/ball
    // sprites to match whatever MatchState the controller gives it. Holds
    // no game rules or state of its own -- once the server is authoritative,
    // this is the only piece that survives unchanged on the client.
    public class GridView : MonoBehaviour
    {
        public GameConfig Config;
        public float CellSize = 1f;

        GameObject player1Obj;
        GameObject player2Obj;
        GameObject ballObj;

        public void BuildField()
        {
            DrawGridLines();
            DrawGoalMarkers();
        }

        public void SpawnEntities(Vector2Int player1Pos, Vector2Int player2Pos, Vector2Int ballPos)
        {
            player1Obj = SpawnEntity("Player1", player1Pos, new Color(0.2f, 0.45f, 1f), 0.7f, 2);
            player2Obj = SpawnEntity("Player2", player2Pos, new Color(1f, 0.25f, 0.25f), 0.7f, 2);
            ballObj = SpawnEntity("Ball", ballPos, Color.white, 0.5f, 3);
        }

        public Vector3 GridToWorld(int x, int y) => new Vector3(x * CellSize, y * CellSize, 0f);

        public void ApplyPositions(Vector2Int player1Pos, Vector2Int player2Pos, Vector2Int ballPos)
        {
            if (player1Obj != null) player1Obj.transform.position = GridToWorld(player1Pos.x, player1Pos.y);
            if (player2Obj != null) player2Obj.transform.position = GridToWorld(player2Pos.x, player2Pos.y);
            if (ballObj != null) ballObj.transform.position = GridToWorld(ballPos.x, ballPos.y);
        }

        void DrawGridLines()
        {
            GameObject linesParent = new GameObject("GridLines");
            linesParent.transform.SetParent(transform);

            for (int x = 0; x < Config.GridWidth; x++)
                CreateLine(linesParent.transform, "VLine_" + x, GridToWorld(x, 0), GridToWorld(x, Config.GridHeight - 1));

            for (int y = 0; y < Config.GridHeight; y++)
                CreateLine(linesParent.transform, "HLine_" + y, GridToWorld(0, y), GridToWorld(Config.GridWidth - 1, y));
        }

        void CreateLine(Transform parent, string objName, Vector3 from, Vector3 to)
        {
            GameObject lineObj = new GameObject(objName);
            lineObj.transform.SetParent(parent);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.03f;
            lr.endWidth = 0.03f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1f, 1f, 1f, 0.35f);
            lr.endColor = new Color(1f, 1f, 1f, 0.35f);
            lr.useWorldSpace = true;
        }

        void DrawGoalMarkers()
        {
            GameObject goalsParent = new GameObject("GoalMarkers");
            goalsParent.transform.SetParent(transform);

            for (int y = 0; y < Config.GridHeight; y++)
            {
                if (!Config.IsInGoalRow(y)) continue;
                SpawnGoalMarker(goalsParent.transform, "LeftGoal_" + y, 0, y);
                SpawnGoalMarker(goalsParent.transform, "RightGoal_" + y, Config.GridWidth - 1, y);
            }
        }

        void SpawnGoalMarker(Transform parent, string objName, int x, int y)
        {
            GameObject obj = new GameObject(objName);
            obj.transform.SetParent(parent);
            obj.transform.position = GridToWorld(x, y);
            obj.transform.localScale = Vector3.one * 0.9f;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(0.2f, 0.8f, 0.3f, 0.35f);
            sr.sortingOrder = 1;
        }

        GameObject SpawnEntity(string objName, Vector2Int gridPos, Color color, float scale, int sortingOrder)
        {
            GameObject obj = new GameObject(objName);
            obj.transform.SetParent(transform);
            obj.transform.position = GridToWorld(gridPos.x, gridPos.y);
            obj.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            return obj;
        }

        Sprite CreateCircleSprite()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    tex.SetPixel(x, y, dist <= radius ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        void OnDrawGizmos()
        {
            if (Config == null) return;
            Gizmos.color = Color.gray;
            for (int x = 0; x < Config.GridWidth; x++)
                Gizmos.DrawLine(GridToWorld(x, 0), GridToWorld(x, Config.GridHeight - 1));
            for (int y = 0; y < Config.GridHeight; y++)
                Gizmos.DrawLine(GridToWorld(0, y), GridToWorld(Config.GridWidth - 1, y));
        }
    }
}
