using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// Owns the tile grid and the hidden "reveal" picture behind it. Every tile is opaque and
    /// covers one grid cell of a big backdrop sprite (a composited Kenney critter); clearing
    /// tiles uncovers more of it, so the picture assembles itself from *holes*, not from the
    /// tile colors themselves the way Pixel Flow's grid works — same "reveal a picture" idea,
    /// different mechanism, and it means level content never has to be hand-authored pixel art.
    /// </summary>
    public class PuzzleGrid : MonoBehaviour
    {
        public Transform GridRoot;
        public Transform BackdropRoot;

        GridCell[,] cells;
        readonly int[] remainingPerColor = new int[6];
        readonly List<GridCell> clearHistory = new List<GridCell>();
        readonly List<GridCell> scratch = new List<GridCell>();

        public PieceColor RevealColor { get; private set; }
        public event Action<PieceColor, bool> OnCellCleared; // color, wasKey
        public event Action OnGridComplete;

        public void BuildLevel(List<PieceColor> colorsInPlay)
        {
            foreach (Transform child in GridRoot) Destroy(child.gameObject);
            foreach (Transform child in BackdropRoot) Destroy(child.gameObject);
            clearHistory.Clear();
            Array.Clear(remainingPerColor, 0, remainingPerColor.Length);

            cells = new GridCell[GameTuning.GridCols, GameTuning.GridRows];
            for (int row = 0; row < GameTuning.GridRows; row++)
            {
                for (int col = 0; col < GameTuning.GridCols; col++)
                {
                    var color = colorsInPlay[UnityEngine.Random.Range(0, colorsInPlay.Count)];
                    bool hasKey = UnityEngine.Random.value < GameTuning.KeyChancePerCell;

                    var go = new GameObject($"Cell_{col}_{row}", typeof(GridCell));
                    go.transform.SetParent(GridRoot, false);
                    go.transform.localPosition = GameTuning.CellPosition(col, row);

                    var cell = go.GetComponent<GridCell>();
                    cell.Setup(color, hasKey);
                    cells[col, row] = cell;
                    remainingPerColor[(int)color]++;
                }
            }

            BuildBackdrop();
        }

        void BuildBackdrop()
        {
            RevealColor = (PieceColor)UnityEngine.Random.Range(0, 6);

            var bodyGo = new GameObject("BackdropBody", typeof(SpriteRenderer));
            bodyGo.transform.SetParent(BackdropRoot, false);
            var bodySr = bodyGo.GetComponent<SpriteRenderer>();
            bodySr.sprite = ArtRegistry.Instance.Block(RevealColor);
            bodySr.sortingOrder = 0;
            // Deliberately muted/desaturated so a cleared hole always reads as "backdrop peeking
            // through" even when RevealColor happens to match a still-present active tile color.
            bodySr.color = new Color(0.55f, 0.5f, 0.62f, 1f);

            float gridWidth = GameTuning.GridCols * GameTuning.CellSize;
            float srcSize = Mathf.Max(bodySr.sprite.bounds.size.x, bodySr.sprite.bounds.size.y);
            float scale = (gridWidth * 0.8f) / srcSize;
            bodyGo.transform.localScale = Vector3.one * scale;

            var centerCol = (GameTuning.GridCols - 1) / 2f;
            var centerRow = (GameTuning.GridRows - 1) / 2f;
            bodyGo.transform.localPosition = GameTuning.CellPosition(0, 0)
                + new Vector2(centerCol * GameTuning.CellSize, -centerRow * GameTuning.CellSize);

            var faceGo = new GameObject("BackdropFace", typeof(SpriteRenderer));
            faceGo.transform.SetParent(bodyGo.transform, false);
            var faceSr = faceGo.GetComponent<SpriteRenderer>();
            faceSr.sprite = ArtRegistry.Instance.RandomFace();
            faceSr.sortingOrder = 1;
            faceSr.color = new Color(0.85f, 0.82f, 0.9f, 1f);
            faceGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            float faceSrcSize = Mathf.Max(faceSr.sprite.bounds.size.x, faceSr.sprite.bounds.size.y);
            faceGo.transform.localScale = Vector3.one * ((srcSize * 0.6f) / faceSrcSize);
        }

        public int RemainingOfColor(PieceColor c) => remainingPerColor[(int)c];

        public (bool cleared, bool wasKey) TryClearRandomCellOfColor(PieceColor c)
        {
            if (remainingPerColor[(int)c] <= 0) return (false, false);

            scratch.Clear();
            foreach (var cell in cells)
                if (cell != null && !cell.Cleared && cell.Color == c) scratch.Add(cell);

            if (scratch.Count == 0) return (false, false);

            var target = scratch[UnityEngine.Random.Range(0, scratch.Count)];
            target.Clear();
            remainingPerColor[(int)c]--;
            clearHistory.Add(target);

            OnCellCleared?.Invoke(c, target.HasKey);

            bool complete = true;
            foreach (var n in remainingPerColor) if (n > 0) { complete = false; break; }
            if (complete) OnGridComplete?.Invoke();

            return (true, target.HasKey);
        }

        public bool UndoLast()
        {
            if (clearHistory.Count == 0) return false;
            var cell = clearHistory[^1];
            clearHistory.RemoveAt(clearHistory.Count - 1);
            cell.Restore();
            remainingPerColor[(int)cell.Color]++;
            return true;
        }

        public void PulseColor(PieceColor c) => StartCoroutine(PulseRoutine(c));

        IEnumerator PulseRoutine(PieceColor c)
        {
            var targets = new List<GridCell>();
            foreach (var cell in cells)
                if (cell != null && !cell.Cleared && cell.Color == c) targets.Add(cell);

            for (int i = 0; i < 3; i++)
            {
                foreach (var t in targets) t.transform.localScale *= 1.3f;
                yield return new WaitForSeconds(0.15f);
                foreach (var t in targets) t.transform.localScale /= 1.3f;
                yield return new WaitForSeconds(0.15f);
            }
        }
    }
}
