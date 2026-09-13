using UnityEngine;

public class OmokGame : MonoBehaviour
{
    const int Size = 15;
    const int Empty = 0;
    const int Black = 1;
    const int White = 2;

    int[,] board = new int[Size, Size];
    int currentPlayer = Black;
    int winner = Empty;
    string statusMessage = "";

    float cellSize = 36f;
    float boardOriginX = 20f;
    float boardOriginY = 60f;

    GUIStyle stoneStyle;
    GUIStyle statusStyle;
    GUIStyle restartStyle;

    void Start()
    {
        ResetBoard();
    }

    void RecalculateLayout()
    {
        float availableHeight = Screen.height - 140f;
        float availableWidth = Screen.width - 40f;
        float maxCellFromHeight = availableHeight / (Size - 1);
        float maxCellFromWidth = availableWidth / (Size - 1);
        cellSize = Mathf.Min(maxCellFromHeight, maxCellFromWidth);

        float boardPixelSize = cellSize * (Size - 1);
        boardOriginX = (Screen.width - boardPixelSize) * 0.5f;
        boardOriginY = 100f;
    }

    void ResetBoard()
    {
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                board[x, y] = Empty;

        currentPlayer = Black;
        winner = Empty;
        statusMessage = "흑 차례";
    }

    void OnGUI()
    {
        RecalculateLayout();

        if (stoneStyle == null)
        {
            stoneStyle = new GUIStyle(GUI.skin.button);
            statusStyle = new GUIStyle(GUI.skin.label);
            restartStyle = new GUIStyle(GUI.skin.button);
        }

        float stoneButtonSize = cellSize * 0.85f;
        stoneStyle.fontSize = Mathf.RoundToInt(stoneButtonSize * 0.7f);
        statusStyle.fontSize = Mathf.RoundToInt(cellSize * 0.6f);
        restartStyle.fontSize = Mathf.RoundToInt(cellSize * 0.45f);

        GUI.Label(new Rect(boardOriginX, 20, 400, 60), statusMessage, statusStyle);

        // board background
        float boardPixelSize = cellSize * (Size - 1);
        GUI.Box(new Rect(boardOriginX - cellSize * 0.5f, boardOriginY - cellSize * 0.5f,
            boardPixelSize + cellSize, boardPixelSize + cellSize), "");

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                float px = boardOriginX + x * cellSize - stoneButtonSize * 0.5f;
                float py = boardOriginY + y * cellSize - stoneButtonSize * 0.5f;
                Rect rect = new Rect(px, py, stoneButtonSize, stoneButtonSize);

                string label = board[x, y] == Black ? "●" : board[x, y] == White ? "○" : "";
                if (GUI.Button(rect, label, stoneStyle))
                {
                    TryPlaceStone(x, y);
                }
            }
        }

        if (winner != Empty)
        {
            float btnWidth = cellSize * 3.5f;
            float btnHeight = cellSize * 0.9f;
            if (GUI.Button(new Rect(boardOriginX, boardOriginY + boardPixelSize + 30, btnWidth, btnHeight), "다시 시작", restartStyle))
            {
                ResetBoard();
            }
        }
    }

    void TryPlaceStone(int x, int y)
    {
        if (winner != Empty) return;
        if (board[x, y] != Empty) return;

        board[x, y] = currentPlayer;

        if (CheckWin(x, y, currentPlayer))
        {
            winner = currentPlayer;
            statusMessage = (winner == Black ? "흑" : "백") + " 승리!";
            return;
        }

        currentPlayer = currentPlayer == Black ? White : Black;
        statusMessage = (currentPlayer == Black ? "흑" : "백") + " 차례";
    }

    bool CheckWin(int x, int y, int player)
    {
        int[][] directions = new int[][]
        {
            new int[] { 1, 0 },
            new int[] { 0, 1 },
            new int[] { 1, 1 },
            new int[] { 1, -1 },
        };

        foreach (var dir in directions)
        {
            int count = 1;
            count += CountDirection(x, y, dir[0], dir[1], player);
            count += CountDirection(x, y, -dir[0], -dir[1], player);
            if (count >= 5) return true;
        }
        return false;
    }

    int CountDirection(int x, int y, int dx, int dy, int player)
    {
        int count = 0;
        int cx = x + dx;
        int cy = y + dy;
        while (cx >= 0 && cx < Size && cy >= 0 && cy < Size && board[cx, cy] == player)
        {
            count++;
            cx += dx;
            cy += dy;
        }
        return count;
    }
}
