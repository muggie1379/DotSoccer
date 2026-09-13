using UnityEngine;

public class GridManager : MonoBehaviour
{
    public int gridWidth = 19;
    public int gridHeight = 11;
    public float cellSize = 1f;

    public Vector2Int player1Spawn = new Vector2Int(0, 5);
    public Vector2Int player2Spawn = new Vector2Int(18, 5);

    [Tooltip("1 = 전반 (1P가 공 소유), 2 = 후반 (2P가 공 소유)")]
    public int currentHalf = 1;

    [Tooltip("골대 높이(칸 수). 양쪽 끝 열(x=0, x=gridWidth-1)에서 세로 중앙 기준으로 적용됨")]
    public int goalZoneSize = 5;

    GameObject player1Obj;
    GameObject player2Obj;
    GameObject ballObj;

    void Awake()
    {
        DrawGridLines();
        DrawGoalMarkers();
        SpawnRoundEntities();
    }

    // 해당 y행이 골대 범위(세로 중앙 기준 goalZoneSize칸) 안에 있는지 확인
    public bool IsInGoalRow(int y)
    {
        int centerY = gridHeight / 2;
        int halfSpan = goalZoneSize / 2;
        return Mathf.Abs(y - centerY) <= halfSpan;
    }

    void SpawnRoundEntities()
    {
        player1Obj = SpawnEntity("Player1", player1Spawn, new Color(0.2f, 0.45f, 1f), 0.7f, 2);
        player2Obj = SpawnEntity("Player2", player2Spawn, new Color(1f, 0.25f, 0.25f), 0.7f, 2);

        Vector2Int ballSpawn = currentHalf == 1 ? player1Spawn : player2Spawn;
        ballObj = SpawnEntity("Ball", ballSpawn, Color.white, 0.5f, 3);
    }

    // 전반/후반 전환 시 호출 (다음 단계의 라운드 루프에서 사용)
    public void StartNextHalf()
    {
        currentHalf = currentHalf == 1 ? 2 : 1;

        if (player1Obj != null) Destroy(player1Obj);
        if (player2Obj != null) Destroy(player2Obj);
        if (ballObj != null) Destroy(ballObj);

        SpawnRoundEntities();
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(x * cellSize, y * cellSize, 0f);
    }

    public void MovePlayer(int playerIndex, Vector2Int gridPos)
    {
        GameObject obj = playerIndex == 1 ? player1Obj : player2Obj;
        if (obj != null) obj.transform.position = GridToWorld(gridPos.x, gridPos.y);
    }

    public void MoveBall(Vector2Int gridPos)
    {
        if (ballObj != null) ballObj.transform.position = GridToWorld(gridPos.x, gridPos.y);
    }

    void DrawGridLines()
    {
        GameObject linesParent = new GameObject("GridLines");
        linesParent.transform.SetParent(transform);

        for (int x = 0; x < gridWidth; x++)
        {
            CreateLine(linesParent.transform, "VLine_" + x,
                GridToWorld(x, 0), GridToWorld(x, gridHeight - 1));
        }

        for (int y = 0; y < gridHeight; y++)
        {
            CreateLine(linesParent.transform, "HLine_" + y,
                GridToWorld(0, y), GridToWorld(gridWidth - 1, y));
        }
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
        lr.sortingOrder = 0;
        lr.useWorldSpace = true;
    }

    void DrawGoalMarkers()
    {
        GameObject goalsParent = new GameObject("GoalMarkers");
        goalsParent.transform.SetParent(transform);

        for (int y = 0; y < gridHeight; y++)
        {
            if (!IsInGoalRow(y)) continue;

            SpawnGoalMarker(goalsParent.transform, "LeftGoal_" + y, 0, y);
            SpawnGoalMarker(goalsParent.transform, "RightGoal_" + y, gridWidth - 1, y);
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
        Gizmos.color = Color.gray;
        for (int x = 0; x < gridWidth; x++)
            Gizmos.DrawLine(GridToWorld(x, 0), GridToWorld(x, gridHeight - 1));
        for (int y = 0; y < gridHeight; y++)
            Gizmos.DrawLine(GridToWorld(0, y), GridToWorld(gridWidth - 1, y));
    }
}
