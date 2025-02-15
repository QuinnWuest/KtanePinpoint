using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class pinpointScript : MonoBehaviour {

    public KMAudio Audio; //TODO: add sfx
    public KMBombModule Module;

    public KMSelectable[] Positions;
    public GameObject Square;
    public GameObject Rails;
    public SpriteRenderer HorizScissors;
    public SpriteRenderer VertiScissors;
    public Sprite[] ScissorSprites;
    public SpriteRenderer Arm;
    public GameObject DistanceObj;
    public TextMesh Distance;

    int[] points = { -1, -1, -1, -1 }; //position in reading order; was going to use a class for this but this is what _Zero, Zero_ does
    int[] pointXs = { -1, -1, -1, -1 };
    int[] pointYs = { -1, -1, -1, -1 };
    float scaleFactor = -1f;
    float[] dists = { -1f, -1f, -1f };
    float HUESCALE = 0.0005f;
    int shownPoint = 0;
    float WAITTIME = 4f;
    float ZIPTIME = 0.5f;
    float[] posLUT = { -0.055f, -0.042777f, -0.030555f, -0.018333f, -0.006111f, 0.006111f, 0.018333f, 0.030555f, 0.042777f, 0.055f };
    bool submissionMode = false;
    int hoverPosition = -1;

    Coroutine moveSquareCoroutine;
    Coroutine cycleAnimationCoroutine;

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake () {
        moduleId = moduleIdCounter++;
        
        foreach (KMSelectable Position in Positions) {
            Position.OnInteract += delegate () { PositionPress(Position); return false; };
            Position.OnHighlight += delegate () { if (submissionMode) { UpdateHoverPosition(Position); }  };
        }
    }

    void Start () {
        scaleFactor = Rnd.Range(18, 7857) * 0.001f; //scale factors in this range ensure that 1) all the possible hypotenuses have distinct values when truncated to 3 decimals of precision and 2) the maximum a scaled hypotenuse is under 100
        do {
            points[0] = Rnd.Range(0, 100);
            points[1] = Rnd.Range(0, 100);
            points[2] = Rnd.Range(0, 100);
            points[3] = Rnd.Range(0, 100);
        }
        while (points[0]==points[1] || points[0]==points[2] || points[0]==points[3] || points[1]==points[2] || points[1]==points[3] || points[2]==points[3]);
        for (int p = 0; p < 4; p++) {
            pointXs[p] = points[p] % 10;
            pointYs[p] = points[p] / 10;
        }
        for (int p = 0; p < 3; p++) {
            int xd = Math.Abs(pointXs[3] - pointXs[p]);
            int yd = Math.Abs(pointYs[3] - pointYs[p]);
            dists[p] = (float)Math.Sqrt(xd*xd + yd*yd) * scaleFactor;
        }
        Debug.LogFormat("[Pinpoint #{0}] Given points:", moduleId);
        Debug.LogFormat("[Pinpoint #{0}] {1}, distance of {2}", moduleId, gridPos(points[0]), trunc(dists[0]));
        Debug.LogFormat("[Pinpoint #{0}] {1}, distance of {2}", moduleId, gridPos(points[1]), trunc(dists[1]));
        Debug.LogFormat("[Pinpoint #{0}] {1}, distance of {2}", moduleId, gridPos(points[2]), trunc(dists[2]));
        Debug.LogFormat("[Pinpoint #{0}] With scale factor of {1}, the target point is {2}", moduleId, trunc(scaleFactor), gridPos(points[3]));
        Debug.LogFormat("<Pinpoint #{0}> Values w/ float imprecision: dists = {1}, scaleFactor = {2}", moduleId, dists.Join(" "), scaleFactor);
        UpdateDistanceArm();
        StartCoroutine(HueShift());
        if (cycleAnimationCoroutine != null)
            StopCoroutine(cycleAnimationCoroutine);
        cycleAnimationCoroutine = StartCoroutine(CycleAnimation());
    }

    private IEnumerator HueShift () {
        float elapsed = Rnd.Range(0f, 1f/HUESCALE);
        while (true) {
            var c = Color.HSVToRGB(elapsed * HUESCALE, 0.5f, 1f);
            Square.GetComponent<MeshRenderer>().material.color = c;
            Rails.GetComponent<MeshRenderer>().material.color = c;
            HorizScissors.color = c;
            VertiScissors.color = c;
            yield return null;
            elapsed += Time.deltaTime;
            if (elapsed * HUESCALE > 1f)
                elapsed = 0f;
        }
    }

    void PositionPress (KMSelectable P) {
        if (moduleSolved) { return; }
        for (int Q = 0; Q < Positions.Length; Q++) {
            if (Positions[Q] == P) {
                if (!submissionMode) {
                    if (cycleAnimationCoroutine != null)
                        StopCoroutine (cycleAnimationCoroutine);
                    if (moveSquareCoroutine != null)
                        StopCoroutine (moveSquareCoroutine);
                    submissionMode = true;
                    hoverPosition = Q;

                    if (moveSquareCoroutine != null)
                        StopCoroutine(moveSquareCoroutine);
                    var startPos = Square.transform.localPosition;
                    float qx = posLUT[Q % 10];
                    float qz = -posLUT[Q / 10];
                    var goalPos = new Vector3(qx, 0.02f, qz);
                    moveSquareCoroutine = StartCoroutine(MoveSquare(startPos, goalPos, 0.1f));

                    Arm.gameObject.SetActive(false);
                    DistanceObj.SetActive(false);
                    Debug.LogFormat("[Pinpoint #{0}] Entering submission mode.", moduleId);
                    return;
                } else {
                    submissionMode = false;
                    if (Q == points[3]) {
                        //TODO: add solve animation here
                        Module.HandlePass();
                        moduleSolved = true;
                        Debug.LogFormat("[Pinpoint #{0}] Submitted {1}, that is correct, module solved.", moduleId, gridPos(Q));
                    } else {
                        Module.HandleStrike();
                        Debug.LogFormat("[Pinpoint #{0}] Submitted {1}, that is incorrect, strike!", moduleId, gridPos(Q));
                        if (cycleAnimationCoroutine != null)
                            StopCoroutine(cycleAnimationCoroutine);
                        cycleAnimationCoroutine = StartCoroutine(CycleAnimation());
                    }
                }
            }
        }
    }

    void UpdateHoverPosition(KMSelectable P) {
        for (int Q = 0; Q < Positions.Length; Q++) {
            if (Positions[Q] == P) {
                hoverPosition = Q;

                if (moveSquareCoroutine != null)
                    StopCoroutine(moveSquareCoroutine);
                float qx = posLUT[Q % 10];
                float qz = -posLUT[Q / 10];
                var goalPos = new Vector3(qx, 0.02f, qz);
                moveSquareCoroutine = StartCoroutine(MoveSquare(Square.transform.localPosition, goalPos, 0.1f));
            }
        }
    }

    private IEnumerator CycleAnimation()
    {
        Color opc = new Color(1f, 1f, 1f, 0f);
        Arm.color = opc;
        Distance.color = opc;
        while (!submissionMode)
        {
            if (moveSquareCoroutine != null)
                StopCoroutine(moveSquareCoroutine);
            var startPos = Square.transform.localPosition;
            var goalPos = new Vector3(posLUT[pointXs[(shownPoint + 1) % 3]], 0.02f, -posLUT[pointYs[(shownPoint + 1) % 3]]);
            moveSquareCoroutine = StartCoroutine(MoveSquare(startPos, goalPos, ZIPTIME));
            yield return new WaitForSeconds(ZIPTIME);
            UpdateScissors();
            shownPoint = (shownPoint + 1) % 3;
            UpdateDistanceArm();
            var elapsed = 0f;
            Arm.gameObject.SetActive(true);
            DistanceObj.SetActive(true);
            while (elapsed < WAITTIME)
            {
                opc = new Color(1f, 1f, 1f, lerp(1f, 0f, Math.Abs(elapsed - WAITTIME / 2) / (WAITTIME / 2)));
                Arm.color = opc;
                Distance.color = opc;
                yield return null;
                elapsed += Time.deltaTime;
            }
        }
    }

    private IEnumerator MoveSquare (Vector3 start, Vector3 goal, float time)
    {
        var elapsed = 0f;
        while (elapsed < time)
        {
            Square.transform.localPosition = new Vector3(Mathf.Lerp(start.x, goal.x, elapsed / time), 0.02f, Mathf.Lerp(start.z, goal.z, elapsed / time));
            yield return null;
            elapsed += Time.deltaTime;
        }
        Square.transform.localPosition = new Vector3(goal.x, 0.02f, goal.z);
    }


    void Update()
    {
        UpdateScissors();
    }

    void UpdateScissors() {
        HorizScissors.transform.localPosition = new Vector3(0f, 0f, Square.transform.localPosition.z * 16.667f);
        VertiScissors.transform.localPosition = new Vector3(Square.transform.localPosition.x * 16.667f, 0f, 0f);
        HorizScissors.sprite = ScissorSprites[(int)Math.Round((Square.transform.localPosition.x + 0.055f) / 0.00305575f, 0)];
        VertiScissors.sprite = ScissorSprites[(int)Math.Round((-Square.transform.localPosition.z + 0.055f) / 0.00305575f, 0)];
    }

    void UpdateDistanceArm() {
        Arm.flipX = Square.transform.localPosition.x > 0f;
        Arm.flipY = Square.transform.localPosition.z < 0f;
        DistanceObj.transform.localPosition = new Vector3(Square.transform.localPosition.x > 0f ? -0.386f : 0.386f, 0.15f, Square.transform.localPosition.z < 0f ? 0.85f : -0.725f);
        Distance.text = trunc(dists[shownPoint]);
    }

    float lerp(float a, float b, float t) { //this assumes t is in the range 0-1
        return a*(1f-t) + b*t;
    }

    string trunc(float f) {
        string s = f.ToString();
        if (s.IndexOf('.') == -1) {
            return s + ".000";
        } else {
            string[] c = s.Split('.');
            c[1] = c[1].PadRight(3, '0').Substring(0, 3);
            return c[0] + "." + c[1];
        }
    }

    string gridPos(int p) {
        return "ABCDEFGHIJ"[p%10] + (p/10 + 1).ToString();
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} press A1 [Press the cell at position A1.] | Columns are labeled A-J from left to right. | Rows are labeled 1-10 from top to bottom.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToUpperInvariant();
        Match m = Regex.Match(command, @"^\s*(press|submit|click)\s+(?<col>[A-J])\s*(?<row>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            yield break;
        var cg = m.Groups["col"].Value;
        var rg = m.Groups["row"].Value;
        int col = cg[0] - 'A';
        int row;
        if (!int.TryParse(rg, out row) || row < 1 || row > 10)
            yield break;
        var pos = (row - 1) * 10 + col;
        yield return null;
        if (!submissionMode)
        {
            Positions[pos].OnInteract();
            yield return new WaitForSeconds(0.5f);
        }
        Positions[pos].OnHighlight();
        yield return new WaitForSeconds(0.75f);
        Positions[pos].OnInteract();
        yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        var pos = points[3];
        if (!submissionMode)
        {
            Positions[pos].OnInteract();
            yield return new WaitForSeconds(0.5f);
        }
        Positions[pos].OnHighlight();
        yield return new WaitForSeconds(0.75f);
        Positions[pos].OnInteract();
        yield break;
    }
}
