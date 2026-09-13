using UnityEngine;

public class DotSoccerBoard : MonoBehaviour
{
    const int FieldCols = 11;
    const int FieldRows = 9;
    const int GoalDepth = 2;
    const int GoalRows = 3;

    int goalRowStart;
    float cellSize;
    float originX;
    float originY;

    GUIStyle cellStyle;
    GUIStyle goalStyle;
    GUIStyle labelStyle;

    string statusMessage = "그리드 + 좌우 골대";

    void Start()
    {
        goalRowStart = (FieldRows - GoalRows) / 2;
    }

    void RecalculateLayout()
    {
        int totalCols = FieldCols + GoalDepth * 2;
        float availableHeight = Screen.height - 140f;
        float availableWidth = Screen.width - 40f;
        float maxCellFromHeight = availableHeight / (FieldRows - 1);
        float maxCellFromWidth = availableWidth / (totalCols - 1);
        cellSize = Mathf.Min(maxCellFromHeight, maxCellFromWidth);

        float boardPixelWidth = cellSize * (totalCols - 1);
        originX = (Screen.width - boardPixelWidth) * 0.5f;
        originY = 100f;
    }

    void OnGUI()
    {
        RecalculateLayout();

        if (cellStyle == null)
        {
            cellStyle = new GUIStyle(GUI.skin.box);
            goalStyle = new GUIStyle(GUI.skin.box);
            goalStyle.normal.background = MakeTex(new Color(0.2f, 0.7f, 0.3f, 0.9f));
            labelStyle = new GUIStyle(GUI.skin.label);
        }

        labelStyle.fontSize = Mathf.RoundToInt(cellSize * 0.5f);
        GUI.Label(new Rect(originX, 20, 600, 60), statusMessage, labelStyle);

        float dotSize = Mathf.Max(4f, cellSize * 0.15f);

        // Main field boundary
        float fieldPixelX = originX + GoalDepth * cellSize;
        float fieldPixelWidth = cellSize * (FieldCols - 1);
        float fieldPixelHeight = cellSize * (FieldRows - 1);
        GUI.Box(new Rect(fieldPixelX - cellSize * 0.5f, originY - cellSize * 0.5f,
            fieldPixelWidth + cellSize, fieldPixelHeight + cellSize), "");

        // Grid intersection dots
        for (int x = 0; x < FieldCols; x++)
        {
            for (int y = 0; y < FieldRows; y++)
            {
                float px = fieldPixelX + x * cellSize - dotSize * 0.5f;
                float py = originY + y * cellSize - dotSize * 0.5f;
                GUI.Box(new Rect(px, py, dotSize, dotSize), "", cellStyle);
            }
        }

        // Left goal area
        float leftGoalX = originX - cellSize * 0.5f;
        float goalPixelY = originY + goalRowStart * cellSize - cellSize * 0.5f;
        float goalPixelHeight = cellSize * (GoalRows - 1) + cellSize;
        float goalPixelWidth = cellSize * GoalDepth;
        GUI.Box(new Rect(leftGoalX, goalPixelY, goalPixelWidth, goalPixelHeight), "왼쪽 골대", goalStyle);

        // Right goal area
        float rightGoalX = fieldPixelX + fieldPixelWidth + cellSize * 0.5f;
        GUI.Box(new Rect(rightGoalX, goalPixelY, goalPixelWidth, goalPixelHeight), "오른쪽 골대", goalStyle);
    }

    Texture2D MakeTex(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
