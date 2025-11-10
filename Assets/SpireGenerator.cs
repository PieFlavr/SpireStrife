using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpireGenerator : MonoBehaviour
{
    public HexGrid hexGrid;
    public GameObject spirePrefab;
    public int randomSeed = 42;
    [Header("Turn Manager Integration")]
    [Tooltip("Automatically start the Turn Manager when generation completes.")]
    public bool startTurnManagerOnComplete = true;

    [Header("Counts")]
    public int playerSpireCount = 1;
    public int aiSpireCount = 1;
    public int neutralSpireCount = 10;

    [Header("Spire Initial Garrison")]
    public int spireInitialGarrison = 1000;
    public int neutralSpireInitialGarrison = 10;


    [Header("Player Anchor")]
    public Vector2Int playerAnchorCenter = new Vector2Int(0, -17);
    public int playerAnchorRadius = 5;

    [Header("Neutral Near Player")]
    public int neutralNearPlayerCount = 3;
    public int neutralNearRadius = 6;

    [Header("Geometry Limits (hex steps)")]
    public int minHexDist = 2;   // hard spacing floor
    public int maxHexDist = 12;  // soft reference only

    [Header("Path Distance Limit (steps)")]
    public int pathMaxStepsExclusive = 10;

    [Header("Difficulty [0..1]")]
    [Range(0f, 1f)] public float difficulty = 0f; // 0 easy, 1 hard

    [Header("Neutral Separation (dynamic)")]
    public int neutralMinDistEasy = 1;
    public int neutralMinDistHard = 5;

    [Header("Distance Bias Target (hex steps)")]
    public int biasMin = 3;   // target at difficulty=0
    public int biasMax = 10;  // target at difficulty=1   

    // difficulty-driven knobs
    float neutralAttractionLen;
    int neutralMinDistDyn;

    // state
    readonly List<Vector2Int> placedAxials = new();
    readonly List<Vector3> placedWorld = new();
    bool firstPlayerSet = false;
    Vector2Int firstPlayerAxial;
    public bool IsGenerating { get; private set; } = false;

    // deterministic RNG
    System.Random rng;

    void Awake()
    {
        IsGenerating = true; // mark generating early to avoid race with TurnManager.Start()
        rng = new System.Random(randomSeed); // isolate PRNG for determinism
        // If other systems rely on UnityEngine.Random, you may also seed it:
        // Random.InitState(randomSeed);
    }

    void Start()
    {
        SetDifficultyParams();
        StartCoroutine(GenerateSpiresCoroutine());
    }

    void SetDifficultyParams()
    {
        neutralAttractionLen = Mathf.Lerp(2.0f, 4.0f, difficulty);

        int easy = Mathf.Max(1, neutralMinDistEasy);
        int hard = Mathf.Max(easy, neutralMinDistHard);
        neutralMinDistDyn = Mathf.RoundToInt(Mathf.Lerp(easy, hard, difficulty));
    }

    public IEnumerator GenerateSpiresCoroutine()
    {
        IsGenerating = true;
        placedAxials.Clear();
        placedWorld.Clear();
        firstPlayerSet = false;

        yield return StartCoroutine(PlacePlayerSpiresCoroutine());

        yield return StartCoroutine(PlaceNeutralNearPlayerBiasedCoroutine(neutralMinDistDyn));

        int remainNeutrals = Mathf.Max(0, neutralSpireCount - neutralNearPlayerCount);
        yield return StartCoroutine(PlaceManyGraphConstrainedCoroutine(
            remainNeutrals, Color.white, GlobalScore, pathMaxStepsExclusive, neutralMinDistDyn));

        yield return StartCoroutine(PlaceAIBiasedCoroutine(aiSpireCount));

        // All spires placed
        IsGenerating = false;
        if (startTurnManagerOnComplete && TurnManager.inst != null && TurnManager.inst.CurrentPhase == TurnManager.Phase.Init)
        {
            TurnManager.inst.StartGame();
        }
    }

    /// <summary>
    /// Clears all existing spires from the scene and resets generation state.
    /// </summary>
    public void ClearExistingSpires()
    {
        foreach (var spire in FindObjectsOfType<SpireConstruct>())
        {
            Destroy(spire.gameObject);
        }

        placedAxials.Clear();
        placedWorld.Clear();
        firstPlayerSet = false;

        Debug.Log("SpireGenerator: Cleared all existing spires.");
    }

    /// <summary>
    /// Regenerates the map from scratch with current difficulty settings.
    /// Call this after LevelManager updates the difficulty value.
    /// </summary>
    public void RegenerateMap()
    {
        ClearExistingSpires();
        SetDifficultyParams(); // Recalculate difficulty-driven parameters
        StartCoroutine(GenerateSpiresCoroutine());
        Debug.Log($"SpireGenerator: Starting regeneration with difficulty {difficulty:F2}");
    }

    // ---------- Difficulty → randomness and distance target ----------
    float Temperature() => Mathf.Lerp(1.5f, 0.25f, difficulty);           // easy noisy, hard greedy
    float TargetDist() => Mathf.Lerp(biasMin, biasMax, difficulty);       // 0→biasMin, 1→biasMax
    float TargetSpread() => Mathf.Lerp(3.5f, 1.25f, difficulty);          // easy wide, hard tight

    // Peak at distance ≈ TargetDist from center
    float DistBiasTo(Vector2Int p, Vector2Int center)
    {
        int d = HexAxialDistance(p, center);
        float t = TargetDist();
        float s = Mathf.Max(0.001f, TargetSpread());
        return Mathf.Exp(-Mathf.Pow((d - t) / s, 2f));
    }

    // Score used after first player placement
    float GlobalScore(Vector2Int p)
    {
        if (!firstPlayerSet) return 0f;
        return DistBiasTo(p, firstPlayerAxial);
    }

    // ---------- Deterministic PRNG wrappers ----------
    float NextFloat() => (float)rng.NextDouble();
    int NextInt(int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);

    // ---------- Sampling helpers ----------
    int WeightedIndex(List<float> weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) return -1;
        float r = NextFloat() * total;
        for (int i = 0; i < weights.Count; i++)
        {
            r -= Mathf.Max(0f, weights[i]);
            if (r <= 0f) return i;
        }
        return weights.Count - 1;
    }

    int SampleBySoftmax(List<HexCell> candidates, System.Func<Vector2Int, float> scoreFn, float tau)
    {
        if (candidates.Count == 0) return -1;

        var scores = new List<float>(candidates.Count);
        float maxS = float.NegativeInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            float s = scoreFn(candidates[i].axial_coords);
            scores.Add(s);
            if (s > maxS) maxS = s;
        }

        var weights = new List<float>(candidates.Count);
        float denom = Mathf.Max(0.001f, tau);
        for (int i = 0; i < scores.Count; i++)
            weights.Add(Mathf.Exp((scores[i] - maxS) / denom));

        return WeightedIndex(weights);
    }

    // ---------- Player ----------
    IEnumerator PlacePlayerSpiresCoroutine()
    {
        if (playerSpireCount <= 0) yield break;

        // first player spire near anchor, biased to TargetDist from anchor
        HexCell first = PickCandidateNear(playerAnchorCenter, playerAnchorRadius, requireRules: false);
        if (first == null) yield break;

        Spawn(first, Color.yellow);
        firstPlayerAxial = first.axial_coords;
        firstPlayerSet = true;
        yield return null;

        // remaining player spires: biased random by GlobalScore
        for (int i = 1; i < playerSpireCount; i++)
        {
            bool done = false;
            yield return StartCoroutine(TryPlaceBiasedWithPathCoroutine(
                Color.yellow,
                pickNearCenter: (Vector2Int?)null,
                nearRadius: 0,
                scoreFn: GlobalScore,
                maxSteps: pathMaxStepsExclusive,
                overrideMinHexDist: -1,
                placed: _ => done = true));
            if (!done) break;
        }
    }

    // ---------- Neutrals near player ----------
    IEnumerator PlaceNeutralNearPlayerBiasedCoroutine(int neutralMinDistOverride)
    {
        if (!firstPlayerSet || neutralNearPlayerCount <= 0) yield break;

        for (int i = 0; i < neutralNearPlayerCount; i++)
        {
            bool finished = false;
            yield return StartCoroutine(TryPlaceBiasedWithPathCoroutine(
                Color.white,
                pickNearCenter: firstPlayerAxial,
                nearRadius: neutralNearRadius,
                scoreFn: GlobalScore,
                maxSteps: pathMaxStepsExclusive,
                overrideMinHexDist: neutralMinDistOverride,
                placed: _ => finished = true));
            if (!finished) break;
        }
    }

    // ---------- Remaining neutrals ----------
    IEnumerator PlaceManyGraphConstrainedCoroutine(
        int count, Color color, System.Func<Vector2Int, float> scoreFn, int maxSteps, int neutralMinDistOverride)
    {
        for (int i = 0; i < count; i++)
        {
            bool finished = false;
            yield return StartCoroutine(TryPlaceBiasedWithPathCoroutine(
                color,
                pickNearCenter: null,
                nearRadius: 0,
                scoreFn: scoreFn,
                maxSteps: maxSteps,
                overrideMinHexDist: neutralMinDistOverride,
                placed: _ => finished = true));
            if (!finished) break;
        }
    }

    // ---------- AI spires ----------
    IEnumerator PlaceAIBiasedCoroutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            bool finished = false;
            yield return StartCoroutine(TryPlaceBiasedWithPathCoroutine(
                Color.blue,
                pickNearCenter: null,
                nearRadius: 0,
                scoreFn: GlobalScore,
                maxSteps: pathMaxStepsExclusive,
                overrideMinHexDist: -1,
                placed: _ => finished = true));
            if (!finished) break;
        }
    }

    // ---------- Core placement: softmax sampling without replacement ----------
    IEnumerator TryPlaceBiasedWithPathCoroutine(
        Color color,
        Vector2Int? pickNearCenter,
        int nearRadius,
        System.Func<Vector2Int, float> scoreFn,
        int maxSteps,
        int overrideMinHexDist,
        System.Action<bool> placed)
    {
        var candidates = (pickNearCenter.HasValue)
            ? GatherCandidatesAround(pickNearCenter.Value, nearRadius, overrideMinHexDist)
            : GatherAllCandidates(overrideMinHexDist);

        if (candidates.Count == 0) { placed?.Invoke(false); yield break; }

        float tau = Temperature();
        bool success = false;

        // try up to all candidates without replacement
        for (int attempts = 0; attempts < candidates.Count; attempts++)
        {
            int pick = SampleBySoftmax(candidates, scoreFn, tau);
            if (pick < 0) break;

            var c = candidates[pick];
            candidates.RemoveAt(pick);

            if (!SatisfiesLocalGeometry(c.axial_coords, overrideMinHexDist)) continue;

            bool reachable = placedWorld.Count == 0;
            if (!reachable)
            {
                for (int i = 0; i < placedWorld.Count; i++)
                {
                    bool decided = false;
                    bool within = false;

                    PathRequestManager.RequestPathUnder(placedWorld[i], c.transform.position, maxSteps,
                        (ok, _) => { within = ok; decided = true; });

                    while (!decided) yield return null;

                    if (within) { reachable = true; break; }
                }
            }
            if (!reachable) continue;

            Spawn(c, color);
            success = true;
            break;
        }

        placed?.Invoke(success);
    }

    // ---------- Candidate gathering (stable order) ----------
    List<HexCell> GatherCandidatesAround(Vector2Int center, int radius, int overrideMinHexDist)
    {
        var list = new List<HexCell>();
        foreach (var c in hexGrid.GetAllCells())
        {
            if (c == null || !c.isWalkable) continue;
            if (HexAxialDistance(c.axial_coords, center) > radius) continue;
            if (!SatisfiesLocalGeometry(c.axial_coords, overrideMinHexDist)) continue;
            list.Add(c);
        }
        list.Sort((a, b) =>
        {
            if (a.axial_coords.x != b.axial_coords.x)
                return a.axial_coords.x.CompareTo(b.axial_coords.x);
            return a.axial_coords.y.CompareTo(b.axial_coords.y);
        });
        return list;
    }

    List<HexCell> GatherAllCandidates(int overrideMinHexDist)
    {
        var list = new List<HexCell>();
        foreach (var c in hexGrid.GetAllCells())
        {
            if (c == null || !c.isWalkable) continue;
            if (!SatisfiesLocalGeometry(c.axial_coords, overrideMinHexDist)) continue;
            list.Add(c);
        }
        list.Sort((a, b) =>
        {
            if (a.axial_coords.x != b.axial_coords.x)
                return a.axial_coords.x.CompareTo(b.axial_coords.x);
            return a.axial_coords.y.CompareTo(b.axial_coords.y);
        });
        return list;
    }

    // ---------- Geometry constraint with optional override ----------
    bool SatisfiesLocalGeometry(Vector2Int p, int overrideMinHexDist = -1)
    {
        int minDist = (overrideMinHexDist > 0) ? overrideMinHexDist : minHexDist;
        foreach (var q in placedAxials)
        {
            int d = HexAxialDistance(p, q);
            if (d < minDist) return false;
        }
        return true;
    }

    // ---------- Spawn ----------
    void Spawn(HexCell cell, Color color)
    {
        placedAxials.Add(cell.axial_coords);
        placedWorld.Add(cell.transform.position);

        // if (color != Color.white) cell.SetColor(color);

        var spire = Instantiate(spirePrefab, cell.transform.position, Quaternion.identity);

        var sc = spire.GetComponent<SpireConstruct>();
        if (sc != null)
        {
            // Set intended starting owner based on color hint
            if (color == Color.white)
            {
                sc.startingOwner = SpireConstruct.OwnerType.Neutral;
                spire.name = $"Neutral_Spire_{cell.axial_coords.x}_{cell.axial_coords.y}";
                sc.initialGarrison = neutralSpireInitialGarrison;
            }
            else if (color == Color.blue)
            {
                sc.startingOwner = SpireConstruct.OwnerType.AI;
                spire.name = $"Ai_Spire_{cell.axial_coords.x}_{cell.axial_coords.y}";
                sc.initialGarrison = spireInitialGarrison;
            }
            else
            {
                sc.startingOwner = SpireConstruct.OwnerType.Player;
                spire.name = $"Player_Spire_{cell.axial_coords.x}_{cell.axial_coords.y}";
                sc.initialGarrison = spireInitialGarrison;
            }

            // Properly register on the cell to invoke OnPlacedOnGrid and visuals/layer setup
            if (!cell.TryAddGridObject(sc))
            {
                // Fallback: place manually if something unusual blocks TryAdd
                sc.OnPlacedOnGrid(cell);
            }
        }
        else
        {
            Debug.LogError("Spire prefab missing SpireConstruct component.");
        }
    }

    // ---------- Biased first pick near anchor (uses same distance bias, center=anchor) ----------
    HexCell PickCandidateNear(Vector2Int center, int radius, bool requireRules)
    {
        var near = new List<HexCell>();
        var weights = new List<float>();
        float tau = Temperature();

        foreach (var c in hexGrid.GetAllCells())
        {
            if (c == null || !c.isWalkable) continue;
            int d = HexAxialDistance(c.axial_coords, center);
            if (d > radius) continue;
            if (requireRules && !SatisfiesLocalGeometry(c.axial_coords)) continue;

            near.Add(c);
            float w = DistBiasTo(c.axial_coords, center);      // peak at TargetDist()
            w = Mathf.Pow(w, 1f / Mathf.Max(0.001f, tau));     // extra noise control
            weights.Add(w);
        }

        if (near.Count == 0) return null;

        // stable tiebreaker: sort by axial before sampling fallback
        near.Sort((a, b) =>
        {
            if (a.axial_coords.x != b.axial_coords.x)
                return a.axial_coords.x.CompareTo(b.axial_coords.x);
            return a.axial_coords.y.CompareTo(b.axial_coords.y);
        });

        int idx = WeightedIndex(weights);
        if (idx < 0) idx = NextInt(0, near.Count);
        return near[idx];
    }

    // ---------- Utilities ----------
    static int HexAxialDistance(Vector2Int a, Vector2Int b)
    {
        int ax = a.x, az = a.y, ay = -ax - az;
        int bx = b.x, bz = b.y, by = -bx - bz;
        return Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(ay - by), Mathf.Abs(az - bz));
    }
}
