using AssemblyEngine.Core;
using System.Numerics;

namespace FpsSample;

internal readonly record struct FpsArenaBlock(Vector3 Center, Vector3 Scale, Color Color)
{
    public Vector3 Min => Center - (Scale * 0.5f);

    public Vector3 Max => Center + (Scale * 0.5f);
}

internal sealed class FpsArenaMap
{
    public const float CellSize = 2.6f;
    public const float WallHeight = 2.8f;
    public const float CrateHeight = 1.2f;
    public const float ExitTriggerRadius = 1.2f;

    private FpsArenaMap(
        int columns,
        int rows,
        IReadOnlyList<FpsArenaBlock> solidBlocks,
        IReadOnlyList<Vector3> enemySpawns,
        IReadOnlyList<Vector3> floorPanels,
        Vector3 playerSpawn,
        Vector3 exitPosition)
    {
        Columns = columns;
        Rows = rows;
        SolidBlocks = solidBlocks;
        EnemySpawns = enemySpawns;
        FloorPanels = floorPanels;
        PlayerSpawn = playerSpawn;
        ExitPosition = exitPosition;
    }

    public int Columns { get; }

    public int Rows { get; }

    public float Width => Columns * CellSize;

    public float Depth => Rows * CellSize;

    public IReadOnlyList<FpsArenaBlock> SolidBlocks { get; }

    public IReadOnlyList<Vector3> EnemySpawns { get; }

    public IReadOnlyList<Vector3> FloorPanels { get; }

    public Vector3 PlayerSpawn { get; }

    public Vector3 ExitPosition { get; }

    public static FpsArenaMap CreateDefault()
    {
        string[] rows =
        [
            "###############",
            "#P...#...D....#",
            "#.##.#.###.##.#",
            "#....#...#....#",
            "#.D..C...#..D.#",
            "###.###.###...#",
            "#...#.....#...#",
            "#.###.C.#.#.#.#",
            "#...#...#...#.#",
            "#.D...#...C...#",
            "#...###.#.###.#",
            "#....D..#....E#",
            "###############"
        ];

        return Parse(rows);
    }

    private static FpsArenaMap Parse(string[] rows)
    {
        if (rows.Length == 0)
            throw new ArgumentException("Arena layout cannot be empty.", nameof(rows));

        var columns = rows[0].Length;
        if (columns == 0)
            throw new ArgumentException("Arena layout cannot contain empty rows.", nameof(rows));

        var solidBlocks = new List<FpsArenaBlock>();
        var enemySpawns = new List<Vector3>();
        var floorPanels = new List<Vector3>();
        Vector3? playerSpawn = null;
        Vector3? exitPosition = null;

        for (var row = 0; row < rows.Length; row++)
        {
            if (rows[row].Length != columns)
                throw new ArgumentException("Arena layout rows must all have the same width.", nameof(rows));

            for (var column = 0; column < columns; column++)
            {
                var cell = rows[row][column];
                var center = GetCellCenter(row, column, rows.Length, columns);

                if (cell != '#' && ((row + column) % 2 == 0))
                    floorPanels.Add(center);

                switch (cell)
                {
                    case '#':
                        solidBlocks.Add(new FpsArenaBlock(
                            center + new Vector3(0f, WallHeight / 2f, 0f),
                            new Vector3(CellSize, WallHeight, CellSize),
                            new Color(28, 72, 96)));
                        break;

                    case 'C':
                        solidBlocks.Add(new FpsArenaBlock(
                            center + new Vector3(0f, CrateHeight / 2f, 0f),
                            new Vector3(CellSize * 0.82f, CrateHeight, CellSize * 0.82f),
                            new Color(138, 96, 60)));
                        break;

                    case 'D':
                        enemySpawns.Add(center);
                        break;

                    case 'P':
                        playerSpawn = center;
                        break;

                    case 'E':
                        exitPosition = center;
                        break;

                    case '.':
                        break;

                    default:
                        throw new ArgumentException($"Unknown arena token '{cell}'.", nameof(rows));
                }
            }
        }

        if (playerSpawn is null)
            throw new ArgumentException("Arena layout must include a player spawn 'P'.", nameof(rows));

        if (exitPosition is null)
            throw new ArgumentException("Arena layout must include an extraction tile 'E'.", nameof(rows));

        return new FpsArenaMap(
            columns,
            rows.Length,
            solidBlocks,
            enemySpawns,
            floorPanels,
            playerSpawn.Value,
            exitPosition.Value);
    }

    private static Vector3 GetCellCenter(int row, int column, int rows, int columns)
    {
        var x = ((column - (columns / 2f)) + 0.5f) * CellSize;
        var z = ((row - (rows / 2f)) + 0.5f) * CellSize;
        return new Vector3(x, 0f, z);
    }
}