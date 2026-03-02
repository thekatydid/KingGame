using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BoardTileClickSpikeTest : MonoBehaviour
{
    private static readonly BoardCoord[] NeighborOffsets =
    {
        new BoardCoord(-1, -1),
        new BoardCoord(0, -1),
        new BoardCoord(1, -1),
        new BoardCoord(-1, 0),
        new BoardCoord(1, 0),
        new BoardCoord(-1, 1),
        new BoardCoord(0, 1),
        new BoardCoord(1, 1)
    };

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private GroundSpikeBurst spikeBurstPrefab;
    [SerializeField] private ChessBoardManager board;
    [SerializeField] private string effectRootName = "StoneEdgeEffect";

    [Header("Input")]
    [SerializeField] private bool ignoreUiClick = true;
    [SerializeField] private int ringPointCount = 8;
    [SerializeField] private float minRadiusRatio = 0.35f;
    [SerializeField] private int maxSampleAttempts = 40;

    private Transform effectRoot;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoardManager>();
        }

        effectRoot = GetOrCreateEffectRoot();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (ignoreUiClick && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (targetCamera == null || spikeBurstPrefab == null || board == null)
        {
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (!board.TryGetCoordFromRay(ray, out BoardCoord centerCoord) || !board.IsInside(centerCoord))
        {
            return;
        }

        if (effectRoot == null)
        {
            effectRoot = GetOrCreateEffectRoot();
        }

        GameObject clickGroup = new GameObject($"ClickEffect_{Time.frameCount}");
        clickGroup.transform.SetParent(effectRoot, false);

        int validNeighborCount = 0;
        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            BoardCoord targetCoord = centerCoord + NeighborOffsets[i];
            if (!board.IsInside(targetCoord))
            {
                continue;
            }

            validNeighborCount++;
        }

        float cellSize = EstimateCellSize(board, centerCoord);
        float radius = Mathf.Max(0.01f, cellSize * 0.5f);
        Vector3 centerWorld = board.CoordToWorld(centerCoord);
        int desiredCount = Mathf.Max(1, ringPointCount);
        List<Vector3> spawnPositions = BuildRandomRingPositions(centerCoord, centerWorld, radius, desiredCount);

        if (spawnPositions.Count == 0)
        {
            Destroy(clickGroup);
            return;
        }

        GroundSpikeBurst burst = Instantiate(spikeBurstPrefab, clickGroup.transform);
        burst.PlayAtPositions(centerWorld, board.transform.up, spawnPositions);

        float maxLifetime = burst.EstimatedLifetime;
        if (maxLifetime > 0f)
        {
            Destroy(clickGroup, maxLifetime);
        }
    }

    private List<Vector3> BuildRandomRingPositions(
        BoardCoord centerCoord,
        Vector3 centerWorld,
        float radius,
        int desiredCount
    )
    {
        List<Vector3> result = new List<Vector3>(desiredCount);
        float minR = radius * Mathf.Clamp01(minRadiusRatio);
        int attempts = Mathf.Max(desiredCount * 4, maxSampleAttempts);

        for (int i = 0; i < attempts && result.Count < desiredCount; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float dist = Mathf.Lerp(minR, radius, Mathf.Sqrt(Random.value));
            Vector3 candidate = centerWorld + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
            BoardCoord mapped = board.WorldToCoord(candidate);
            if (!board.IsInside(mapped))
            {
                continue;
            }

            result.Add(candidate);
        }

        return result;
    }

    private static float EstimateCellSize(ChessBoardManager boardManager, BoardCoord centerCoord)
    {
        Vector3 center = boardManager.CoordToWorld(centerCoord);
        BoardCoord[] samples =
        {
            new BoardCoord(1, 0),
            new BoardCoord(-1, 0),
            new BoardCoord(0, 1),
            new BoardCoord(0, -1)
        };

        for (int i = 0; i < samples.Length; i++)
        {
            BoardCoord neighbor = centerCoord + samples[i];
            if (!boardManager.IsInside(neighbor))
            {
                continue;
            }

            return Vector3.Distance(center, boardManager.CoordToWorld(neighbor));
        }

        return 1.5f;
    }

    private Transform GetOrCreateEffectRoot()
    {
        if (string.IsNullOrWhiteSpace(effectRootName))
        {
            effectRootName = "StoneEdgeEffect";
        }

        GameObject found = GameObject.Find(effectRootName);
        if (found != null)
        {
            return found.transform;
        }

        return new GameObject(effectRootName).transform;
    }
}
